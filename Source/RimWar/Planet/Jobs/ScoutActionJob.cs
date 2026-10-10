using System.Collections.Generic;
using RimWar.Threading;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    /// <summary>
    /// A settlement sends a scouting party against a weaker hostile caravan, war object or settlement.
    /// Same rules as the removed WorldComponent_PowerTracker.AttemptScoutMission_UnThreaded.
    ///
    /// The original loop skips a player caravan that fails a traversal-distance check (a world flood fill, game thread
    /// only) and moves on. Compute() therefore returns the candidates in loop order, and Commit() takes the first one
    /// that still passes its live checks.
    /// </summary>
    public sealed class ScoutActionJob : SettlementActionJob
    {
        private struct Candidate
        {
            public WorldObject Handle;
            public Faction Faction;
            public WoKind Kind;
            public int Points;
            public int MaxTraversal; // -1: no traversal check
        }

        private readonly bool forcePlayerTown;
        private readonly bool forcePlayerCaravan;

        private List<Candidate> candidates;

        public ScoutActionJob(RimWarData rwd, Settlement parentSettlement, RimWarSettlementComp rwsComp,
                              bool forcePlayerTown, bool forcePlayerCaravan, bool ignoreRestrictions)
            : base(rwd, parentSettlement, rwsComp, false, ignoreRestrictions)
        {
            this.forcePlayerTown = forcePlayerTown;
            this.forcePlayerCaravan = forcePlayerCaravan;
        }

        protected internal override void Compute(WorldSnapshot s, System.Random rng)
        {
            int self = Self(s);
            if (self < 0)
            {
                return;
            }
            WoRecord src = s.Objects[self];
            RimWarBehavior behavior = s.Behavior(src.Faction);
            int targetRange = ScanRange(s, self);
            if (behavior == RimWarBehavior.Expansionist)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.3f);
            }
            else if (behavior == RimWarBehavior.Warmonger)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.2f);
            }
            else if (behavior == RimWarBehavior.Aggressive)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.1f);
            }

            List<int> woList = PickObjects(s, self, targetRange, rng);
            float maxTargetPoints = (src.Points - src.PointDamage) * .5f;
            candidates = new List<Candidate>();
            for (int i = 0; i < woList.Count; i++)
            {
                int c = woList[i];
                WoRecord rec = s.Objects[c];
                if (rec.Faction < 0)
                {
                    continue;
                }
                if (!((s.Hostile(rec.Faction, src.Faction) && s.ApproxDistance(self, c) <= targetRange) || ignoreRestrictions))
                {
                    continue;
                }
                bool isPlayer = s.IsPlayer(rec.Faction);
                if (isPlayer && s.PreventPlayerActionsUntil > s.Tick && !ignoreRestrictions)
                {
                    continue;
                }
                if (isPlayer && src.PlayerHeat < s.MinimumHeatForPlayerAction && !s.AtWar(src.Faction, s.PlayerFaction) && !ignoreRestrictions)
                {
                    continue;
                }
                if (s.IsVassal(rec.Faction) && rec.Kind == WoKind.Settlement && rec.HasRimWarComp && src.PlayerHeat < rec.VassalHeat && !ignoreRestrictions)
                {
                    break;
                }
                if (isPlayer && !s.Factions[src.Faction].CanFight200 && !forcePlayerCaravan && !forcePlayerTown)
                {
                    continue;
                }

                Candidate candidate = new Candidate
                {
                    Handle = rec.Handle,
                    Faction = s.Factions[rec.Faction].Handle,
                    Kind = rec.Kind,
                    MaxTraversal = -1
                };
                if (rec.Kind == WoKind.Caravan)
                {
                    if (rec.CaravanWealth / 200 <= maxTargetPoints)
                    {
                        candidate.Points = WorldUtility.CalculateScoutMissionPoints(behavior, Mathf.RoundToInt(rec.CaravanWealth / 200), rng);
                        if (!ignoreRestrictions)
                        {
                            candidate.MaxTraversal = Mathf.RoundToInt(targetRange * rec.CaravanVisibility);
                            candidates.Add(candidate);
                            continue;
                        }
                        candidates.Add(candidate);
                        break;
                    }
                }
                else if (rec.Kind == WoKind.WarObject)
                {
                    if (rec.Points <= maxTargetPoints || ignoreRestrictions)
                    {
                        candidate.Points = ScoutPointsAgainst(s, behavior, src.Faction, rec, rng);
                        candidates.Add(candidate);
                        break;
                    }
                }
                else if (rec.Kind == WoKind.Settlement)
                {
                    if (rec.HasRimWarComp && (rec.Points <= maxTargetPoints || ignoreRestrictions))
                    {
                        candidate.Points = ScoutPointsAgainst(s, behavior, src.Faction, rec, rng);
                        candidates.Add(candidate);
                        break;
                    }
                }
            }
        }

        private List<int> PickObjects(WorldSnapshot s, int self, int targetRange, System.Random rng)
        {
            List<int> woList = new List<int>();
            if (s.Settings.forceRandomObject)
            {
                if (s.ObjectCount > 0)
                {
                    woList.Add(rng.Next(s.ObjectCount));
                }
            }
            else if (forcePlayerCaravan)
            {
                List<int> caravans = new List<int>();
                for (int i = 0; i < s.ObjectCount; i++)
                {
                    if (s.Objects[i].Kind == WoKind.Caravan)
                    {
                        caravans.Add(i);
                    }
                }
                if (caravans.Count > 0)
                {
                    woList.Add(rng.RandomElement(caravans));
                }
            }
            else if (forcePlayerTown)
            {
                // Port of WorldUtility.GetClosestSettlementOfFaction(Faction.OfPlayer, parent.Tile, 500).
                int closest = -1;
                float closestDist = 0f;
                List<int> playerTowns = PlayerSettlements(s);
                for (int i = 0; i < playerTowns.Count; i++)
                {
                    int p = playerTowns[i];
                    if (!s.Objects[p].HasRimWarComp)
                    {
                        continue;
                    }
                    float dist = s.ApproxDistance(self, p);
                    if (dist <= 500 && (closest < 0 || dist < closestDist))
                    {
                        closest = p;
                        closestDist = dist;
                    }
                }
                if (closest >= 0)
                {
                    woList.Add(closest);
                }
            }
            else
            {
                woList = s.ObjectsInRange(self, targetRange, rng, WorldUtility.maxObjectsPerScan);
            }
            return woList;
        }

        private static int ScoutPointsAgainst(WorldSnapshot s, RimWarBehavior behavior, int ownFaction, WoRecord target, System.Random rng)
        {
            int pts = WorldUtility.CalculateScoutMissionPoints(behavior, target.Points, rng);
            if (s.AtWar(ownFaction, target.Faction))
            {
                pts = Mathf.RoundToInt(pts * 1.2f);
            }
            return pts;
        }

        protected internal override void Commit()
        {
            if (candidates == null || candidates.Count == 0 || !SourceStillValid())
            {
                return;
            }
            WorldComponent_PowerTracker wcpt = Tracker;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate c = candidates[i];
                WorldObject wo = c.Handle;
                if (!TargetStillValid(wo, c.Faction))
                {
                    continue;
                }
                bool isPlayer = wo.Faction == Faction.OfPlayer;
                if (isPlayer && wcpt.preventActionsAgainstPlayerUntilTick > now && !ignoreRestrictions)
                {
                    continue;
                }
                if (isPlayer && rwsComp.PlayerHeat < wcpt.minimumHeatForPlayerAction && !rwd.IsAtWarWith(Faction.OfPlayer) && !ignoreRestrictions)
                {
                    continue;
                }
                RimWarSettlementComp rwscVassal = wo.GetComponent<RimWarSettlementComp>();
                if (WorldUtility.IsVassalFaction(wo.Faction) && rwscVassal != null && rwsComp.PlayerHeat < rwscVassal.vassalHeat && !ignoreRestrictions)
                {
                    return;
                }
                if (c.MaxTraversal >= 0 && Find.WorldGrid.TraversalDistanceBetween(wo.Tile, parentSettlement.Tile, true, c.MaxTraversal) > c.MaxTraversal)
                {
                    continue;
                }
                Execute(c, wcpt);
                return;
            }
        }

        private void Execute(Candidate c, WorldComponent_PowerTracker wcpt)
        {
            WorldObject wo = c.Handle;
            if (c.Kind == WoKind.Caravan)
            {
                WorldUtility.CreateScout(c.Points, rwd, parentSettlement, parentSettlement.Tile, wo, WorldObjectDefOf.Caravan);
                rwsComp.RimWarPoints -= c.Points;
                rwsComp.PlayerHeat = 0;
                wcpt.minimumHeatForPlayerAction += wcpt.GetHeatForAction(RimWarAction.ScoutingParty);
            }
            else if (c.Kind == WoKind.WarObject)
            {
                WorldUtility.CreateScout(c.Points, rwd, parentSettlement, parentSettlement.Tile, wo, RimWarDefOf.RW_WarObject);
                rwsComp.RimWarPoints -= c.Points;
                if (wo.Faction == Faction.OfPlayer || WorldUtility.IsVassalFaction(wo.Faction))
                {
                    rwsComp.PlayerHeat = 0;
                    wcpt.minimumHeatForPlayerAction += wcpt.GetHeatForAction(RimWarAction.ScoutingParty);
                }
            }
            else
            {
                WorldUtility.CreateScout(c.Points, rwd, parentSettlement, parentSettlement.Tile, wo, WorldObjectDefOf.Settlement);
                rwsComp.RimWarPoints -= c.Points;
                ApplyHostileActionHeat(wo, RimWarAction.ScoutingParty);
            }
        }
    }
}
