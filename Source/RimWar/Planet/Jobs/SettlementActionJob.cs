using System.Collections.Generic;
using RimWar.Threading;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWar.Planet
{
    /// <summary>
    /// Base for a settlement's periodic action (warband, scout, trader, diplomat, settler).
    /// The constructor runs on the game thread and captures only identities and flags; Compute() reads the snapshot;
    /// Commit() re-validates against live state and creates the unit through WorldUtility.
    /// </summary>
    public abstract class SettlementActionJob : RimWarJob
    {
        // Same cap as RimWarSettlementComp.NearbyHostileSettlements/NearbyFriendlySettlements.
        private const int MaxNearbySettlements = 20;

        protected readonly RimWarData rwd;
        protected readonly Settlement parentSettlement;
        protected readonly RimWarSettlementComp rwsComp;
        protected readonly Faction faction;
        protected readonly bool forcePlayer;
        protected readonly bool ignoreRestrictions;
        private readonly int parentId;
        private readonly int[] nearbyIds;

        protected SettlementActionJob(RimWarData rwd, Settlement parentSettlement, RimWarSettlementComp rwsComp,
                                      bool forcePlayer, bool ignoreRestrictions)
        {
            MainThread.Verify();
            this.rwd = rwd;
            this.parentSettlement = parentSettlement;
            this.rwsComp = rwsComp;
            this.faction = parentSettlement.Faction;
            this.forcePlayer = forcePlayer;
            this.ignoreRestrictions = ignoreRestrictions;
            this.parentId = parentSettlement.ID;

            List<Settlement> inRange = rwsComp.OtherSettlementsInRange;
            List<int> ids = new List<int>(inRange.Count);
            for (int i = 0; i < inRange.Count; i++)
            {
                if (inRange[i] != null)
                {
                    ids.Add(inRange[i].ID);
                }
            }
            this.nearbyIds = ids.ToArray();
        }

        // ---- Compute() helpers: snapshot only ----

        /// <summary>Snapshot index of the acting settlement, or -1 if it no longer exists or has no known faction.</summary>
        protected int Self(WorldSnapshot s)
        {
            int index;
            if (!s.TryGetIndex(parentId, out index) || s.Objects[index].Faction < 0)
            {
                return -1;
            }
            return index;
        }

        protected static int ScanRange(WorldSnapshot s, int self)
        {
            return RimWarSettlementComp.ScanRangeFor(s.Objects[self].Points, s.Settings.settlementScanRangeDivider, s.Settings.maxSettelementScanRange);
        }

        /// <summary>Port of rwd.HostileSettlements / NonHostileSettlements (WorldUtility.UpdateRWDSettlementLists).</summary>
        protected static List<int> SettlementsByHostility(WorldSnapshot s, int factionIndex, bool hostile)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < s.ObjectCount; i++)
            {
                WoRecord rec = s.Objects[i];
                if (rec.Kind != WoKind.Settlement || !rec.HasRimWarComp || rec.Faction < 0 || s.Behavior(rec.Faction) == RimWarBehavior.Excluded)
                {
                    continue;
                }
                if (s.Hostile(rec.Faction, factionIndex) == hostile)
                {
                    result.Add(i);
                }
            }
            return result;
        }

        /// <summary>Port of GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements.</summary>
        protected static List<int> PlayerSettlements(WorldSnapshot s)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < s.ObjectCount; i++)
            {
                WoRecord rec = s.Objects[i];
                if (rec.Kind == WoKind.Settlement && rec.ValidSettlement && s.IsPlayer(rec.Faction))
                {
                    result.Add(i);
                }
            }
            return result;
        }

        /// <summary>Port of rwsComp.NearbyHostileSettlements (hostile == true) / NearbyFriendlySettlements (hostile == false).</summary>
        protected List<int> NearbySettlements(WorldSnapshot s, int self, bool hostile)
        {
            List<int> result = new List<int>();
            int ownFaction = s.Objects[self].Faction;
            for (int i = 0; i < nearbyIds.Length && result.Count < MaxNearbySettlements; i++)
            {
                int index;
                if (!s.TryGetIndex(nearbyIds[i], out index))
                {
                    continue;
                }
                WoRecord rec = s.Objects[index];
                if (rec.HasRimWarComp && rec.Points > 0 && rec.Faction >= 0 && s.Hostile(rec.Faction, ownFaction) == hostile)
                {
                    result.Add(index);
                }
            }
            return result;
        }

        /// <summary>
        /// The rate limits on actions against the player and vassals. Evaluated in Compute() with snapshot values and again
        /// in Commit() with live values: several jobs computed from one snapshot must not all pass the same limit.
        /// </summary>
        protected static bool BlockedByPlayerGates(bool targetIsPlayer, bool targetIsVassal, bool targetHasComp,
                                                   int sourcePlayerHeat, int targetVassalHeat, int preventUntil,
                                                   int minimumHeat, int now, bool atWarWithPlayer, bool ignoreRestrictions)
        {
            if (ignoreRestrictions)
            {
                return false;
            }
            if (targetIsPlayer && preventUntil > now)
            {
                return true;
            }
            if (targetIsPlayer && sourcePlayerHeat < minimumHeat && !atWarWithPlayer)
            {
                return true;
            }
            if (targetIsVassal && targetHasComp && sourcePlayerHeat < targetVassalHeat)
            {
                return true;
            }
            return false;
        }

        protected static bool BlockedByPlayerGates(WorldSnapshot s, int self, int target, bool ignoreRestrictions)
        {
            WoRecord src = s.Objects[self];
            WoRecord dst = s.Objects[target];
            bool targetHasSettlementComp = dst.Kind == WoKind.Settlement && dst.HasRimWarComp;
            return BlockedByPlayerGates(s.IsPlayer(dst.Faction), s.IsVassal(dst.Faction), targetHasSettlementComp, src.PlayerHeat,
                                        dst.VassalHeat, s.PreventPlayerActionsUntil, s.MinimumHeatForPlayerAction, s.Tick,
                                        s.AtWar(src.Faction, s.PlayerFaction), ignoreRestrictions);
        }

        // ---- Commit() helpers: game thread ----

        protected static WorldComponent_PowerTracker Tracker => WorldUtility.Get_WCPT();

        protected bool SourceStillValid()
        {
            return parentSettlement != null && !parentSettlement.Destroyed && parentSettlement.Spawned &&
                   parentSettlement.Faction == faction && rwsComp != null && rwsComp.parent == parentSettlement;
        }

        protected static bool TargetStillValid(WorldObject target, Faction expectedFaction)
        {
            return target != null && !target.Destroyed && target.Spawned && target.Faction == expectedFaction;
        }

        protected bool BlockedByPlayerGatesLive(WorldObject target)
        {
            WorldComponent_PowerTracker wcpt = Tracker;
            RimWarSettlementComp targetComp = target.GetComponent<RimWarSettlementComp>();
            return BlockedByPlayerGates(target.Faction == Faction.OfPlayer, WorldUtility.IsVassalFaction(target.Faction),
                                        targetComp != null, rwsComp.PlayerHeat, targetComp != null ? targetComp.vassalHeat : 0,
                                        wcpt.preventActionsAgainstPlayerUntilTick, wcpt.minimumHeatForPlayerAction,
                                        Find.TickManager.TicksGame, rwd.IsAtWarWith(Faction.OfPlayer), ignoreRestrictions);
        }

        /// <summary>Heat bookkeeping shared by the hostile actions (warband, launched warband, settlement scouting).</summary>
        protected void ApplyHostileActionHeat(WorldObject target, RimWarAction action)
        {
            WorldComponent_PowerTracker wcpt = Tracker;
            if (target.Faction == Faction.OfPlayer)
            {
                rwsComp.PlayerHeat = 0;
                wcpt.minimumHeatForPlayerAction += wcpt.GetHeatForAction(action);
            }
            else if (WorldUtility.IsVassalFaction(target.Faction))
            {
                rwsComp.PlayerHeat = 0;
                RimWarSettlementComp rwscVassal = target.GetComponent<RimWarSettlementComp>();
                if (rwscVassal != null)
                {
                    rwscVassal.vassalHeat += (2 * wcpt.GetHeatForAction(action));
                }
            }
        }
    }
}
