using System.Collections.Generic;
using RimWar.Threading;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    /// <summary>
    /// A settlement sends settlers to found a new settlement nearby.
    /// Same rules as the removed WorldComponent_PowerTracker.AttemptSettler_UnThreaded.
    ///
    /// The candidate tiles come from TileFinder, which uses the world flood filler and Verse.Rand (game thread only),
    /// so they are found in the constructor; Compute() only checks them against the snapshot.
    /// </summary>
    public sealed class SettlerActionJob : SettlementActionJob
    {
        // Radius in which an existing settlement blocks a candidate tile.
        private const float NearbySettlementRange = 10f;

        private readonly List<TilePoint> candidateTiles = new List<TilePoint>();
        private readonly int targetRange;

        private bool found;
        private PlanetTile destinationTile;
        private int points;

        public SettlerActionJob(RimWarData rwd, Settlement parentSettlement, RimWarSettlementComp rwsComp, bool ignoreRestrictions)
            : base(rwd, parentSettlement, rwsComp, false, ignoreRestrictions)
        {
            if (!(rwsComp.RimWarPoints > 500 || ignoreRestrictions))
            {
                return;
            }
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            targetRange = Mathf.Clamp(rwsComp.SettlementScanRange, 11, Mathf.Max((int)settingsRef.maxSettelementScanRange, 12));
            if (rwd.behavior == RimWarBehavior.Expansionist)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.2f);
            }
            else if (rwd.behavior == RimWarBehavior.Warmonger)
            {
                targetRange = Mathf.RoundToInt(targetRange * .8f);
            }
            for (int i = 0; i < 5; i++)
            {
                PlanetTile tile;
                TileFinder.TryFindPassableTileWithTraversalDistance(parentSettlement.Tile, 10, targetRange, out tile);
                if (tile.Valid)
                {
                    Tile t = Find.WorldGrid[tile];
                    if (t.PrimaryBiome != null && !t.PrimaryBiome.isExtremeBiome && t.PrimaryBiome.canBuildBase)
                    {
                        candidateTiles.Add(TilePoint.Of(tile));
                    }
                }
            }
        }

        protected internal override void Compute(WorldSnapshot s, System.Random rng)
        {
            int self = Self(s);
            if (self < 0 || candidateTiles.Count == 0)
            {
                return;
            }
            for (int i = 0; i < candidateTiles.Count; i++)
            {
                TilePoint tile = candidateTiles[i];
                if (tile.TileId <= 0 || !(s.ApproxDistance(tile, self) <= targetRange || ignoreRestrictions))
                {
                    continue;
                }
                bool nearbySettlement = false;
                List<int> nearby = s.ObjectsInRange(tile, NearbySettlementRange, rng, WorldUtility.maxObjectsPerScan);
                for (int j = 0; j < nearby.Count; j++)
                {
                    if (s.Objects[nearby[j]].DefIsVanillaSettlement)
                    {
                        nearbySettlement = true;
                    }
                }
                if (!nearbySettlement || ignoreRestrictions)
                {
                    found = true;
                    destinationTile = tile.Tile;
                    points = Mathf.RoundToInt(rng.Range(.4f, .6f) * 500);
                    return;
                }
            }
        }

        protected internal override void Commit()
        {
            if (!found || !SourceStillValid())
            {
                return;
            }
            if (!(rwsComp.RimWarPoints > 500 || ignoreRestrictions))
            {
                return;
            }
            WorldUtility.CreateSettler(points, rwd, parentSettlement, parentSettlement.Tile, destinationTile, null);
            rwsComp.RimWarPoints -= points;
        }
    }
}
