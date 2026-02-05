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
    public class BattleSite : RimWarSite
    {        

        public bool Attackable
        {
            get
            {
                return AreAnyUnitsHostileTo(Faction.OfPlayer);
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (nextCombatTick < Find.TickManager.TicksGame)
            {
                nextCombatTick = Find.TickManager.TicksGame + 2500; //every game hour
                //Log.Message("cycling battle site " + this.Tile);
                if (!base.HasMap)
                {
                    //Log.Message("evaluating battle site combat with " + Units.Count + " units");   
                    CheckAndCleanDuplicates();
                    bool anyCombatRemaining = false;
                    if (Units.Count > 1)
                    {
                        for (int i = 0; i < Units.Count; i++)
                        {
                            WarObject waro = Units[i];
                            if (waro.EffectivePoints > 0)
                            {
                                for (int j = i + 1; j < Units.Count; j++)
                                { 
                                    WarObject ward = Units[j];
                                    if (ward.EffectivePoints > 0 && waro.EffectivePoints > 0 && ward.Faction.HostileTo(waro.Faction))
                                    {
                                        IncidentUtility.ResolveCombat_Units(waro, ward);
                                        if (waro.EffectivePoints > 0 && ward.EffectivePoints > 0)
                                        {
                                            anyCombatRemaining = true;
                                        }
                                    }
                                }
                            }
                        }
                        if (!anyCombatRemaining)
                        {
                            //Log.Message("no combat remaining");
                            IncidentUtility.ResolveBattle_Units(this);
                        }
                    }
                    else
                    {
                        IncidentUtility.ResolveBattle_Units(this);
                        //spawn remaining units and destroy site
                        //spawn old battle site?
                    }
                }
                else
                {
                    IncidentUtility.UpdateUnitCombatStatus(this.Units);
                }
            }
        }

        private void CheckAndCleanDuplicates()
        {
            Restart:;
            for(int i = 0; i < this.Units.Count; i++)
            {
                WarObject first = this.Units[i];
                if(i < this.Units.Count  - 1)
                {
                    for(int j = i + 1; j < this.Units.Count; j++)
                    {
                        if(first.Faction == this.Units[j].Faction && first.RimWarPoints == this.Units[j].RimWarPoints)
                        {
                            //Log.Message("removing " + this.Units[j].Name);
                            this.Units.Remove(this.Units[j]);
                            goto Restart;
                        }
                    }
                }
            }
        }

        public string GetUnitsToString
        {
            get
            {
                string str = "";
                for (int i = 0; i < Units.Count; i++)
                {
                    if(str != "")
                    {
                        str += "\n";
                    }
                    str += Units[i].Label;
                }
                return str;
            }
        }

        public string GetUnitsWithPointsToString
        {
            get
            {
                string str = "";
                for (int i = 0; i < Units.Count; i++)
                {
                    if (str != "")
                    {
                        str += "\n";
                    }
                    str += Units[i].Label;
                    str += " " + Units[i].RimWarPoints + " (" + Units[i].PointDamage + ")";

                }
                return str;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
        }

        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            foreach(Gizmo g in base.GetCaravanGizmos(caravan))
            {
                yield return g;
            }
            if (!this.HasMap)
            {
				Command_Action command_Action = new Command_Action
				{
					icon = AttackCommand,
					defaultLabel = "RW_CommandAttackBattleSite".Translate(),
					defaultDesc = "RW_CommandAttackBattleSiteDesc".Translate(),
					action = delegate
						{
							IncidentUtility.AttackBattleSite(caravan, this);
						}
				};
				yield return (Gizmo)command_Action;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Caravan caravan)
        {
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(caravan))
            {
                yield return floatMenuOption;
            }
            if (!this.HasMap)
            {
                foreach (FloatMenuOption fmo in CaravanArrivalAction_JoinBattle.GetFloatMenuOptions(caravan, this))
                {
                    yield return fmo;
                }
            }
        }

        public override IEnumerable<FloatMenuOption> GetTransportersFloatMenuOptions(IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> representative)
        {
            foreach (FloatMenuOption transportPodsFloatMenuOption in base.GetTransportersFloatMenuOptions(pods, representative))
            {
                yield return transportPodsFloatMenuOption;
            }
            if (!base.HasMap)
            {
                foreach (FloatMenuOption floatMenuOption3 in TransportPodsArrivalAction_JoinBattle.GetFloatMenuOptions(representative, pods, this))
                {
                    yield return floatMenuOption3;
                }
            }
        }

        public override IEnumerable<FloatMenuOption> GetShuttleFloatMenuOptions(IEnumerable<IThingHolder> pods, Action<PlanetTile, TransportersArrivalAction> launchAction)
        {
            foreach (FloatMenuOption shuttleFloatMenuOption in base.GetShuttleFloatMenuOptions(pods, launchAction))
            {
                yield return shuttleFloatMenuOption;
            }
            if (!base.HasMap)
            {
                foreach (FloatMenuOption floatMenuOption in TransportersArrivalActionUtility.GetFloatMenuOptions(() => TransportPodsArrivalAction_JoinBattle.CanAttack(pods, this), () => new TransportPodsArrivalAction_Shuttle_JoinBattle(this, this), "AttackShuttle".Translate(Label), launchAction, base.Tile))
                {
                    yield return floatMenuOption;
                }
            }
        }
    }
}
