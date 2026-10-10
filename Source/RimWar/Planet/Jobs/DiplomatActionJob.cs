using System.Collections.Generic;
using RimWar.Threading;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    /// <summary>
    /// A settlement sends a diplomat to a settlement in range.
    /// Same rules as the removed WorldComponent_PowerTracker.AttemptDiplomatMission_UnThreaded.
    /// </summary>
    public sealed class DiplomatActionJob : SettlementActionJob
    {
        private WorldObject target;
        private Faction targetFaction;
        private int points;

        public DiplomatActionJob(RimWarData rwd, Settlement parentSettlement, RimWarSettlementComp rwsComp)
            : base(rwd, parentSettlement, rwsComp, false, false)
        {
        }

        protected internal override void Compute(WorldSnapshot s, System.Random rng)
        {
            int self = Self(s);
            if (self < 0)
            {
                return;
            }
            WoRecord src = s.Objects[self];
            int targetRange = ScanRange(s, self);

            List<int> candidates = new List<int>();
            if (s.Settings.forceRandomObject)
            {
                List<int> nonHostile = SettlementsByHostility(s, src.Faction, false);
                List<int> hostile = SettlementsByHostility(s, src.Faction, true);
                if (nonHostile.Count > 0)
                {
                    candidates.Add(rng.RandomElement(nonHostile));
                }
                if (hostile.Count > 0)
                {
                    candidates.Add(rng.RandomElement(hostile));
                }
            }
            else
            {
                // Port of WorldUtility.GetRimWorldSettlementsInRange.
                List<int> inRange = s.ObjectsInRange(self, targetRange, rng, WorldUtility.maxObjectsPerScan);
                for (int i = 0; i < inRange.Count; i++)
                {
                    if (s.Objects[inRange[i]].Kind == WoKind.Settlement)
                    {
                        candidates.Add(inRange[i]);
                    }
                }
            }
            if (candidates.Count == 0)
            {
                return;
            }

            int t = rng.RandomElement(candidates);
            WoRecord dst = s.Objects[t];
            if (!(s.ApproxDistance(self, t) <= targetRange))
            {
                return;
            }
            int pts = WorldUtility.CalculateDiplomatPoints(rng);
            RimWarBehavior behavior = s.Behavior(src.Faction);
            if (behavior == RimWarBehavior.Cautious)
            {
                pts = Mathf.RoundToInt(pts * 1.1f);
            }
            else if (behavior == RimWarBehavior.Warmonger)
            {
                pts = Mathf.RoundToInt(pts * .8f);
            }
            else if (behavior == RimWarBehavior.Merchant)
            {
                pts = Mathf.RoundToInt(pts * 1.3f);
            }
            if (!(src.Points * .5f >= pts))
            {
                return;
            }

            target = dst.Handle;
            targetFaction = dst.Faction >= 0 ? s.Factions[dst.Faction].Handle : null;
            points = pts;
        }

        protected internal override void Commit()
        {
            if (target == null || !SourceStillValid() || !TargetStillValid(target, targetFaction))
            {
                return;
            }
            if (!(rwsComp.RimWarPoints * .5f >= points))
            {
                return;
            }
            WorldUtility.CreateDiplomat(points, rwd, parentSettlement, parentSettlement.Tile, target, WorldObjectDefOf.Settlement);
            rwsComp.RimWarPoints -= points;
            if (target.Faction == Faction.OfPlayer)
            {
                WorldComponent_PowerTracker wcpt = Tracker;
                rwsComp.PlayerHeat = 0;
                wcpt.minimumHeatForPlayerAction += wcpt.GetHeatForAction(RimWarAction.Diplomat);
            }
        }
    }
}
