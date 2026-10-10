using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using HarmonyLib;
using RimWar;
using RimWorld;
using RimWorld.Planet;

namespace RimWar.Planet
{
    public class RimWarCaravanComp : WorldObjectComp
    {
        public float scanRange = 2f;
        public WarObject currentTarget = null;

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
            if (Find.TickManager.TicksGame % 121 == 0)
            {
                EngageCaravanTarget(FindCaravanTargetInRange(scanRange));
            }
        }

        // Game thread. Only caravans the player ordered to engage a war object (caravanTargetData) have anything to find,
        // so check that list before scanning the world.
        public WorldObject FindCaravanTargetInRange(float range)
        {
            List<CaravanTargetData> ctd = WorldUtility.Get_WCPT().GetCaravaTargetData;
            if (ctd == null || ctd.Count == 0 || !ctd.Any(x => x.caravan == this.parent))
            {
                return null;
            }
            WorldObject destinationTarget = null;
            List<WorldObject> worldObjects = WorldUtility.GetWorldObjectsInRange(this.parent.Tile, range);
            if (worldObjects != null && worldObjects.Count > 0)
            {
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    WorldObject wo = worldObjects[i];
                    if (wo != null && !wo.Destroyed && wo.Faction != this.parent.Faction)
                    {
                        for (int j = 0; j < ctd.Count; j++)
                        {
                            if (ctd[j].caravan == this.parent && ctd[j].caravanTarget == wo)
                            {
                                destinationTarget = wo;
                            }
                        }
                    }
                }
            }
            return destinationTarget;
        }

        public void EngageCaravanTarget(WorldObject destinationTarget)
        {
            WarObject wo = destinationTarget as WarObject;
            if (wo != null)
            {
                wo.EngageCaravan(this.parent as Caravan);
            }
        }
    }
}
