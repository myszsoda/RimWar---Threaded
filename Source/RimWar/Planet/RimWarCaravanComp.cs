using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using HarmonyLib;
using RimWar;
using RimWorld;
using RimWorld.Planet;
using RimWar.RocketTools;
using RimWar.Options;
using System.Diagnostics;
using System.Threading;

namespace RimWar.Planet
{
    public class RimWarCaravanComp : WorldObjectComp
    {
        public float scanRange = 2f;
        private bool initialized = false;
        public WarObject currentTarget = null;

        public class ContextStorage
        {
            internal bool shouldExecute;
            internal WorldObject destinationTarget;
            internal int destinationTile;
        }

        public static RocketTasker<ContextStorage> tasker = new RocketTasker<ContextStorage>();
        public static int MainthreadID = -1;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look<WarObject>(ref currentTarget, "currentTarget", false);
        }

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
        }

        public override void CompTick()
        {
            base.CompTick();
            if(!initialized)
            {
                MainthreadID = Thread.CurrentThread.ManagedThreadId;
            }
            if(Find.TickManager.TicksGame % 121 == 0)
            {
                if (Options.Settings.Instance.threadingEnabled)
                {
                    tasker.Register((Func<ContextStorage>)(() =>
                    {
                        var context = new ContextStorage();
                        ScanAction(scanRange, context);
                        return context;
                    }),
                    (Action<ContextStorage>)((context) =>
                    {
                        EngageCaravanTarget(context);
                    }));
                }
                else
                {
                    var context = new ContextStorage();
                    ScanAction(scanRange, context);
                    EngageCaravanTarget(context);
                }
            }
            if (Options.Settings.Instance.threadingEnabled)
            {
                tasker.Tick();
            }
        }

        public void ScanAction(float range, ContextStorage context)
        {
            List<WorldObject> worldObjects = WorldUtility.GetWorldObjectsInRange(this.parent.Tile, range);
            List<CaravanTargetData> ctd = WorldUtility.Get_WCPT().GetCaravaTargetData;
            context.shouldExecute = false;
            //Log.Message("scanning");
            //if(ctd != null)
            //{
            //    Log.Message("ctd count " + ctd.Count);
            //    for(int i = 0; i<ctd.Count; i++)
            //    {
            //        Log.Message("ctd " + i + " target " + ctd[i].caravanTarget.Name);
            //    }
            //}
            if (worldObjects != null && worldObjects.Count > 0 && ctd != null && ctd.Count > 0)
            {
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    WorldObject wo = worldObjects[i];                    
                    if (wo != null && !wo.Destroyed && wo.Faction != this.parent.Faction)
                    {
                        for(int j = 0; j < ctd.Count; j++)
                        {
                            if(ctd[j].caravan == this.parent && ctd[j].caravanTarget == wo)
                            {
                                context.destinationTarget = wo;
                                context.shouldExecute = true;                                
                            }
                        }
                    }
                }
            }
        }

        public void EngageCaravanTarget(ContextStorage context)
        {
            if(context.shouldExecute && context.destinationTarget != null)
            {
                WarObject wo = context.destinationTarget as WarObject;
                if(wo != null)
                {
                    wo.EngageCaravan(this.parent as Caravan);
                }
            }
        }
    }
}
