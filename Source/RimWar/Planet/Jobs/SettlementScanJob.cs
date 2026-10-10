using System.Collections.Generic;
using RimWar.Threading;
using RimWorld.Planet;

namespace RimWar.Planet
{
    /// <summary>
    /// Refreshes RimWarSettlementComp.OtherSettlementsInRange. Replaces the scan that the property getter used to run
    /// inline (or on a RocketTask under a Mutex).
    /// </summary>
    public sealed class SettlementScanJob : RimWarJob
    {
        private readonly RimWarSettlementComp comp;
        private readonly int settlementId;

        private List<WorldObject> found;

        public SettlementScanJob(RimWarSettlementComp comp)
        {
            MainThread.Verify();
            this.comp = comp;
            this.settlementId = comp.parent.ID;
        }

        protected internal override void Compute(WorldSnapshot s, System.Random rng)
        {
            int self;
            if (!s.TryGetIndex(settlementId, out self))
            {
                return;
            }
            int range = RimWarSettlementComp.ScanRangeFor(s.Objects[self].Points, s.Settings.settlementScanRangeDivider, s.Settings.maxSettelementScanRange);
            // Same sampling as WorldUtility.GetRimWorldSettlementsInRange: at most maxObjectsPerScan random objects in range,
            // of which the settlements are kept.
            List<int> inRange = s.ObjectsInRange(self, range, rng, WorldUtility.maxObjectsPerScan);
            found = new List<WorldObject>();
            for (int i = 0; i < inRange.Count; i++)
            {
                int index = inRange[i];
                if (index != self && s.Objects[index].Kind == WoKind.Settlement)
                {
                    found.Add(s.Objects[index].Handle);
                }
            }
        }

        protected internal override void Commit()
        {
            if (found == null || comp.parent == null || comp.parent.Destroyed)
            {
                return;
            }
            List<Settlement> settlements = new List<Settlement>(found.Count);
            for (int i = 0; i < found.Count; i++)
            {
                Settlement settlement = found[i] as Settlement;
                if (settlement != null && !settlement.Destroyed)
                {
                    settlements.Add(settlement);
                }
            }
            comp.ApplySettlementScan(settlements);
        }
    }
}
