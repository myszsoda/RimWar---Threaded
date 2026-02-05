using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

using UnityEngine;

namespace RimWar
{
    public struct ConsolidatePoints : IExposable
    {
        public int points, delay;

        public ConsolidatePoints(int pts, int dly)
        {
            Mathf.Clamp(pts, 0, pts);
            Mathf.Clamp(dly, 0, dly);
            points = pts;
            delay = dly;
        }

        public void ExposeData()
        {
            Scribe_Values.Look<int>(ref this.points, "points", 0, false);
            Scribe_Values.Look<int>(ref this.delay, "delay", 0, false);
        }
    }
}
