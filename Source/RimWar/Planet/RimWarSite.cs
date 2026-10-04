using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimWar.Planet
{
    [StaticConstructorOnStartup]
    public class RimWarSite : MapParent
    {
        private List<WarObject> units;
        public int nextCombatTick = 0;
        public static readonly Texture2D AttackCommand = ContentFinder<Texture2D>.Get("UI/Commands/AttackSettlement");

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look<WarObject>(ref this.units, "units", LookMode.Deep, new object[0]);
            Scribe_Values.Look<int>(ref this.nextCombatTick, "nextCombatTick", 0, false);
        }

        public List<WarObject> Units
        {
            get
            {
                if (units == null)
                {
                    units = new List<WarObject>();
                    units.Clear();
                }
                return units;
            }
            set
            {
                if (units == null)
                {
                    units = new List<WarObject>();
                    units.Clear();
                }
                units = value;
            }
        }

        public bool UnderAttack
        {
            get
            {
                if (units != null)
                {
                    return units.Count > 0;
                }
                return false;
            }
        }

        public bool AnyCombatRemaining
        {
            get
            {                
                for(int i = 0; i < Units.Count; i++)
                {
                    if (Units[i].EffectivePoints <= 0)
                    {
                        continue;
                    }
                    for(int j = i + 1; j < Units.Count; j++)
                    {
                        if(Units[j].EffectivePoints > 0 && Units[j].Faction.HostileTo(Units[i].Faction))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public bool AreAnyUnitsHostileTo(Faction f)
        {
            IEnumerable<WarObject> waros = from waro in Units
                                           where waro.Faction != null && waro.Faction.HostileTo(f)
                                           select waro;

            return waros.Any();
                                           
        }

        public bool AreAnyUnitsHostile(List<WarObject> unitList)
        {
            for(int i = 1; i < unitList.Count; i++)
            {
                if(unitList[0].Faction.HostileTo(unitList[i].Faction))
                {
                    return true;
                }
            }
            return false;
        }

        public bool PlayerOnMap
        {
            get
            {
                if(!base.HasMap)
                {
                    return false;
                }
                if(base.Map != null)
                {
                    foreach(Pawn p in base.Map.mapPawns.AllPawns)
                    {
                        if(p.IsColonist && !p.Dead)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
        {
            alsoRemoveWorldObject = false;
            if (!base.Map.IsPlayerHome)
            {
                return !base.Map.mapPawns.AnyPawnBlockingMapRemoval;
            }
            return false;
        }

        protected override void Tick()
        {
            base.Tick();
        }

        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(this.Label);
            //stringBuilder.Append(" " + this.Tile);
            foreach (WarObject waro in Units)
            {
                stringBuilder.Append("\n" + waro.Label + " " + waro.RimWarPoints + " (" + waro.PointDamage + ")");
            }            
            return stringBuilder.ToString();
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (Prefs.DevMode)
            {
                List<Gizmo> gizmoIE = base.GetGizmos().ToList();
                Command_Action command_Action1 = new Command_Action();
                command_Action1.defaultLabel = "Dev: Destroy";
                command_Action1.defaultDesc = "Destroys the site and all contained objects.";
                command_Action1.action = delegate
                {
                    Destroy();
                };
                yield return (Gizmo)command_Action1;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            return base.GetFloatMenuOptions(caravan);
        }

        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(IEnumerable<IThingHolder> pods,Action<PlanetTile, TransportersArrivalAction> representative)
        {
            return base.GetTransportersFloatMenuOptions(pods, representative);
        }

        public override IEnumerable<FloatMenuOption> GetShuttleFloatMenuOptions(IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
        {
            return base.GetShuttleFloatMenuOptions(pods, launchAction);
        }
    }

}
