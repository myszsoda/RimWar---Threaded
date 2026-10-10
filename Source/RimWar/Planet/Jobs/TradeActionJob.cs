using System.Collections.Generic;
using RimWar.Threading;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    /// <summary>
    /// A settlement sends a trade caravan to a friendly settlement.
    /// Same rules as the removed WorldComponent_PowerTracker.AttemptTradeMission_UnThreaded.
    /// </summary>
    public sealed class TradeActionJob : SettlementActionJob
    {
        private WorldObject target;
        private Faction targetFaction;
        private int points;

        public TradeActionJob(RimWarData rwd, Settlement parentSettlement, RimWarSettlementComp rwsComp,
                              bool forcePlayer, bool ignoreRestrictions)
            : base(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions)
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

            List<int> candidates;
            if (s.Settings.forceRandomObject)
            {
                candidates = new List<int>();
                List<int> nonHostile = SettlementsByHostility(s, src.Faction, false);
                if (nonHostile.Count > 0)
                {
                    candidates.Add(rng.RandomElement(nonHostile));
                }
            }
            else if (forcePlayer)
            {
                candidates = PlayerSettlements(s);
            }
            else
            {
                candidates = NearbySettlements(s, self, false);
            }
            if (candidates.Count == 0)
            {
                return;
            }

            int t = rng.RandomElement(candidates);
            WoRecord dst = s.Objects[t];
            if (!(s.ApproxDistance(self, t) <= targetRange || ignoreRestrictions))
            {
                return;
            }
            if (s.IsPlayer(dst.Faction))
            {
                if (src.PlayerHeat < s.MinimumHeatForPlayerAction && !ignoreRestrictions)
                {
                    return;
                }
                if (!s.Factions[src.Faction].CanTrade && !ignoreRestrictions && !forcePlayer)
                {
                    return;
                }
            }

            // The original sizes the caravan from the sending settlement's own points.
            int pts = WorldUtility.CalculateTraderPoints(src.Points, s.IsPlayer(src.Faction), rng);
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
                pts = Mathf.RoundToInt(pts * 1.2f);
            }
            if (!(Mathf.RoundToInt(src.Points * .5f) >= pts || ignoreRestrictions))
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
            WorldComponent_PowerTracker wcpt = Tracker;
            if (target.Faction == Faction.OfPlayer && rwsComp.PlayerHeat < wcpt.minimumHeatForPlayerAction && !ignoreRestrictions)
            {
                return;
            }
            if (!(Mathf.RoundToInt(rwsComp.RimWarPoints * .5f) >= points || ignoreRestrictions))
            {
                return;
            }
            WorldUtility.CreateTrader(points, rwd, parentSettlement, parentSettlement.Tile, target, WorldObjectDefOf.Settlement);
            rwsComp.RimWarPoints -= points;
            if (target.Faction == Faction.OfPlayer)
            {
                rwsComp.PlayerHeat = 0;
                wcpt.minimumHeatForPlayerAction += wcpt.GetHeatForAction(RimWarAction.Caravan);
            }
        }
    }
}
