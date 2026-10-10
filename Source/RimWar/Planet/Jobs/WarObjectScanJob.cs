using System.Collections.Generic;
using RimWar.Threading;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    /// <summary>
    /// A war object looks around for something to engage: a caravan, a battle site to join, or another war object.
    /// Port of WarObject.ScanAction and Scout.ScanAction (scouts first re-target onto weaker nearby prey, then scan at range 1).
    ///
    /// Commit() re-checks everything against live state - both parties alive and spawned, still in range, still different
    /// factions - so two units that pick each other, or a unit and a battle site that resolves first, cannot double-apply.
    /// </summary>
    public sealed class WarObjectScanJob : RimWarJob
    {
        private readonly WarObject warObject;
        private readonly int selfId;
        private readonly int destinationId;
        private readonly float scanRange;
        private readonly float detectionModifier;
        private readonly bool scoutRules;

        // Result of Compute().
        private WorldObject newDestination;
        private WorldObject interactTarget;
        private WoKind interactKind;

        public WarObjectScanJob(WarObject warObject)
        {
            MainThread.Verify();
            this.warObject = warObject;
            this.selfId = warObject.ID;
            WorldObject destination = warObject.DestinationTarget;
            this.destinationId = destination != null ? destination.ID : -1;
            this.scanRange = warObject.ScanRange;
            this.detectionModifier = warObject.DetectionModifier;
            this.scoutRules = warObject is Scout;
        }

        private float InteractionRange => scoutRules ? 1f : scanRange;

        protected internal override void Compute(WorldSnapshot s, System.Random rng)
        {
            int self;
            if (!s.TryGetIndex(selfId, out self))
            {
                return;
            }
            int destination;
            if (destinationId < 0 || !s.TryGetIndex(destinationId, out destination))
            {
                destination = -1;
            }
            if (scoutRules)
            {
                int retarget = ComputeScoutRetarget(s, rng, self, destination);
                if (retarget != destination && retarget >= 0)
                {
                    destination = retarget;
                    newDestination = s.Objects[retarget].Handle;
                }
            }
            ComputeInteraction(s, rng, self, destination);
        }

        // Port of Scout.ScanAction (before its base.ScanAction(1f) call).
        private int ComputeScoutRetarget(WorldSnapshot s, System.Random rng, int self, int destination)
        {
            WoRecord me = s.Objects[self];
            List<int> candidates = s.ObjectsInRange(self, scanRange, rng, WorldUtility.maxObjectsPerScan);
            for (int i = 0; i < candidates.Count; i++)
            {
                int c = candidates[i];
                WoRecord rec = s.Objects[c];
                if (rec.Faction == me.Faction || c == destination)
                {
                    continue;
                }
                if (destination >= 0 && !(s.ApproxDistance(c, self) < s.ApproxDistance(destination, self)))
                {
                    continue;
                }
                if (rec.Kind == WoKind.Caravan && me.Interactable)
                {
                    if (CaravanDetected(s, rng, self, c) && (rec.CaravanWealth / 100) <= me.Points && s.Hostile(me.Faction, rec.Faction))
                    {
                        destination = c;
                        break;
                    }
                }
                if (rec.Kind == WoKind.BattleSite)
                {
                    for (int u = 0; u < rec.SiteUnitCount; u++)
                    {
                        if (s.SiteUnits[rec.FirstSiteUnit + u].Faction == rec.Faction)
                        {
                            destination = c;
                            break;
                        }
                    }
                }
                if (rec.Kind == WoKind.WarObject && s.Hostile(me.Faction, rec.Faction))
                {
                    if (rec.Points <= me.Points)
                    {
                        destination = c;
                        break;
                    }
                }
            }
            return destination;
        }

        // Port of WarObject.CaravanDetected.
        private bool CaravanDetected(WorldSnapshot s, System.Random rng, int self, int caravan)
        {
            WoRecord car = s.Objects[caravan];
            float vis = car.CaravanVisibility * (1f / Mathf.Max(1f, s.ApproxDistance(caravan, self)));
            float relativePower = (car.CaravanWealth / 80f) / (float)s.Objects[self].Points;
            return rng.Chance(vis * detectionModifier * relativePower);
        }

        // Port of WarObject.ScanAction.
        private void ComputeInteraction(WorldSnapshot s, System.Random rng, int self, int destination)
        {
            WoRecord me = s.Objects[self];
            if (!me.Interactable)
            {
                return;
            }
            List<int> candidates = s.ObjectsInRange(self, InteractionRange, rng, WorldUtility.maxObjectsPerScan);
            for (int i = 0; i < candidates.Count; i++)
            {
                int c = candidates[i];
                WoRecord rec = s.Objects[c];
                if (rec.Faction == me.Faction || c == destination)
                {
                    continue;
                }
                if (rec.Kind == WoKind.Caravan)
                {
                    if (rec.HasRimWarComp && rec.CaravanTargetId != selfId)
                    {
                        interactTarget = rec.Handle;
                        interactKind = rec.Kind;
                    }
                    return;
                }
                if (rec.Kind == WoKind.BattleSite || rec.Kind == WoKind.RimWarSite || rec.Kind == WoKind.WarObject)
                {
                    interactTarget = rec.Handle;
                    interactKind = rec.Kind;
                    return;
                }
            }
        }

        protected internal override void Commit()
        {
            if (warObject == null || warObject.Destroyed || !warObject.Spawned)
            {
                return;
            }
            if (newDestination != null && !newDestination.Destroyed && newDestination.Spawned)
            {
                warObject.DestinationTarget = newDestination;
            }
            WorldObject target = interactTarget;
            if (target == null || target.Destroyed || !target.Spawned || !warObject.interactable)
            {
                return;
            }
            if (target.Faction == warObject.Faction || target == warObject.DestinationTarget)
            {
                return;
            }
            if (target.Tile != warObject.Tile && Find.WorldGrid.ApproxDistanceInTiles(warObject.Tile, target.Tile) > InteractionRange)
            {
                return;
            }
            if (interactKind == WoKind.Caravan)
            {
                Caravan car = target as Caravan;
                RimWarCaravanComp rwcc = car?.GetComponent<RimWarCaravanComp>();
                if (rwcc != null && rwcc.currentTarget != warObject)
                {
                    warObject.EngageNearbyCaravan(car);
                }
            }
            else if (interactKind == WoKind.WarObject)
            {
                WarObject other = target as WarObject;
                if (other != null)
                {
                    warObject.EngageNearbyWarObject(other);
                }
            }
            else
            {
                warObject.InteractWithSite(target);
            }
        }
    }
}
