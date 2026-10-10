using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using RimWar.Options;
using RimWar.Planet;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Threading
{
    public enum WoKind : byte
    {
        Other,
        Settlement,
        WarObject,
        Caravan,
        BattleSite,
        RimWarSite
    }

    /// <summary>
    /// Value copy of one world object, taken on the game thread.
    /// <see cref="Handle"/> is an opaque identity: worker code may compare it or hand it back to Commit(),
    /// but must never dereference it. The same applies to <see cref="Tile"/>: PlanetTile's ==, Equals and
    /// GetHashCode read Find.WorldGrid, so worker code compares TileId/LayerId instead.
    /// </summary>
    public struct WoRecord
    {
        public int LoadId;
        public WorldObject Handle;
        public WoKind Kind;
        public PlanetTile Tile;
        public int TileId;
        public int LayerId;
        public bool OnOrbit;
        public Vector3 Normal;
        public float DistanceScale;
        public short Faction;
        public bool HasRimWarComp;
        public bool ValidSettlement;
        public bool DefIsVanillaSettlement;
        public int Points;
        public int PointDamage;
        public int PlayerHeat;
        public int VassalHeat;
        public bool Interactable;
        public float CaravanWealth;
        public float CaravanVisibility;
        public int CaravanTargetId;
        public int FirstSiteUnit;
        public int SiteUnitCount;
    }

    /// <summary>
    /// Position of a tile in the form worker code can use for distances. Create with <see cref="Of"/> on the game thread.
    /// </summary>
    public struct TilePoint
    {
        public PlanetTile Tile;
        public int TileId;
        public int LayerId;
        public Vector3 Normal;
        public float DistanceScale;

        internal TilePoint(WoRecord rec)
        {
            Tile = rec.Tile;
            TileId = rec.TileId;
            LayerId = rec.LayerId;
            Normal = rec.Normal;
            DistanceScale = rec.DistanceScale;
        }

        /// <summary>Game thread only.</summary>
        public static TilePoint Of(PlanetTile tile)
        {
            MainThread.Verify();
            TilePoint p = new TilePoint();
            p.Tile = tile;
            p.TileId = tile.tileId;
            PlanetLayer layer = tile.Layer;
            p.LayerId = layer != null ? layer.LayerID : -1;
            p.Normal = Find.WorldGrid.GetTileCenter(tile).normalized;
            p.DistanceScale = layer != null && layer.AverageTileSize > 0f ? layer.Radius / layer.AverageTileSize : 0f;
            return p;
        }
    }

    public struct SiteUnitRecord
    {
        public short Faction;
        public int Points;
        public int PointDamage;
    }

    public struct FactionRecord
    {
        public Faction Handle;
        public RimWarData Rwd;
        public RimWarBehavior Behavior;
        public bool IsPlayer;
        public bool IsVassal;
        public bool CanFight200;
        public bool CanTrade;
        public bool MovesAtNight;
    }

    /// <summary>
    /// Immutable (after capture) copy of the world state that RimWar jobs need.
    /// Replaces the old WorldUtility.CopyData()/WorldObjectsHolder, which handed worker threads RimWorld's live list.
    ///
    /// Lifecycle: captured on the game thread, shared by any number of jobs (reference counted),
    /// recycled into a pool once the store and every job have released it. Never written after capture.
    /// </summary>
    public sealed class WorldSnapshot
    {
        private WoRecord[] objects = new WoRecord[256];
        private SiteUnitRecord[] siteUnits = new SiteUnitRecord[64];
        private FactionRecord[] factions = new FactionRecord[32];
        private bool[] hostile = new bool[32 * 32];
        private bool[] atWar = new bool[32 * 32];
        private readonly Dictionary<int, int> indexByLoadId = new Dictionary<int, int>();
        private readonly Dictionary<Faction, int> factionIndex = new Dictionary<Faction, int>();
        private int refCount;

        public int Tick { get; private set; }
        public int Epoch { get; private set; }
        public SettingsRef Settings { get; private set; }
        public int PreventPlayerActionsUntil { get; private set; }
        public int MinimumHeatForPlayerAction { get; private set; }
        public int ObjectCount { get; private set; }
        public int FactionCount { get; private set; }
        public int PlayerFaction { get; private set; }

        /// <summary>Do not modify. Valid entries are [0, ObjectCount).</summary>
        public WoRecord[] Objects => objects;

        /// <summary>Do not modify. Use WoRecord.FirstSiteUnit/SiteUnitCount.</summary>
        public SiteUnitRecord[] SiteUnits => siteUnits;

        /// <summary>Do not modify. Valid entries are [0, FactionCount).</summary>
        public FactionRecord[] Factions => factions;

        public bool TryGetIndex(int loadId, out int index)
        {
            return indexByLoadId.TryGetValue(loadId, out index);
        }

        public bool Hostile(int factionA, int factionB)
        {
            if (factionA < 0 || factionB < 0)
            {
                return false;
            }
            return hostile[factionA * FactionCount + factionB];
        }

        /// <summary>True when faction A's RimWarData lists faction B as a war enemy.</summary>
        public bool AtWar(int factionA, int factionB)
        {
            if (factionA < 0 || factionB < 0)
            {
                return false;
            }
            return atWar[factionA * FactionCount + factionB];
        }

        public bool IsAtWar(int faction)
        {
            if (faction < 0)
            {
                return false;
            }
            int row = faction * FactionCount;
            for (int i = 0; i < FactionCount; i++)
            {
                if (atWar[row + i])
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsPlayer(int faction)
        {
            return faction >= 0 && faction == PlayerFaction;
        }

        public bool IsVassal(int faction)
        {
            return faction >= 0 && factions[faction].IsVassal;
        }

        public RimWarBehavior Behavior(int faction)
        {
            return faction >= 0 ? factions[faction].Behavior : RimWarBehavior.Undefined;
        }

        public bool SameTile(int a, int b)
        {
            return objects[a].TileId == objects[b].TileId && objects[a].LayerId == objects[b].LayerId;
        }

        /// <summary>
        /// Same result as WorldGrid.ApproxDistanceInTiles(a.Tile, b.Tile) for tiles on one planet layer
        /// (PlanetLayer.ApproxDistanceInTiles: spherical distance between normalized tile centers * radius / average tile size).
        /// Tiles on different layers are treated as unreachable.
        /// </summary>
        public float ApproxDistance(int a, int b)
        {
            return ApproxDistance(objects[a].Normal, objects[a].LayerId, objects[a].DistanceScale, b);
        }

        /// <summary>Distance from a tile described by a TilePoint (see <see cref="TilePoint"/>) to object b.</summary>
        public float ApproxDistance(TilePoint from, int b)
        {
            return ApproxDistance(from.Normal, from.LayerId, from.DistanceScale, b);
        }

        private float ApproxDistance(Vector3 na, int layerA, float scaleA, int b)
        {
            if (layerA != objects[b].LayerId)
            {
                return float.MaxValue;
            }
            Vector3 nb = objects[b].Normal;
            if (na == nb)
            {
                return 0f;
            }
            return Mathf.Acos(Vector3.Dot(na, nb)) * scaleA;
        }

        /// <summary>
        /// Port of WorldUtility.GetWorldObjectsInRange for jobs: objects within range of object `from`, excluding orbit
        /// objects, in random order, at most `cap` entries. Returns an empty list when `from` is in orbit.
        /// </summary>
        public List<int> ObjectsInRange(int from, float range, System.Random rng, int cap)
        {
            if (objects[from].OnOrbit)
            {
                return new List<int>();
            }
            return ObjectsInRange(new TilePoint(objects[from]), range, rng, cap);
        }

        /// <summary>As above, around an arbitrary tile captured on the game thread with <see cref="TilePoint.Of"/>.</summary>
        public List<int> ObjectsInRange(TilePoint from, float range, System.Random rng, int cap)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < ObjectCount; i++)
            {
                if (objects[i].OnOrbit)
                {
                    continue;
                }
                bool sameTile = objects[i].TileId == from.TileId && objects[i].LayerId == from.LayerId;
                if (sameTile || ApproxDistance(from, i) <= range)
                {
                    result.Add(i);
                }
            }
            rng.Shuffle(result);
            if (result.Count > cap)
            {
                result.RemoveRange(cap, result.Count - cap);
            }
            return result;
        }

        internal void AddRef()
        {
            Interlocked.Increment(ref refCount);
        }

        /// <summary>Thread-safe. The last release returns the buffers to the pool.</summary>
        internal void Release()
        {
            if (Interlocked.Decrement(ref refCount) == 0)
            {
                WorldSnapshotStore.Recycle(this);
            }
        }

        /// <summary>Game thread only. Fills this (pooled, unreferenced) instance from live state.</summary>
        internal void Capture(int tick, int epoch, WorldComponent_PowerTracker wcpt)
        {
            MainThread.Verify();
            Tick = tick;
            Epoch = epoch;
            Settings = new SettingsRef();
            PreventPlayerActionsUntil = wcpt != null ? wcpt.preventActionsAgainstPlayerUntilTick : 0;
            MinimumHeatForPlayerAction = wcpt != null ? wcpt.minimumHeatForPlayerAction : 0;

            CaptureFactions();

            List<WorldObject> live = Find.WorldObjects.AllWorldObjects;
            if (objects.Length < live.Count)
            {
                objects = new WoRecord[Grow(objects.Length, live.Count)];
            }
            indexByLoadId.Clear();
            int siteUnitCount = 0;
            PlanetLayer orbit = Find.WorldGrid.Orbit;
            int count = 0;
            for (int i = 0; i < live.Count; i++)
            {
                WorldObject wo = live[i];
                if (wo == null || wo.Destroyed || !wo.Tile.Valid)
                {
                    continue;
                }
                TilePoint point = TilePoint.Of(wo.Tile);
                WoRecord rec = new WoRecord();
                rec.LoadId = wo.ID;
                rec.Handle = wo;
                rec.Tile = wo.Tile;
                rec.TileId = point.TileId;
                rec.LayerId = point.LayerId;
                rec.OnOrbit = orbit != null && point.LayerId == orbit.LayerID;
                rec.Normal = point.Normal;
                rec.DistanceScale = point.DistanceScale;
                rec.Faction = (short)IndexOf(wo.Faction);
                rec.CaravanTargetId = -1;
                rec.FirstSiteUnit = siteUnitCount;

                if (wo is BattleSite || wo is RimWarSite)
                {
                    rec.Kind = wo is BattleSite ? WoKind.BattleSite : WoKind.RimWarSite;
                    List<WarObject> units = ((RimWarSite)wo).Units;
                    if (siteUnits.Length < siteUnitCount + units.Count)
                    {
                        Array.Resize(ref siteUnits, Grow(siteUnits.Length, siteUnitCount + units.Count));
                    }
                    for (int u = 0; u < units.Count; u++)
                    {
                        WarObject unit = units[u];
                        siteUnits[siteUnitCount].Faction = (short)IndexOf(unit?.Faction);
                        siteUnits[siteUnitCount].Points = unit != null ? unit.RimWarPoints : 0;
                        siteUnits[siteUnitCount].PointDamage = unit != null ? unit.PointDamage : 0;
                        siteUnitCount++;
                    }
                    rec.SiteUnitCount = units.Count;
                }
                else if (wo is RimWorld.Planet.Settlement)
                {
                    rec.Kind = WoKind.Settlement;
                    rec.ValidSettlement = WorldUtility.IsValidSettlement(wo);
                    rec.DefIsVanillaSettlement = wo.def == WorldObjectDefOf.Settlement;
                    RimWarSettlementComp rwsc = wo.GetComponent<RimWarSettlementComp>();
                    if (rwsc != null)
                    {
                        rec.HasRimWarComp = true;
                        rec.Points = rwsc.RimWarPoints;
                        rec.PointDamage = rwsc.PointDamage;
                        rec.PlayerHeat = rwsc.PlayerHeat;
                        rec.VassalHeat = rwsc.vassalHeat;
                    }
                }
                else if (wo is WarObject warObject)
                {
                    rec.Kind = WoKind.WarObject;
                    rec.Points = warObject.RimWarPoints;
                    rec.PointDamage = warObject.PointDamage;
                    rec.Interactable = warObject.interactable;
                }
                else if (wo is Caravan caravan)
                {
                    rec.Kind = WoKind.Caravan;
                    rec.CaravanWealth = caravan.PlayerWealthForStoryteller;
                    rec.CaravanVisibility = caravan.Visibility;
                    RimWarCaravanComp rwcc = caravan.GetComponent<RimWarCaravanComp>();
                    if (rwcc != null)
                    {
                        rec.HasRimWarComp = true;
                        if (rwcc.currentTarget != null)
                        {
                            rec.CaravanTargetId = rwcc.currentTarget.ID;
                        }
                    }
                }
                else
                {
                    rec.Kind = WoKind.Other;
                }

                objects[count] = rec;
                indexByLoadId[rec.LoadId] = count;
                count++;
            }
            if (count < ObjectCount)
            {
                // Drop references held by the previous use of this pooled buffer.
                Array.Clear(objects, count, ObjectCount - count);
            }
            ObjectCount = count;
        }

        private static int Grow(int current, int needed)
        {
            int size = Math.Max(current, 16);
            while (size < needed)
            {
                size *= 2;
            }
            return size;
        }

        private int IndexOf(Faction faction)
        {
            if (faction == null)
            {
                return -1;
            }
            int index;
            return factionIndex.TryGetValue(faction, out index) ? index : -1;
        }

        private void CaptureFactions()
        {
            List<Faction> all = Find.FactionManager.AllFactionsListForReading;
            int count = all.Count;
            if (factions.Length < count)
            {
                factions = new FactionRecord[count];
            }
            if (hostile.Length < count * count)
            {
                hostile = new bool[count * count];
                atWar = new bool[count * count];
            }
            factionIndex.Clear();
            PlayerFaction = -1;
            for (int i = 0; i < count; i++)
            {
                Faction f = all[i];
                factionIndex[f] = i;
                RimWarData rwd = WorldUtility.GetRimWarDataForFaction(f);
                FactionRecord rec = new FactionRecord();
                rec.Handle = f;
                rec.Rwd = rwd;
                rec.Behavior = rwd != null ? rwd.behavior : RimWarBehavior.Undefined;
                rec.MovesAtNight = rwd != null && rwd.movesAtNight;
                rec.IsPlayer = f.IsPlayer;
                rec.IsVassal = WorldUtility.IsVassalFaction(f);
                rec.CanFight200 = WorldUtility.FactionCanFight(200, f);
                rec.CanTrade = WorldUtility.FactionCanTrade(f);
                factions[i] = rec;
                if (f == Faction.OfPlayerSilentFail)
                {
                    PlayerFaction = i;
                }
            }
            for (int a = 0; a < count; a++)
            {
                RimWarData rwdA = factions[a].Rwd;
                for (int b = 0; b < count; b++)
                {
                    int cell = a * count + b;
                    hostile[cell] = a != b && all[a].HostileTo(all[b]);
                    atWar[cell] = rwdA != null && rwdA.IsAtWarWith(all[b]);
                }
            }
            if (factions.Length > count)
            {
                Array.Clear(factions, count, factions.Length - count);
            }
            FactionCount = count;
        }
    }

    /// <summary>
    /// Owns the current snapshot and the buffer pool. Capture/Acquire run on the game thread;
    /// Recycle may run on the worker thread.
    /// </summary>
    public static class WorldSnapshotStore
    {
        private static WorldSnapshot current;
        private static readonly ConcurrentQueue<WorldSnapshot> pool = new ConcurrentQueue<WorldSnapshot>();

        public static int Captures { get; private set; }

        /// <summary>
        /// Returns a snapshot no older than maxAgeTicks, with one reference added for the caller.
        /// Which snapshot is used depends only on tick numbers, so results are deterministic.
        /// </summary>
        internal static WorldSnapshot Acquire(int maxAgeTicks, int epoch, WorldComponent_PowerTracker wcpt)
        {
            MainThread.Verify();
            int now = GenTicks.TicksGame;
            if (current == null || current.Epoch != epoch || now < current.Tick || now - current.Tick >= maxAgeTicks)
            {
                WorldSnapshot snap;
                if (!pool.TryDequeue(out snap))
                {
                    snap = new WorldSnapshot();
                }
                snap.Capture(now, epoch, wcpt);
                snap.AddRef(); // the store's reference
                WorldSnapshot old = current;
                current = snap;
                if (old != null)
                {
                    old.Release();
                }
                Captures++;
            }
            current.AddRef();
            return current;
        }

        internal static void Recycle(WorldSnapshot snapshot)
        {
            pool.Enqueue(snapshot);
        }

        internal static void Reset()
        {
            MainThread.Verify();
            WorldSnapshot old = current;
            current = null;
            if (old != null)
            {
                old.Release();
            }
        }
    }
}
