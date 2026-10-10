using System.Collections.Generic;
using RimWar.Threading;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    /// <summary>
    /// A settlement sends a warband (on foot, or launched by transport pods) against a hostile settlement.
    /// Same rules as the removed WorldComponent_PowerTracker.AttemptWarbandActionAgainstTown_UnThreaded and
    /// AttemptLaunchedWarbandAgainstTown_UnThreaded, which differ only in the numbers below.
    /// </summary>
    public sealed class WarbandActionJob : SettlementActionJob
    {
        private readonly bool launched;

        // Result of Compute().
        private WorldObject target;
        private Faction targetFaction;
        private int points;
        private bool atWarWithTarget;

        public WarbandActionJob(RimWarData rwd, Settlement parentSettlement, RimWarSettlementComp rwsComp,
                                bool forcePlayer, bool ignoreRestrictions, bool launched)
            : base(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions)
        {
            this.launched = launched;
        }

        private float RangeMultiplier => launched ? 2f : 1f;
        private float WarmongerMultiplier => launched ? 1.2f : 1.25f;
        private float AtWarMultiplier => launched ? 1.1f : 1.2f;
        private float AtWarCommitment => launched ? .8f : .85f;
        private float Commitment => launched ? .6f : .75f;
        private RimWarAction Action => launched ? RimWarAction.LaunchedWarband : RimWarAction.Warband;

        protected internal override void Compute(WorldSnapshot s, System.Random rng)
        {
            int self = Self(s);
            if (self < 0)
            {
                return;
            }
            WoRecord src = s.Objects[self];
            int targetRange = Mathf.RoundToInt(ScanRange(s, self) * RangeMultiplier);

            List<int> candidates;
            if (s.Settings.forceRandomObject)
            {
                candidates = SettlementsByHostility(s, src.Faction, true);
            }
            else if (forcePlayer)
            {
                candidates = PlayerSettlements(s);
            }
            else
            {
                candidates = NearbySettlements(s, self, true);
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
            if (BlockedByPlayerGates(s, self, t, ignoreRestrictions))
            {
                return;
            }
            if (s.IsPlayer(dst.Faction) && !s.Factions[src.Faction].CanFight200 && !forcePlayer)
            {
                return;
            }

            bool targetIsPlayer = dst.HasRimWarComp && s.IsPlayer(dst.Faction);
            int pts = WorldUtility.CalculateWarbandPointsForRaid(dst.HasRimWarComp ? dst.Points : 0, targetIsPlayer, rng);
            RimWarBehavior behavior = s.Behavior(src.Faction);
            if (behavior == RimWarBehavior.Cautious)
            {
                pts = Mathf.RoundToInt(pts * 1.1f);
            }
            else if (behavior == RimWarBehavior.Warmonger)
            {
                pts = Mathf.RoundToInt(pts * WarmongerMultiplier);
            }

            bool atWar = s.AtWar(src.Faction, dst.Faction);
            if (atWar)
            {
                pts = Mathf.RoundToInt(pts * AtWarMultiplier);
            }
            if (!(src.Points * (atWar ? AtWarCommitment : Commitment) >= pts || ignoreRestrictions))
            {
                return;
            }

            target = dst.Handle;
            targetFaction = dst.Faction >= 0 ? s.Factions[dst.Faction].Handle : null;
            points = pts;
            atWarWithTarget = atWar;
        }

        protected internal override void Commit()
        {
            if (target == null || !SourceStillValid() || !TargetStillValid(target, targetFaction))
            {
                return;
            }
            if (BlockedByPlayerGatesLive(target))
            {
                return;
            }
            if (!(rwsComp.RimWarPoints * (atWarWithTarget ? AtWarCommitment : Commitment) >= points || ignoreRestrictions))
            {
                return;
            }
            if (launched)
            {
                WorldUtility.CreateLaunchedWarband(points, rwd, parentSettlement, parentSettlement.Tile, target, WorldObjectDefOf.Settlement);
            }
            else
            {
                WorldUtility.CreateWarband(points, rwd, parentSettlement, parentSettlement.Tile, target, WorldObjectDefOf.Settlement);
            }
            rwsComp.RimWarPoints -= points;
            ApplyHostileActionHeat(target, Action);
        }
    }
}
