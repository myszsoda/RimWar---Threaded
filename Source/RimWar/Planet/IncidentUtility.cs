using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using Verse.AI.Group;
using RimWar.History;
using RimWar.Utility;

namespace RimWar.Planet
{
    public class IncidentUtility
    {
        public IncidentParms parms = new IncidentParms();

        public static void ResolveWorldEngagement(WarObject warObject, WorldObject wo)
        {
            if (warObject != null && wo != null)
            {
                if (wo.Faction != null && wo.Faction.HostileTo(warObject.Faction))
                {
                    if (wo is Caravan)
                    {
                        DoCaravanAttackWithPoints(warObject, wo as Caravan, warObject.rimwarData, PawnsArrivalModeOrRandom(PawnsArrivalModeDefOf.EdgeWalkIn));
                    }
                    else if (wo is WarObject)
                    {
                        ResolveRimWarBattle(warObject, wo as WarObject);
                    }
                }
                else
                {
                    if(wo is Caravan)
                    {
                        if(warObject is Trader)
                        {
                            Trader trader = warObject as Trader;
                            if(!trader.tradedWithTrader) //!trader.TradedWith.Contains(wo))
                            {
                                //attempt to trade with player
                                DoCaravanTradeWithPoints(warObject, wo as Caravan, warObject.rimwarData, PawnsArrivalModeOrRandom(PawnsArrivalModeDefOf.EdgeWalkIn));
                            }
                        }
                        else if(warObject is Diplomat)
                        {
                            DoPeaceTalks_Caravan(warObject, wo as Caravan, warObject.rimwarData, PawnsArrivalModeOrRandom(PawnsArrivalModeDefOf.EdgeWalkIn));
                        }
                        else
                        {
                            //do nothing
                        }
                    }
                }
            }
        }

        public static void ResolveRimWarBattle(WarObject attacker, WarObject defender)
        {
            List<WorldObject> wos = WorldUtility.GetAllWorldObjectsAtExcept(defender.Tile, attacker);
            if (ValidateRimWarAction(attacker, defender, wos))
            {
                bool transitionToSettlement = false;
                RimWarSettlementComp rwsc = null;
                for(int i = 0; i < wos.Count; i++)
                {
                    WorldObject wo = wos[i];
                    if(wo is Settlement)
                    {
                        rwsc = wo.GetComponent<RimWarSettlementComp>();
                        if(rwsc != null && !rwsc.parent.Destroyed)
                        {
                            transitionToSettlement = true;
                        }
                    }
                }
                wos.Add(attacker);
                List<WarObject> waros = new List<WarObject>();
                waros.Clear();
                foreach(WorldObject wo in wos)
                {
                    if(wo is WarObject)
                    {
                        waros.Add(wo as WarObject);
                    }
                }
                if (transitionToSettlement)
                {
                    if (rwsc.parent.Faction == Faction.OfPlayer)
                    {
                        foreach (WarObject wo in waros)
                        {
                            DoRaidWithPoints(wo, rwsc.parent as Settlement, wo.rimwarData, PawnsArrivalModeDefOf.EdgeWalkIn);
                        }
                    }
                    else
                    { 
                        rwsc.AttackingUnits.AddRange(waros);
                        rwsc.nextCombatTick = Find.TickManager.TicksGame + 2500;
                    }
                }
                else
                {
                    IncidentUtility.CreateNewBattleSite(defender.Tile, waros);
                }
                foreach(WarObject waro in waros)
                {
                    if (!waro.Destroyed)
                    {
                        waro.Destroy();
                    }
                    if (Find.WorldObjects.Contains(waro))
                    {
                        Find.WorldObjects.Remove(waro);
                    }
                }

                /*
                float combinedPoints = attacker.RimWarPoints + defender.RimWarPoints;
                float attackerRoll = Rand.Value;
                float defenderRoll = Rand.Value;
                float attackerResult = attackerRoll * (attacker.RimWarPoints - attacker.PointDamage) * attacker.rimwarData.combatAttribute;
                float defenderResult = defenderRoll * (defender.RimWarPoints - defender.PointDamage) * defender.rimwarData.combatAttribute;
                float endPointsAttacker = 0f;
                float endPointsDefender = 0f;
                RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_NeutralEvent);
                let.Label = "RW_LetterBattle".Translate();
                float attackerPointDamage = 0f;
                float defenderPointDamage = 0f;
                if (attackerResult > defenderResult)
                {
                    //Log.Message("attacker " + attacker.Label + " wins agaisnt warband " + defender.Label);
                    endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                    attackerPointDamage = endPointsAttacker;
                    endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                    defenderPointDamage = endPointsDefender;
                                                                                                                                           //Attacker wins, defender is destroyed
                    if (attackerResult > 2 * defenderResult) //routed
                    {
                        endPointsAttacker += endPointsDefender * (Rand.Range(.25f, .4f)); //gain points of the defender warband in combat power
                    }
                    else if (attackerResult > 1.5f * defenderResult) //solid win
                    {
                        endPointsAttacker += endPointsDefender * (Rand.Range(.15f, .3f));
                        if (defender.WarSettlementComp != null)
                        {
                            ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.3f, .5f) * endPointsDefender, defender.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(defender.Tile, defender.ParentSettlement.Tile) * defender.TicksPerMove) + Find.TickManager.TicksGame);
                            defender.WarSettlementComp.SettlementPointGains.Add(reconstitute);
                        }
                    }
                    else
                    {
                        endPointsAttacker += endPointsDefender * Rand.Range(.1f, .2f);
                        if (defender.WarSettlementComp != null)
                        {
                            ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.45f, .6f) * endPointsDefender, defender.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(defender.Tile, defender.ParentSettlement.Tile) * defender.TicksPerMove) + Find.TickManager.TicksGame);
                            defender.WarSettlementComp.SettlementPointGains.Add(reconstitute);
                        }
                    }
                    let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.Label, defender.RimWarPoints);
                    WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, 2 * attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, Mathf.RoundToInt(attackerPointDamage));
                    let.lookTargets = attacker;
                    let.relatedFaction = attacker.Faction;
                }
                else
                {
                    //Log.Message("defender " + defender.Label + " wins against warband " + defender.Label);
                    //Defender wins
                    endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                    attackerPointDamage = endPointsAttacker;
                    endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                    defenderPointDamage = endPointsDefender;

                    if (defenderResult > 2 * attackerResult) //routed
                    {
                        endPointsDefender += endPointsAttacker * (Rand.Range(.25f, .4f)); //gain up to half the points of the defender warband in combat power
                    }
                    else if (attackerResult > 1.5f * defenderResult) //solid win
                    {
                        endPointsDefender += endPointsAttacker * (Rand.Range(.15f, .3f));
                        if (attacker.WarSettlementComp != null)
                        {
                            ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.3f, .5f) * endPointsAttacker, attacker.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(attacker.Tile, attacker.ParentSettlement.Tile) * attacker.TicksPerMove) + Find.TickManager.TicksGame);
                            attacker.WarSettlementComp.SettlementPointGains.Add(reconstitute);
                        }
                    }
                    else
                    {
                        endPointsDefender += endPointsAttacker * Rand.Range(.1f, .2f);
                        if (defender.WarSettlementComp != null)
                        {
                            ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min(Rand.Range(.45f, .6f) * endPointsAttacker, attacker.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(attacker.Tile, attacker.ParentSettlement.Tile) * attacker.TicksPerMove) + Find.TickManager.TicksGame);
                            attacker.WarSettlementComp.SettlementPointGains.Add(reconstitute);
                        }
                    }
                    let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "was defeated by", defender.Label, defender.RimWarPoints);
                    WorldUtility.CreateWarObjectOfType(defender, Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 50, 2 * defender.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(defender.Faction), defender.ParentSettlement, defender.Tile, defender.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, Mathf.RoundToInt(defenderPointDamage));
                    let.lookTargets = defender;
                    let.relatedFaction = defender.Faction;
                }
                RW_LetterMaker.Archive_RWLetter(let);
                defender.Faction.TryAffectGoodwillWith(attacker.Faction, -2, true, true, null, null);
                attacker.Faction.TryAffectGoodwillWith(defender.Faction, -2, true, true, null, null);
                defender.Destroy(); //force removal of the non-initiating warband
                */
            }
        }

        public static void ResolveWarObjectAttackOnSettlement(WarObject attacker, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp defender, RimWarData rwd)
        {
            //Log.Message("resolving war object attack on settlement for " + attacker.Name + " against " + defender.parent.Label);
            if (ValidateRimWarAction(attacker, defender, WorldUtility.GetAllWorldObjectsAtExcept(attacker.Tile, attacker)))
            {

                    defender.AttackingUnits.Add(attacker);
                    defender.nextCombatTick = Find.TickManager.TicksGame + 2500;
                    /*
                    float combinedPoints = attacker.RimWarPoints + defender.RimWarPoints;
                    float attackerRoll = Rand.Value;
                    float defenderRoll = Rand.Value;
                    float attackerResult = attackerRoll * (attacker.RimWarPoints - attacker.PointDamage) * attacker.rimwarData.combatAttribute;
                    float defenderResult = defenderRoll * (defender.RimWarPoints - defender.PointDamage) * defender.RWD.combatAttribute;
                    if (defender.isCapitol)
                    {
                        defenderResult *= 1.15f;
                    }
                    float endPointsAttacker = 0f;
                    float endPointsDefender = 0f;

                    RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_NeutralEvent);
                    let.Label = "RW_LetterSettlementBattle".Translate();

                    //determine attacker/defender win
                    //if attacker wins ->
                    // determine points assigned to attacker (routed or solid can capture, routed can raze (city loses additional pts), solid or win can weaken)
                    // determine if the city is destroyed (less than x points always or chance when routed)
                    // determine if the city is captured (no additional points to attacker but settlement faction change) or remains (razed (loses more points) or weakened)
                    //if defender wins ->
                    // determine points assigned to defender (routed can capture the warband (lots of points, nothing returned to attacker)
                    // determine points to assign to attacker and defender and send attack back to parent settlement

                    endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                    int attackerPointDamage = Mathf.RoundToInt(endPointsAttacker);
                    endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
                    int defenderPointDamage = Mathf.RoundToInt(endPointsDefender);

                    if (attackerResult > defenderResult)
                    {
                        //Log.Message("attacker " + attacker.Label + " wins against settlement " + defender.RimWorld_Settlement.Name);
                                                                                                                                               //Attacker wins
                        if (attackerResult > 1.75 * defenderResult) //routed
                        {
                            float rndCapture = Rand.Value;
                            if (attacker.rimwarData?.behavior == RimWarBehavior.Expansionist)
                            {
                                rndCapture *= 1.1f;
                            }
                            else if (attacker.rimwarData?.behavior == RimWarBehavior.Warmonger)
                            {
                                rndCapture *= 1.5f;
                            }

                            if (rndCapture >= .35f && attacker.rimwarData?.behavior != RimWarBehavior.Vassal)
                            {
                                //Log.Message("attacker is capturing " + defender.RimWorld_Settlement.Name);
                                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "captured", defender.parent?.Label, defender.RimWarPoints);
                                let.lookTargets = attacker;
                                let.relatedFaction = attacker.Faction;
                                WorldUtility.ConvertSettlement(Find.WorldObjects.SettlementAt(defender.parent.Tile), WorldUtility.GetRimWarDataForFaction(defender.parent.Faction), WorldUtility.GetRimWarDataForFaction(attacker.Faction), Mathf.RoundToInt(Mathf.Max(endPointsDefender, 0)), defenderPointDamage);
                                //RimWorld.Planet.Settlement rws = Find.World.worldObjects.SettlementAt(defender.Tile);
                                //rws.SetFaction(attacker.Faction);
                                //WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                                //defender.Faction = attacker.Faction;
                                //WorldUtility.GetRimWarDataForFaction(attacker.Faction).FactionSettlements.Add(defender);
                                //Find.World.WorldUpdate();                        
                            }
                            else
                            {
                                float pointsAdjustment = endPointsDefender * (Rand.Range(.35f, .5f));
                                endPointsAttacker += pointsAdjustment;
                                endPointsDefender -= pointsAdjustment;
                                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.parent.Label, defender.RimWarPoints);
                                let.lookTargets = attacker;
                                let.relatedFaction = defender.parent.Faction;
                                if (endPointsDefender <= 1000)
                                {
                                    let.Text += "\nThe pathetic hamlet was burned to the ground.";
                                    //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                                    Find.WorldObjects.SettlementAt(defender.parent.Tile)?.Destroy();
                                    if (WorldUtility.GetRimWarDataForFaction(defender.parent.Faction)?.WorldSettlements?.Count <= 0)
                                    {
                                        WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.parent.Faction));
                                    }
                                }
                                else
                                {
                                    float rndRaze = Rand.Value;
                                    if (attacker.rimwarData?.behavior == RimWarBehavior.Expansionist)
                                    {
                                        rndRaze *= .8f;
                                    }
                                    else if (attacker.rimwarData?.behavior == RimWarBehavior.Warmonger)
                                    {
                                        rndRaze *= 1.2f;
                                    }

                                    if (rndRaze >= .9f)
                                    {
                                        let.Text += "\nThe city was brutally destroyed.";
                                        endPointsAttacker += (Rand.Range(.4f, .7f) * endPointsDefender);
                                        Find.WorldObjects.SettlementAt(defender.parent.Tile)?.Destroy();
                                        //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                                        //WorldUtility.GetRimWarDataForFaction(defender.Faction)?.FactionSettlements?.Remove(defender);
                                        if (WorldUtility.GetRimWarDataForFaction(defender.parent.Faction)?.WorldSettlements?.Count <= 0)
                                        {
                                            WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.parent.Faction));
                                        }
                                    }
                                    else
                                    {
                                        defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints));
                                        defender.PointDamage += defenderPointDamage;
                                    }
                                }
                            }
                        }
                        else if (attackerResult > 1.35f * defenderResult) //solid win
                        {
                            float rndCapture = Rand.Value;
                            if (attacker.rimwarData?.behavior == RimWarBehavior.Expansionist)
                            {
                                rndCapture *= 1.1f;
                            }
                            else if (attacker.rimwarData?.behavior == RimWarBehavior.Warmonger)
                            {
                                rndCapture *= 1.5f;
                            }

                            if (rndCapture >= .6f && attacker.rimwarData?.behavior != RimWarBehavior.Vassal)
                            {
                                //Log.Message("attacker is capturing " + defender.RimWorld_Settlement.Name);
                                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "captured", defender.parent.Label, defender.RimWarPoints);
                                let.lookTargets = attacker;
                                let.relatedFaction = attacker.Faction;
                                WorldUtility.ConvertSettlement(Find.WorldObjects.SettlementAt(defender.parent.Tile), WorldUtility.GetRimWarDataForFaction(defender.parent.Faction), WorldUtility.GetRimWarDataForFaction(attacker.Faction), Mathf.RoundToInt(Mathf.Max(endPointsDefender, 0)), defenderPointDamage);

                                //Find.World.worldObjects.SettlementAt(defender.Tile).SetFaction(attacker.Faction);
                                //WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                                //defender.Faction = attacker.Faction;
                                //WorldUtility.GetRimWarDataForFaction(attacker.Faction).FactionSettlements.Add(defender);
                                //Find.World.WorldUpdate();
                            }
                            else
                            {
                                float pointsAdjustment = endPointsDefender * (Rand.Range(.2f, .35f));
                                endPointsAttacker += pointsAdjustment;
                                endPointsDefender -= pointsAdjustment;
                                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.parent.Label, defender.RimWarPoints);
                                let.lookTargets = attacker;
                                let.relatedFaction = defender.parent.Faction;
                                if (endPointsDefender <= 1000)
                                {
                                    let.Text += "\nThe pathetic hamlet was burned to the ground.";
                                    Find.WorldObjects.SettlementAt(defender.parent.Tile)?.Destroy();
                                    //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                                    //WorldUtility.GetRimWarDataForFaction(defender.Faction)?.FactionSettlements?.Remove(defender);
                                    if (WorldUtility.GetRimWarDataForFaction(defender.parent.Faction)?.WorldSettlements.Count <= 0)
                                    {
                                        WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.parent.Faction));
                                    }
                                }
                                else
                                {
                                    defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints));
                                    defender.PointDamage += defenderPointDamage;
                                }
                            }
                        }
                        else
                        {
                            float rndCapture = Rand.Value;
                            if (attacker.rimwarData?.behavior == RimWarBehavior.Expansionist)
                            {
                                rndCapture *= 1.1f;
                            }
                            else if (attacker.rimwarData?.behavior == RimWarBehavior.Warmonger)
                            {
                                rndCapture *= 1.5f;
                            }

                            if (rndCapture >= .9f && attacker.rimwarData?.behavior != RimWarBehavior.Vassal)
                            {
                                //Log.Message("attacker is capturing " + defender.RimWorld_Settlement.Name);
                                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "captured", defender.parent.Label, defender.RimWarPoints);
                                let.lookTargets = attacker;
                                let.relatedFaction = attacker.Faction;
                                WorldUtility.ConvertSettlement(Find.WorldObjects.SettlementAt(defender.parent.Tile), WorldUtility.GetRimWarDataForFaction(defender.parent.Faction), WorldUtility.GetRimWarDataForFaction(attacker.Faction), Mathf.RoundToInt(Mathf.Max(endPointsDefender, 0)), defenderPointDamage);

                                //Find.World.worldObjects.SettlementAt(defender.Tile).SetFaction(attacker.Faction);
                                //WorldUtility.GetRimWarDataForFaction(defender.Faction).FactionSettlements.Remove(defender);
                                //defender.Faction = attacker.Faction;
                                //WorldUtility.GetRimWarDataForFaction(attacker.Faction).FactionSettlements.Add(defender);
                                //Find.World.WorldUpdate();
                            }
                            else
                            {
                                float pointsAdjustment = endPointsDefender * (Rand.Range(.1f, .25f));
                                endPointsAttacker += pointsAdjustment;
                                endPointsDefender -= pointsAdjustment;
                                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.parent.Label, defender.RimWarPoints);
                                let.lookTargets = attacker;
                                let.relatedFaction = defender.parent.Faction;
                                if (endPointsDefender <= 1000)
                                {
                                    let.Text += "\nThe pathetic hamlet was burned to the ground.";
                                    Find.WorldObjects.SettlementAt(defender.parent.Tile)?.Destroy();
                                    //Find.WorldObjects.Remove(Find.World.worldObjects.SettlementAt(defender.Tile));
                                    //WorldUtility.GetRimWarDataForFaction(defender.parent.Faction)?.WorldSettlements?.Remove(defender);
                                    if (WorldUtility.GetRimWarDataForFaction(defender.parent.Faction)?.WorldSettlements?.Count <= 0)
                                    {
                                        WorldUtility.RemoveRWDFaction(WorldUtility.GetRimWarDataForFaction(defender.parent.Faction));
                                    }
                                }
                                else
                                {
                                    defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints));
                                    defender.PointDamage += defenderPointDamage;
                                }
                            }
                        }
                        WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, 2 * attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, attackerPointDamage);

                    }
                    else
                    {
                        //Log.Message("attacker " + attacker.Label + " loses against settlement " + defender.RimWorld_Settlement.Name);
                        //Defender wins
                        
                        let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "failed in their assault against", defender.parent.Label, defender.RimWarPoints);
                        let.lookTargets = defender.parent;
                        let.relatedFaction = defender.parent.Faction;
                        if (defenderResult > 1.75 * attackerResult) //routed
                        {
                            endPointsDefender += endPointsAttacker * (Rand.Range(.35f, .55f)); //gain up to half the points of the attacker warband in combat power and disperse the warband                            
                            if (attacker.WarSettlementComp != null)
                            {
                                ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(Mathf.Min((Rand.Range(.3f, .5f) * endPointsAttacker)/2f, attacker.RimWarPoints)), Mathf.RoundToInt(Find.WorldGrid.TraversalDistanceBetween(attacker.Tile, attacker.ParentSettlement.Tile) * attacker.TicksPerMove) + Find.TickManager.TicksGame);
                                attacker.WarSettlementComp.SettlementPointGains.Add(reconstitute);
                            }
                        }
                        else if (defenderResult > 1.35f * attackerResult) //solid win; warband retreats back to parent settlement
                        {
                            float pointsAdjustment = endPointsAttacker * (Rand.Range(.3f, .4f));
                            endPointsAttacker -= pointsAdjustment;
                            endPointsDefender += pointsAdjustment;
                            if (attacker.WarSettlementComp != null)
                            {
                                WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, attackerPointDamage);
                            }
                        }
                        else
                        {
                            float pointsAdjustment = endPointsAttacker * (Rand.Range(.15f, .25f));
                            endPointsAttacker -= pointsAdjustment;
                            endPointsDefender += pointsAdjustment;
                            if (attacker.WarSettlementComp != null)
                            {
                                WorldUtility.CreateWarObjectOfType(attacker, Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 50, attacker.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(attacker.Faction), attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement, WorldObjectDefOf.Settlement);
                            }
                        }
                        defender.PointDamage += defenderPointDamage;
                        defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 100, defender.RimWarPoints + attacker.RimWarPoints));
                    }
                    RW_LetterMaker.Archive_RWLetter(let);
                    if (defender.parent.Faction != null && attacker.Faction != null && !defender.parent.Faction.defeated && !attacker.Faction.defeated)
                    {
                        defender.parent.Faction.TryAffectGoodwillWith(attacker.Faction, -5, true, true, null, null);
                        attacker.Faction.TryAffectGoodwillWith(defender.parent.Faction, -2, true, true, null, null);
                    }
                    */
                
            }
        }

        public static void ResolveRimWarTrade(Trader attacker, Trader defender)
        {
            if (attacker != null && attacker.Faction != null && defender != null && defender.Faction != null)
            {
                float combinedPoints = attacker.RimWarPoints + defender.RimWarPoints;
                float attackerRoll = Rand.Value;
                float defenderRoll = Rand.Value;
                float attackerResult = attackerRoll * attacker.RimWarPoints * attacker.rimwarData.combatAttribute;
                float defenderResult = defenderRoll * defender.RimWarPoints * defender.rimwarData.combatAttribute;
                float endPointsAttacker = 0f;
                float endPointsDefender = 0f;

                RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_TradeEvent);
                let.Label = "RW_LetterTradeEvent".Translate();

                if (attackerResult > defenderResult)
                {
                    //Log.Message("attacking trader " + attacker.Label + " wins agaisnt defending trader " + defender.Label);
                    let.Text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "swindled", defender.Label, defender.RimWarPoints);
                    endPointsAttacker = (attacker.RimWarPoints + (Rand.Range(.1f, .2f) * defender.RimWarPoints)); //winner always gains points
                    endPointsDefender = (defender.RimWarPoints + (Rand.Range(-.1f, .1f) * attacker.RimWarPoints)); //loser may lose or gain points
                                                                                                                   //Attacker wins                
                }
                else
                {
                    //Log.Message("defending trader " + defender.Label + " wins against attacking trader " + attacker.Label);
                    //Defender wins
                    let.Text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "was taken advantage of by", defender.Label, defender.RimWarPoints);
                    endPointsAttacker = (attacker.RimWarPoints + (Rand.Range(-.1f, .1f) * defender.RimWarPoints)); //loser may lose or gain points
                    endPointsDefender = (defender.RimWarPoints + (Rand.Range(.1f, .2f) * attacker.RimWarPoints)); //winner always gains points      
                }
                //attacker.TradedWith.Add(defender);            
                //defender.TradedWith.Add(attacker);
                attacker.tradedWithTrader = true;
                defender.tradedWithTrader = true;
                attacker.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsAttacker, 0, attacker.RimWarPoints * 1.5f));
                defender.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(endPointsDefender, 0, defender.RimWarPoints * 1.5f));
                defender.Faction.TryAffectGoodwillWith(attacker.Faction, 2, true, true, null, null);
                attacker.Faction.TryAffectGoodwillWith(defender.Faction, 2, true, true, null, null);

                let.lookTargets = attacker;
                let.relatedFaction = attacker.Faction;
                RW_LetterMaker.Archive_RWLetter(let);
            }
        }

        public static void ResolveSettlementTrade(Trader attacker, RimWarSettlementComp defenderTown)
        {
            if (ValidateRimWarAction(attacker, defenderTown, WorldUtility.GetAllWorldObjectsAtExcept(attacker.Tile, attacker)))
            {
                float combinedPoints = attacker.RimWarPoints + defenderTown.RimWarPoints;
                float attackerRoll = Rand.Value;
                float defenderRoll = Rand.Value;
                float attackerResult = attackerRoll * attacker.RimWarPoints * attacker.rimwarData.combatAttribute;
                float defenderResult = defenderRoll * defenderTown.RimWarPoints * defenderTown.RWD.combatAttribute;
                float endPointsAttacker = 0f;
                float endPointsDefender = 0f;

                RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_TradeEvent);
                let.Label = "RW_LetterTradeEvent".Translate();

                if (attackerResult > defenderResult)
                {
                    //Log.Message("attacking trader " + attacker.Label + " wins agaisnt defending settlement " + defenderTown.RimWorld_Settlement.Name);
                    //Attacker wins 
                    let.Text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "made significant gains in a trade with", defenderTown.parent.Label, defenderTown.RimWarPoints);
                    endPointsAttacker = (attacker.RimWarPoints + Mathf.Clamp(Rand.Range(.15f, .3f) * attacker.RimWarPoints, 0, 1000));  //always based on trader total points
                    endPointsDefender = (defenderTown.RimWarPoints + Mathf.Clamp(Rand.Range(.1f, .2f) * attacker.RimWarPoints, 0, 1000));
                }
                else
                {
                    //Log.Message("defending settlement " + defenderTown.RimWorld_Settlement.Name + " wins against attacking trader " + attacker.Label);
                    //Defender wins
                    let.Text = "RW_LetterTradeEventText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "made minor gains in a trade with", defenderTown.parent.Label, defenderTown.RimWarPoints);
                    endPointsAttacker = (attacker.RimWarPoints + Mathf.Clamp(Rand.Range(.1f, .2f) * attacker.RimWarPoints, 0, 1000));
                    endPointsDefender = (defenderTown.RimWarPoints + Mathf.Clamp(Rand.Range(.15f, .3f) * attacker.RimWarPoints, 0, 1000));
                }
                defenderTown.RimWarPoints = Mathf.RoundToInt(endPointsDefender);
                Trader newTrader = WorldUtility.CreateTrader(Mathf.RoundToInt(endPointsAttacker), attacker.rimwarData, attacker.ParentSettlement, defenderTown.parent.Tile, attacker.ParentSettlement, WorldObjectDefOf.Settlement);
                if (newTrader != null)
                {
                    newTrader.tradedWithSettlement = true;
                }
                defenderTown.parent.Faction.TryAffectGoodwillWith(attacker.Faction, 1, true, true, null, null);
                attacker.Faction.TryAffectGoodwillWith(defenderTown.parent.Faction, 1, true, true, null, null);

                let.lookTargets = newTrader;
                let.relatedFaction = attacker.Faction;
                RW_LetterMaker.Archive_RWLetter(let);
            }
            else if(attacker.rimwarData != null && attacker.rimwarData.WorldSettlements != null && attacker.rimwarData.WorldSettlements.Count > 0)
            {
                attacker.ValidateParentSettlement();
                if(attacker.ParentSettlement == null)
                {
                    WorldUtility.GetClosestSettlementInRWDTo(attacker.rimwarData, attacker.Tile);
                }
                Trader newTrader = WorldUtility.CreateTrader(Mathf.RoundToInt(attacker.RimWarPoints), attacker.rimwarData, attacker.ParentSettlement, attacker.Tile, attacker.ParentSettlement, WorldObjectDefOf.Settlement);
                if (newTrader != null)
                {
                    newTrader.tradedWithSettlement = true;
                }
            }
        }

        public static void DoRaidWithPoints(WarObject wo, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode, PawnGroupKindDef groupDef = null)
        {
            if (rwd != null && Find.FactionManager.AllFactions.Contains(rwd.RimWarFaction) && !rwd.RimWarFaction.defeated)
            {
                if (rwd.RimWarFaction.HostileTo(playerSettlement.Faction) || rwd.RimWarFaction == playerSettlement.Faction) //can also be warband reinforcing their own settlement
                {
                    IncidentParms parms = new IncidentParms();
                    if(groupDef == null)
                    {
                        groupDef = PawnGroupKindDefOf.Combat;
                    }
                    PawnGroupKindDef combat = groupDef;
                    
                    parms.faction = rwd.RimWarFaction;
                    parms.generateFightersOnly = true;
                    parms.raidArrivalMode = arrivalMode;
                    parms.target = playerSettlement.Map;
                    parms.points = wo.RimWarPoints * rwd.combatAttribute;
                    parms = ResolveRaidStrategy(parms, combat);
                    //Log.Message("raid strategy is " + parms.raidStrategy + " worker is " + parms.raidStrategy.workerClass);
                    parms.points = AdjustedRaidPoints((float)wo.RimWarPoints, parms.raidArrivalMode, parms.raidStrategy, rwd.RimWarFaction, combat);
                    if (!WorldUtility.FactionCanFight((int)parms.points, parms.faction))
                    {
                        Log.Warning(parms.faction.Name + " attempted to execute raid but has no defined combat groups.");
                        return;
                    }
                    //Log.Message("adjusted points " + parms.points);
                    //PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(combat, parms);
                    //List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
                    //if (list.Count == 0)
                    //{
                    //    Log.Error("Got no pawns spawning raid from parms " + parms);
                    //    //return false;
                    //}
                    //parms.raidArrivalMode.Worker.Arrive(list, parms);
                    IncidentWorker_WarObjectRaid raid = new IncidentWorker_WarObjectRaid();
                    try
                    {
                        raid.TryExecuteCustomWorker(parms, combat);

                        RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_HostileEvent);
                        if (rwd.RimWarFaction == playerSettlement.Faction)
                        {
                            let.Label = "RW_LetterSettlementBattle".Translate();
                            let.Text = "RW_ReinforcedSettlement".Translate(rwd.RimWarFaction, playerSettlement.Label);
                        }
                        else
                        {
                            let.Label = "RW_LetterPlayerSettlementBattle".Translate();
                            let.Text = "RW_RaidedPlayer".Translate(rwd.RimWarFaction, playerSettlement.Label, wo.RimWarPoints);
                        }
                        let.relatedFaction = rwd.RimWarFaction;
                        let.lookTargets = playerSettlement;
                        RW_LetterMaker.Archive_RWLetter(let);
                    }
                    catch (NullReferenceException ex)
                    {
                        Log.Warning("attempted to execute raid but encountered a null reference - " + ex);
                        if (rwd != null)
                        {
                            if (rwd.WorldSettlements != null && rwd.WorldSettlements.Count > 0)
                            {
                                ConsolidatePoints reconstitute = new ConsolidatePoints(Mathf.RoundToInt(wo.RimWarPoints/2f), 10 + Find.TickManager.TicksGame);
                                RimWarSettlementComp rwsc = rwd.WorldSettlements.RandomElement().GetComponent<RimWarSettlementComp>();
                                if (rwsc != null)
                                {
                                    rwsc.SettlementPointGains.Add(reconstitute);
                                }
                            }
                        }
                    }
                }
                else
                {
                    DoReinforcementWithPoints(wo, playerSettlement, rwd, arrivalMode);
                }
            }
        }

        public static void DoReinforcementWithPoints(WarObject wo, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode, PawnGroupKindDef groupDef = null)
        {
            if (rwd != null && Find.FactionManager.AllFactions.Contains(rwd.RimWarFaction) && !rwd.RimWarFaction.defeated)
            {
                if (!rwd.RimWarFaction.HostileTo(playerSettlement.Faction))
                {
                    IncidentParms parms = new IncidentParms();
                    if (groupDef == null)
                    {
                        groupDef = PawnGroupKindDefOf.Combat;
                    }
                    PawnGroupKindDef combat = groupDef;

                    parms.faction = rwd.RimWarFaction;
                    parms.generateFightersOnly = true;
                    parms.raidArrivalMode = arrivalMode;
                    parms.target = playerSettlement.Map;
                    parms.points = wo.RimWarPoints * rwd.combatAttribute;
                    parms.raidStrategy = RaidStrategyDefOf.ImmediateAttackFriendly;// RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttackFriendly);
                    parms.points = AdjustedRaidPoints((float)wo.RimWarPoints, parms.raidArrivalMode, parms.raidStrategy, rwd.RimWarFaction, combat);
                    if(!WorldUtility.FactionCanFight((int)parms.points, parms.faction))
                    {
                        Log.Warning(parms.faction.Name + " attempted to execute raid (reinforcement) but has no defined combat groups.");
                        return;
                    }
                    //Log.Message("adjusted points " + parms.points);
                    //PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(combat, parms);
                    //List<Pawn> list = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
                    //if (list.Count == 0)
                    //{
                    //    Log.Error("Got no pawns spawning raid from parms " + parms);
                    //    //return false;
                    //}
                    //parms.raidArrivalMode.Worker.Arrive(list, parms);
                    IncidentWorker_RaidFriendly raid = new IncidentWorker_RaidFriendly();
                    try
                    {
                        raid.TryExecute(parms);

                        RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_FriendlyEvent);
                        let.Label = "RW_LetterPlayerSettlementReinforcement".Translate();
                        let.Text = "RW_ReinforcedPlayer".Translate(rwd.RimWarFaction, playerSettlement.Label, wo.RimWarPoints);
                        let.relatedFaction = rwd.RimWarFaction;
                        let.lookTargets = playerSettlement;
                        RW_LetterMaker.Archive_RWLetter(let);
                    }
                    catch (NullReferenceException ex)
                    {
                        Log.Warning("attempted to execute raid but encountered a null reference - " + ex);
                        if (rwd != null)
                        {
                            if (rwd.WorldSettlements != null && rwd.WorldSettlements.Count > 0)
                            {
                                ConsolidatePoints reconstitute = new ConsolidatePoints(wo.RimWarPoints, 10 + Find.TickManager.TicksGame);
                                RimWarSettlementComp rwsc = rwd.WorldSettlements.RandomElement().GetComponent<RimWarSettlementComp>();
                                if (rwsc != null)
                                {
                                    rwsc.SettlementPointGains.Add(reconstitute);
                                }
                            }
                        }
                    }
                }
                else
                {
                    DoRaidWithPoints(wo, playerSettlement, rwd, arrivalMode);
                }
            }
        }

        public static void DoCaravanAttackWithPoints(WarObject warObject, Caravan playerCaravan, RimWarData rwd, PawnsArrivalModeDef arrivalMode, PawnGroupKindDef groupDef = null)
        {
            if (rwd != null)// && Find.FactionManager.AllFactions.Contains(rwd.RimWarFaction) && !rwd.RimWarFaction.defeated)
            {
                IncidentParms parms = new IncidentParms();
                if (groupDef == null)
                {
                    groupDef = PawnGroupKindDefOf.Combat;
                }
                PawnGroupKindDef kindDef = groupDef;
                parms.faction = warObject.Faction;
                parms.raidArrivalMode = arrivalMode;
                parms.points = (warObject.RimWarPoints - warObject.PointDamage) * rwd.combatAttribute;
                parms.target = playerCaravan;
                parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
                //Log.Message("params init");
                RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_HostileEvent);
                let.Label = "RW_CaravanAmbush".Translate(playerCaravan.Label);
                let.Text = "RW_CaravanAmbushedText".Translate(playerCaravan.Label, warObject.Label, warObject.RimWarPoints);
                let.lookTargets = playerCaravan;
                let.relatedFaction = warObject.Faction;
                RW_LetterMaker.Archive_RWLetter(let);

                if (warObject is Trader || warObject is Settler)
                {
                    Utility.IncidentWorker_WarObjectMeeting iw_caravanMeeting = new Utility.IncidentWorker_WarObjectMeeting();
                    if(iw_caravanMeeting.PreExecuteWorker(parms, warObject, warObject.PointDamage))
                    {

                    }
                    //parms.generateFightersOnly = false;                    
                    //IncidentWorker_CaravanMeeting iw_cm = new IncidentWorker_CaravanMeeting();
                    //parms.forced = true;
                    //iw_cm.TryExecute(parms);
                    //Log.Message("attempting to generate a caravan raid with " + warObject.Name);
                    //parms.generateFightersOnly = false;
                    //Faction enemyFaction = rwd.RimWarFaction;
                    //PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(kindDef, parms);
                    //defaultPawnGroupMakerParms.generateFightersOnly = false;
                    //defaultPawnGroupMakerParms.dontUseSingleUseRocketLaunchers = false;
                    //List<Pawn> attackers = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
                    //if (attackers.Count == 0)
                    //{
                    //    Log.Error("Caravan demand incident couldn't generate any enemies even though min points have been checked. faction=" + defaultPawnGroupMakerParms.faction + "(" + ((defaultPawnGroupMakerParms.faction == null) ? "null" : defaultPawnGroupMakerParms.faction.def.ToString()) + ") parms=" + parms);
                    //}
                    //else
                    //{
                    //    Map map = CaravanIncidentUtility.SetupCaravanAttackMap(playerCaravan, attackers, sendLetterIfRelatedPawns: false);
                    //    parms.target = map;
                    //    parms = ResolveRaidStrategy(parms, kindDef);
                    //    parms.points = AdjustedRaidPoints((float)warObject.RimWarPoints, parms.raidArrivalMode, parms.raidStrategy, rwd.RimWarFaction, kindDef);
                    //    CameraJumper.TryJumpAndSelect(playerCaravan);
                    //    //TaleRecorder.RecordTale(TaleDefOf.CaravanAmbushedByHumanlike, playerCaravan.RandomOwner());
                    //    LongEventHandler.QueueLongEvent(delegate
                    //    {
                    //        LordJob_AssaultColony lordJob_AssaultColony = new LordJob_AssaultColony(enemyFaction, canKidnap: true, canTimeoutOrFlee: false);
                    //        if (lordJob_AssaultColony != null)
                    //        {
                    //            LordMaker.MakeNewLord(enemyFaction, lordJob_AssaultColony, map, attackers);
                    //        }
                    //        Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                    //        CameraJumper.TryJump(attackers[0]);
                    //    }, "GeneratingMapForNewEncounter", false, null);
                    //}
                }
                else
                {
                    //Log.Message("attempting caravan demand");
                    Utility.IncidentWorker_WarObjectDemand iw_caravanDemand = new Utility.IncidentWorker_WarObjectDemand();
                    parms.forced = true;
                    if(iw_caravanDemand.PreExecuteWorker(parms, warObject, warObject.PointDamage))
                    {
                        if(warObject.DestinationTarget == playerCaravan)
                        {
                            warObject.DestinationTarget = warObject.ParentSettlement;
                        }
                    }
                    else
                    {
                        parms.generateFightersOnly = false;
                        Faction enemyFaction = rwd.RimWarFaction;
                        PawnGroupMakerParms defaultPawnGroupMakerParms = IncidentParmsUtility.GetDefaultPawnGroupMakerParms(kindDef, parms);
                        defaultPawnGroupMakerParms.generateFightersOnly = false;
                        defaultPawnGroupMakerParms.dontUseSingleUseRocketLaunchers = false;
                        List<Pawn> attackers = PawnGroupMakerUtility.GeneratePawns(defaultPawnGroupMakerParms).ToList();
                        if (attackers.Count == 0)
                        {
                            Log.Error("Caravan demand incident couldn't generate any enemies even though min points have been checked. faction=" + defaultPawnGroupMakerParms.faction + "(" + ((defaultPawnGroupMakerParms.faction == null) ? "null" : defaultPawnGroupMakerParms.faction.def.ToString()) + ") parms=" + parms);
                        }
                        else
                        {
                            Map map = CaravanIncidentUtility.SetupCaravanAttackMap(playerCaravan, attackers, sendLetterIfRelatedPawns: false);
                            parms.target = map;
                            parms = ResolveRaidStrategy(parms, kindDef);
                            parms.points = AdjustedRaidPoints((float)warObject.RimWarPoints, parms.raidArrivalMode, parms.raidStrategy, rwd.RimWarFaction, kindDef);
                            CameraJumper.TryJumpAndSelect(playerCaravan);
                            //TaleRecorder.RecordTale(TaleDefOf.CaravanAmbushedByHumanlike, playerCaravan.RandomOwner());
                            LongEventHandler.QueueLongEvent(delegate
                            {
                                LordJob_AssaultColony lordJob_AssaultColony = new LordJob_AssaultColony(enemyFaction, canKidnap: true, canTimeoutOrFlee: false);
                                if (lordJob_AssaultColony != null)
                                {
                                    LordMaker.MakeNewLord(enemyFaction, lordJob_AssaultColony, map, attackers);
                                }
                                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                                CameraJumper.TryJump(attackers[0]);
                            }, "GeneratingMapForNewEncounter", false, null);
                            Find.LetterStack.ReceiveLetter("RW_CaravanAmbush".Translate(playerCaravan.Label), "RW_CaravanAmbushedText".Translate(playerCaravan.Label, warObject.Label, warObject.RimWarPoints), LetterDefOf.ThreatSmall);
                            warObject.Destroy();
                        }
                    }
                }                
            }
        }

        public static void DoCaravanTradeWithPoints(WarObject warObject, Caravan playerCaravan, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef kindDef = PawnGroupKindDefOf.Trader;
            parms.faction = rwd.RimWarFaction;
            parms.raidArrivalMode = arrivalMode;
            parms.points = warObject.RimWarPoints;
            parms.target = playerCaravan;
            parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
            //IncidentWorker_CaravanMeeting iw_caravanMeeting = new IncidentWorker_CaravanMeeting();
            Utility.IncidentWorker_WarObjectMeeting iw_caravanMeeting = new Utility.IncidentWorker_WarObjectMeeting();
            if (iw_caravanMeeting.PreExecuteWorker(parms, warObject))
            {
                RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_FriendlyEvent);
                let.Label = "RW_CaravanTrade".Translate(playerCaravan.Label);
                let.Text = "RW_CaravanTradeText".Translate(playerCaravan.Label, warObject.Label);
                let.lookTargets = playerCaravan;
                let.relatedFaction = warObject.Faction;
                RW_LetterMaker.Archive_RWLetter(let);
            }
        }

        public static void DoSettlementTradeWithPoints(WarObject warObject, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode, TraderKindDef traderKind)
        {
            if (rwd != null && Find.FactionManager.AllFactions.Contains(rwd.RimWarFaction) && !rwd.RimWarFaction.defeated)
            {
                IncidentParms parms = new IncidentParms();
                PawnGroupKindDef kindDef = PawnGroupKindDefOf.Trader;
                parms.faction = rwd.RimWarFaction;
                parms.raidArrivalMode = arrivalMode;
                parms.points = warObject.RimWarPoints; 
                parms.target = playerSettlement.Map;
                parms.traderKind = traderKind;
                if (!WorldUtility.FactionCanTrade(parms.faction))
                {
                    Log.Warning(parms.faction.Name + " attempted to trade with player setttlement but has no defined trader kinds.");
                    return;
                }
                IncidentWorker_TraderCaravanArrival iw_tca = new IncidentWorker_TraderCaravanArrival();
                iw_tca.TryExecute(parms);

                RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_FriendlyEvent);
                let.Label = "RW_SettlementTrade".Translate(playerSettlement.Label);
                let.Text = "RW_SettlementTradeText".Translate(playerSettlement.Label, warObject.Label);
                let.lookTargets = playerSettlement;
                let.relatedFaction = warObject.Faction;
                RW_LetterMaker.Archive_RWLetter(let);
            }
        }

        public static void DoPeaceTalks_Caravan(WarObject warObject, Caravan playerCaravan, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        {
            IncidentParms parms = new IncidentParms();
            PawnGroupKindDef kindDef = PawnGroupKindDefOf.Peaceful;
            parms.faction = rwd.RimWarFaction;
            parms.raidArrivalMode = arrivalMode;
            parms.points = warObject.RimWarPoints;
            parms.target = playerCaravan;
            parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
            IncidentDef def = new IncidentDef();
            def = IncidentDef.Named("Quest_PeaceTalks");
            PeaceTalks peaceTalks = (PeaceTalks)WorldObjectMaker.MakeWorldObject(RimWarDefOf.PeaceTalks);
            peaceTalks.Tile = playerCaravan.Tile;
            peaceTalks.SetFaction(warObject.Faction);
            int randomInRange = SiteTuning.QuestSiteTimeoutDaysRange.RandomInRange;
            peaceTalks.GetComponent<TimeoutComp>().StartTimeout(randomInRange * 60000);
            Find.WorldObjects.Add(peaceTalks);
            string text = def.letterText.Formatted(warObject.Faction.def.leaderTitle, warObject.Faction.Name, randomInRange, warObject.Faction.leader.Named("PAWN")).AdjustedFor(warObject.Faction.leader).CapitalizeFirst();
            Find.LetterStack.ReceiveLetter(def.letterLabel, text, def.letterDef, peaceTalks, warObject.Faction);
            //IncidentWorker_QuestPeaceTalks iw_peaceTalkQuest = new IncidentWorker_QuestPeaceTalks();
            //iw_peaceTalkQuest.TryExecute(parms);
        }

        //public static void DoPeaceTalks_Settlement(WarObject warObject, RimWorld.Planet.Settlement playerSettlement, RimWarData rwd, PawnsArrivalModeDef arrivalMode)
        //{
        //    IncidentParms parms = new IncidentParms();
        //    PawnGroupKindDef kindDef = PawnGroupKindDefOf.Peaceful;
        //    parms.faction = rwd.RimWarFaction;
        //    parms.raidArrivalMode = arrivalMode;
        //    parms.points = warObject.RimWarPoints;
        //    parms.target = playerSettlement.Map;
        //    parms.raidStrategy = RaidStrategyOrRandom(RaidStrategyDefOf.ImmediateAttack);
        //    IncidentWorker_QuestPeaceTalks iw_peaceTalkQuest = new IncidentWorker_QuestPeaceTalks();
        //    iw_peaceTalkQuest.TryExecute(parms);
        //}

        public static float AdjustedRaidPoints(float points, PawnsArrivalModeDef raidArrivalMode, RaidStrategyDef raidStrategy, Faction faction, PawnGroupKindDef groupKind)
        {
            if (raidArrivalMode.pointsFactorCurve != null)
            {
                points *= raidArrivalMode.pointsFactorCurve.Evaluate(points);
            }
            if (raidStrategy.pointsFactorCurve != null)
            {
                points *= raidStrategy.pointsFactorCurve.Evaluate(points);
            }
            points = Mathf.Max(points, raidStrategy.Worker.MinimumPoints(faction, groupKind) * 1.05f);
            return points;
        }

        public static IncidentParms ResolveRaidStrategy(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            if (parms.raidStrategy == null)
            {
                Map map = (Map)parms.target;
                DefDatabase<RaidStrategyDef>.AllDefs.Where(delegate (RaidStrategyDef d)
                {
                    if (d.Worker.CanUseWith(parms, groupKind))
                    {
                        if (parms.raidArrivalMode == null)
                        {
                            if (d.arriveModes != null)
                            {
                                return d.arriveModes.Any((PawnsArrivalModeDef x) => x.Worker.CanUseWith(parms));
                            }
                            return false;
                        }
                        return true;
                    }
                    return false;
                }).TryRandomElementByWeight((RaidStrategyDef d) => d.Worker.SelectionWeight(map, parms.points), out RaidStrategyDef result);
                parms.raidStrategy = result;
                if (parms.raidStrategy == null)
                {
                    Log.Warning("No raid stategy found, defaulting to ImmediateAttack. Faction=" + parms.faction.def.defName + ", points=" + parms.points + ", groupKind=" + groupKind + ", parms=" + parms);
                    parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                }                
            }
            return parms;
        }

        public static bool ValidateRimWarAction(WarObject warObject, RimWarSettlementComp settlement, List<WorldObject> objectsHere)
        {
            if (warObject != null && !warObject.Destroyed && settlement != null && settlement.parent != null && !settlement.parent.Destroyed && objectsHere != null && objectsHere.Count > 0)
            {
                return true;
            }
            return false;
        }

        public static bool ValidateRimWarAction(WarObject warObject1, WarObject warObject2, List<WorldObject> objectsHere)
        {
            if (warObject1 != null && !warObject1.Destroyed && warObject2 != null && !warObject2.Destroyed && objectsHere != null && objectsHere.Count > 0)
            {
                return true;
            }
            return false;
        }

        public static PawnsArrivalModeDef PawnsArrivalModeOrRandom(PawnsArrivalModeDef arrivalMode)
        {
            try
            {
                foreach (PawnsArrivalModeDef allDefs in DefDatabase<PawnsArrivalModeDef>.AllDefs)
                {
                    if (allDefs.defName == arrivalMode.defName)
                    {
                        return arrivalMode;
                    }
                }
            }
            catch
            {

            }

            return DefDatabase<PawnsArrivalModeDef>.AllDefs.RandomElement();
        }

        public static RaidStrategyDef RaidStrategyOrRandom(RaidStrategyDef raidStrategy)
        {
            try
            {
                foreach (RaidStrategyDef allDefs in DefDatabase<RaidStrategyDef>.AllDefs)
                {
                    if (allDefs.defName == raidStrategy.defName)
                    {
                        return raidStrategy;
                    }
                }
            }
            catch
            {

            }

            return DefDatabase<RaidStrategyDef>.AllDefs.RandomElement();
            
        }

        //public static void AttackEmpireSettlement(WarObject rwo, RimWarSettlementComp rwsc, int ticksTillEvent = 15000)
        //{
        //    FactionFC fc = Find.World.GetComponent<FactionFC>();
        //    if (fc != null)
        //    {
        //        SettlementFC settlement = fc.returnSettlementByLocation(rwsc.parent.Tile, Find.World.info.name);
        //        militaryForce attackingForce = new militaryForce(Mathf.RoundToInt(Mathf.Max(1, rwo.RimWarPoints / 500f)), rwo.rimwarData.combatAttribute, null, rwo.Faction);
        //        if (settlement != null && attackingForce != null)
        //        {
        //            FCEvent fCEvent = FCEventMaker.MakeEvent(FCEventDefOf.settlementBeingAttacked);
        //            fCEvent.hasCustomDescription = true;
        //            fCEvent.timeTillTrigger = Find.TickManager.TicksGame + ticksTillEvent;
        //            fCEvent.location = settlement.mapLocation;
        //            fCEvent.planetName = settlement.planetName;
        //            fCEvent.hasDestination = true;
        //            fCEvent.customDescription = "settlementAboutToBeAttacked".Translate(settlement.name, rwo.Faction.Name);
        //            fCEvent.militaryForceDefending = militaryForce.createMilitaryForceFromSettlement(settlement);
        //            fCEvent.militaryForceDefendingFaction = FactionColonies.FactionColonies.getPlayerColonyFaction();
        //            fCEvent.militaryForceAttacking = attackingForce;
        //            fCEvent.militaryForceAttackingFaction = rwo.Faction;
        //            fCEvent.settlementFCDefending = settlement;
        //            Find.World.GetComponent<FactionFC>().addEvent(fCEvent);
        //            FCEvent fCEvent2 = fCEvent;
        //            fCEvent2.customDescription = fCEvent2.customDescription + "\n\nThe estimated attacking force's power is: " + fCEvent.militaryForceAttacking.forceRemaining.ToString();
        //            settlement.isUnderAttack = true;
        //            Find.LetterStack.ReceiveLetter("settlementInDanger".Translate(), fCEvent.customDescription, LetterDefOf.ThreatBig, new LookTargets(Find.WorldObjects.SettlementAt(settlement.mapLocation)));
        //        }
        //    }
        //}

        public static void CaravanPayment(Caravan caravan, int fee)
        {
            List<Thing> list = new List<Thing>();
            list.AddRange(caravan.PawnsListForReading.SelectMany((Pawn x) => ThingOwnerUtility.GetAllThingsRecursively(x, allowUnreal: false)));
            list.RemoveAll((Thing x) => x.def != ThingDefOf.Silver);

            int subtractedAmount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                int reduction = list[i].stackCount;
                if (list[i].stackCount > (fee - subtractedAmount))
                {
                    reduction = (fee - subtractedAmount);
                }
                list[i].SplitOff(Mathf.Min(list[i].stackCount, reduction));
                subtractedAmount += reduction;
                if (subtractedAmount >= fee)
                {
                    break;
                }
            }
        }

        public static int TryGetAvailableSilver(Caravan caravan)
        {
            int silver = 0;
            List<Thing> list2 = new List<Thing>();
            list2.AddRange(caravan.PawnsListForReading.SelectMany((Pawn x) => ThingOwnerUtility.GetAllThingsRecursively(x, allowUnreal: false)));
            list2.RemoveAll((Thing x) => x.def != ThingDefOf.Silver);
            foreach (Thing t in list2)
            {
                silver += t.stackCount;
            }
            return silver;
        }

        public static BattleSite CreateNewBattleSite(PlanetTile tile, List<WarObject> wos)
        {
            tile = GetValidBattleSiteTile(tile);
            if (tile != PlanetTile.Invalid)
            {
                BattleSite bs = (BattleSite)WorldObjectMaker.MakeWorldObject(RimWarDefOf.RW_BattleSite);
                bs.Tile = tile;
                //Log.Message("creating battle site");
                //for(int i = 0; i < wos.Count; i++)
                //{
                //    Log.Message("unit " + wos[i].Label + " " + wos[i].RimWarPoints);
                //}
                bs.Units.AddRange(wos);
                bs.nextCombatTick = Find.TickManager.TicksGame + 2500;
                Find.WorldObjects.Add(bs);
                return bs;
            }
            return null;
        }

        public static PlanetTile GetValidBattleSiteTile(PlanetTile tile)
        {
            List<PlanetTile> validTiles = new List<PlanetTile>();
            validTiles.Clear();
            List<PlanetTile> tmpTiles = new List<PlanetTile>();
            tmpTiles.Clear();
            tmpTiles.Add(tile);
            List<PlanetTile> adjacentTiles = new List<PlanetTile>();
            adjacentTiles.Clear();
            Find.WorldGrid.GetTileNeighbors(tile, adjacentTiles);
            tmpTiles.AddRange(adjacentTiles);
            for (int i = 0; i < tmpTiles.Count; i++)
            {
                bool tileValid = true;
                List<WorldObject> wos = WorldUtility.GetAllWorldObjectsAt(tmpTiles[i]);
                if (wos != null && wos.Count > 0)
                { 
                    for (int j = 0; j < wos.Count; j++)
                    {
                        WorldObject wo = wos[j];
                        if (wo is Settlement)
                        {
                            tileValid = false;
                            break;
                        }                        
                    }
                }
                Tile t = Find.WorldGrid[tmpTiles[i]];
                if (t.PrimaryBiome != null)
                {
                    if(!t.PrimaryBiome.canBuildBase)
                    {
                        tileValid = false;
                    }
                    if(Find.World.Impassable(tmpTiles[i]))
                    {
                        tileValid = false;
                    }
                }
                else
                {
                    tileValid = false;
                }
                if (tileValid)
                {
                    return tmpTiles[i];
                }
            }
            return PlanetTile.Invalid;
        }

        public static void ResolveCombat_Settlement(RimWarSettlementComp defender, WarObject attacker)
        {
            float pointClamp = 200f;
            if(attacker.RimWarPoints > 20000)
            {
                pointClamp = 4000f;
            }
            else if(attacker.RimWarPoints > 5000)
            {
                pointClamp = 2000f;
            }
            else if(attacker.RimWarPoints > 1000)
            {
                pointClamp = 500f;
            }

            float defenderPoints = Mathf.Clamp(defender.EffectivePoints, 0, pointClamp);
            float attackerPoints = Mathf.Clamp(attacker.EffectivePoints, 0, pointClamp);
            float combinedPoints = attackerPoints + defenderPoints;
            float attackerRoll = Rand.Value;
            float defenderRoll = Rand.Value;
            float attackerResult = attackerRoll * attackerPoints * attacker.rimwarData.combatAttribute;
            float defenderResult = defenderRoll * defenderPoints * defender.RWD.combatAttribute;
            if (defender.isCapitol)
            {
                defenderResult *= 1.15f;
            }

            if (defender.parent.def.defName == "City_Faction")
            {
                defenderResult *= 1.25f;
            }
            else if (defender.parent.def.defName == "City_Citadel")
            {
                defenderResult *= 1.5f;
            }

            float atkMod = Rand.Range(.5f, .7f);
            float defMod = Rand.Range(.5f, .7f);

            //endPointsAttacker = (attacker.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * defender.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
            //int attackerPointDamage = Mathf.RoundToInt(endPointsAttacker);
            //endPointsDefender = (defender.RimWarPoints * (1 - ((Rand.Range(.3f, .5f) * attacker.RimWarPoints) / combinedPoints))); //always lose points in relation to warband sizes
            //int defenderPointDamage = Mathf.RoundToInt(endPointsDefender);

            if (attackerResult > defenderResult)
            {
                //Attacker wins
                if (attackerResult > 1.75f * defenderResult) //routed
                {
                    atkMod -= .15f;
                    defMod += .4f;
                }
                else if (attackerResult > 1.35f * defenderResult) //solid win
                {
                    atkMod -= .1f;
                    defMod += .3f;
                }
                else
                {
                    atkMod -= .1f;
                    defMod += .2f;
                }
            }
            else 
            {
                //defender wins
                if (defenderResult > 1.75f * attackerResult) //routed
                {
                    atkMod += .25f;
                    defMod -= .3f;
                }
                else if (defenderResult > 1.35f * attackerResult) //solid win
                {
                    atkMod += .1f;
                    defMod -= .3f;
                }
                else
                {
                    atkMod += .1f;
                    defMod -= .2f;
                }
            }
            //Log.Message("attacker taking " + Mathf.RoundToInt(pointClamp * atkMod) + " damage; defender taking " + Mathf.RoundToInt(pointClamp * defMod));
            attacker.PointDamage += Mathf.RoundToInt(pointClamp * atkMod);
            defender.PointDamage += Mathf.RoundToInt(pointClamp * defMod);
            if((attacker.EffectivePoints <= 0) || (defender.EffectivePoints <= 0))
            {
                ResolveBattle_Settlement(defender, attacker, pointClamp);
            }
        }

        public static void ResolveBattle_Settlement(RimWarSettlementComp defender, WarObject attacker, float pointClamp)
        {

            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_NeutralEvent);
            let.Label = "RW_LetterSettlementBattle".Translate();

            if (attacker.EffectivePoints > 0)
            {
                float rndCapture = Rand.Value;
                if (attacker.rimwarData?.behavior == RimWarBehavior.Expansionist)
                {
                    rndCapture *= 1.1f;
                }
                else if (attacker.rimwarData?.behavior == RimWarBehavior.Warmonger)
                {
                    rndCapture *= 1.5f;
                }

                if (rndCapture > .5f && attacker.rimwarData?.behavior != RimWarBehavior.Vassal && defender.RWD.behavior != RimWarBehavior.Vassal && attacker.EffectivePoints >= pointClamp) //can capture
                {
                    let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "captured", defender.parent?.Label, defender.RimWarPoints);
                    let.lookTargets = attacker;
                    let.relatedFaction = attacker.Faction;
                    for(int i = 0; i < defender.AttackingUnits.Count; i++)
                    {
                        WarObject waro = defender.AttackingUnits[i];
                        if(waro != attacker)
                        {
                            WorldUtility.CreateWarObjectOfType(waro, Mathf.RoundToInt(waro.RimWarPoints), WorldUtility.GetRimWarDataForFaction(waro.Faction), waro.ParentSettlement, waro.Tile, waro.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, waro.PointDamage);
                        }
                    }
                    WorldUtility.ConvertSettlement(Find.WorldObjects.SettlementAt(defender.parent.Tile), WorldUtility.GetRimWarDataForFaction(defender.parent.Faction), WorldUtility.GetRimWarDataForFaction(attacker.Faction), Mathf.RoundToInt(Mathf.Max((defender.RimWarPoints * .2f) + attacker.RimWarPoints, 0)), attacker.PointDamage);
                }
                else
                {
                    let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "defeated", defender.parent.Label, defender.RimWarPoints);
                    let.lookTargets = attacker;
                    let.relatedFaction = defender.parent.Faction;
                    float rndSack = Rand.Value;
                    if (attacker.rimwarData?.behavior == RimWarBehavior.Warmonger)
                    {
                        rndSack *= 1.2f;
                    }
                    else if (attacker.rimwarData?.behavior == RimWarBehavior.Merchant)
                    {
                        rndSack *= 1.4f;
                    }
                    else if (attacker.rimwarData?.behavior == RimWarBehavior.Aggressive)
                    {
                        rndSack *= .8f;
                    }
                    if (rndSack > .5f && defender.RimWarPoints > 1000)
                    {
                        if (defender.RWD.behavior != RimWarBehavior.Vassal)
                        {
                            let.Text += "\nThe city was sacked and lost much of their wealth.";
                            float sackAmount = defender.RimWarPoints * Rand.Range(.3f, .6f) / defender.AttackingUnits.Count;
                            for (int i = 0; i < defender.AttackingUnits.Count; i++)
                            {
                                WarObject waro = defender.AttackingUnits[i];
                                waro.RimWarPoints += Mathf.RoundToInt(sackAmount);
                                defender.RimWarPoints -= Mathf.RoundToInt(sackAmount);
                                defender.PointDamage -= Mathf.RoundToInt(sackAmount);
                                WorldUtility.CreateWarObjectOfType(waro, Mathf.RoundToInt(Mathf.Clamp(waro.RimWarPoints, 50, 2 * waro.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(waro.Faction), waro.ParentSettlement, waro.Tile, waro.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, waro.PointDamage);
                            }
                            defender.PointDamage = defender.RimWarPoints - 1;
                            defender.AttackingUnits.Clear();
                        }
                        else
                        {
                            defender.PointDamage = defender.RimWarPoints - 1;
                            defender.AttackingUnits.Clear();
                            defender.parent.Destroy();
                        }
                    }
                    else
                    {
                        if (defender.RimWarPoints < 1000)
                        {
                            let.Text += "\nThe pathetic hamlet was burned to the ground.";
                        }
                        else
                        {
                            let.Text += "\nThe city was brutally destroyed.";
                        }
                        for (int i = 0; i < defender.AttackingUnits.Count; i++)
                        {
                            WarObject waro = defender.AttackingUnits[i];
                            WorldUtility.CreateWarObjectOfType(waro, Mathf.RoundToInt(Mathf.Clamp(waro.RimWarPoints, 50, 2 * waro.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(waro.Faction), waro.ParentSettlement, waro.Tile, waro.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, waro.PointDamage);
                        }                        
                        defender.AttackingUnits.Clear();
                        defender.PointDamage = defender.RimWarPoints - 1;
                        defender.parent.Destroy();
                    }
                }
            }
            else if(defender.EffectivePoints <= 0)
            {
                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "ended in mutual destruction with", defender.parent.Label, defender.RimWarPoints);
                let.lookTargets = attacker;
                let.relatedFaction = defender.parent.Faction;
                let.Text += "\nAny remaining survivors have fled.";
                for (int i = 0; i < defender.AttackingUnits.Count; i++)
                {
                    WarObject waro = defender.AttackingUnits[i];
                    if (waro.EffectivePoints > 0)
                    {
                        WorldUtility.CreateWarObjectOfType(waro, Mathf.RoundToInt(Mathf.Clamp(waro.RimWarPoints, 50, 2 * waro.RimWarPoints)), WorldUtility.GetRimWarDataForFaction(waro.Faction), waro.ParentSettlement, waro.Tile, waro.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, waro.PointDamage);
                    }
                }
                defender.AttackingUnits.Clear();
                defender.PointDamage = defender.RimWarPoints - 1;
                defender.parent.Destroy();
            }
            else
            {
                let.Text = "RW_LetterBattleText".Translate(attacker.Label.CapitalizeFirst(), attacker.RimWarPoints, "was destroyed in their assault against", defender.parent.Label, defender.RimWarPoints);
                let.lookTargets = defender.parent;
                let.relatedFaction = defender.parent.Faction;
                defender.AttackingUnits.Remove(attacker);
            }
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void ResolveCombat_Units(WarObject attacker, WarObject defender) //offense, defense
        {
            //Log.Message("resolving combat between " + attacker.Label + " pts " + attacker.EffectivePoints + " " + defender.Label + " pts " + defender.EffectivePoints);
            float pointClamp = 200f;
            if (attacker.RimWarPoints > 20000)
            {
                pointClamp = 4000f;
            }
            else if (attacker.RimWarPoints > 5000)
            {
                pointClamp = 2000f;
            }
            else if (attacker.RimWarPoints > 1000)
            {
                pointClamp = 500f;
            }

            float defenderPoints = Mathf.Clamp(defender.EffectivePoints, 0, pointClamp);
            float attackerPoints = Mathf.Clamp(attacker.EffectivePoints, 0, pointClamp);
            float combinedPoints = attackerPoints + defenderPoints;
            float attackerRoll = Rand.Value;
            float defenderRoll = Rand.Value;
            float attackerResult = attackerRoll * attackerPoints * attacker.rimwarData.combatAttribute;
            float defenderResult = defenderRoll * defenderPoints * defender.rimwarData.combatAttribute;

            float atkMod = Rand.Range(.5f, .7f);
            float defMod = Rand.Range(.5f, .7f);

            if (attackerResult > defenderResult)
            {
                //Attacker wins
                if (attackerResult > 1.75f * defenderResult) //routed
                {
                    atkMod -= .15f;
                    defMod += .35f;
                }
                else if (attackerResult > 1.35f * defenderResult) //solid win
                {
                    atkMod -= .1f;
                    defMod += .25f;
                }
                else
                {
                    atkMod -= .1f;
                    defMod += .15f;
                }
            }
            else
            {
                //defender wins
                if (defenderResult > 1.75f * attackerResult) //routed
                {
                    atkMod += .35f;
                    defMod -= .15f;
                }
                else if (defenderResult > 1.35f * attackerResult) //solid win
                {
                    atkMod += .25f;
                    defMod -= .1f;
                }
                else
                {
                    atkMod += .15f;
                    defMod -= .1f;
                }
            }
            attacker.PointDamage += Mathf.RoundToInt(pointClamp * atkMod);
            defender.PointDamage += Mathf.RoundToInt(pointClamp * defMod);
        }

        public static void ResolveBattle_Units(BattleSite bs)
        {
            //Log.Message("resolving field combat");
            RW_Letter let = RW_LetterMaker.Make_RWLetter(RimWarDefOf.RimWar_NeutralEvent);
            let.Label = "RW_LetterBattle".Translate();
            List<WarObject> survivors = new List<WarObject>();
            survivors.Clear();
            List<WarObject> defeated = new List<WarObject>();
            defeated.Clear();
            string strS = "";
            int sPts = 0;
            string strD = "";
            int dPts = 0;
            if (bs.Units != null && bs.Units.Count > 0)
            {
                for (int i = 0; i < bs.Units.Count; i++)
                {
                    if (bs.Units[i].EffectivePoints > 0)
                    {
                        survivors.Add(bs.Units[i]);
                        if (strS != "")
                        {
                            strS += " ";
                        }
                        strS += bs.Units[i].Label;
                        sPts += bs.Units[i].RimWarPoints;
                    }
                    else
                    {
                        defeated.Add(bs.Units[i]);
                        if (strD != "")
                        {
                            strD += " ";
                        }
                        strD += bs.Units[i].Label;
                        dPts += bs.Units[i].RimWarPoints;
                    }
                }
            }
            if (survivors.Count > 0)
            {
                let.Text = "RW_LetterBattleText".Translate(strS.CapitalizeFirst(), sPts, "defeated", strD, dPts);
                foreach (WarObject wo in survivors)
                {
                    //if ((wo.EffectivePoints > Mathf.RoundToInt(wo.RimWarPoints / 2f) && wo.DestinationTarget != bs) || wo is Settler)
                    //{
                    //    if (wo is Settler)
                    //    {
                    //        WorldUtility.CreateSettler(wo.RimWarPoints, wo.rimwarData, wo.ParentSettlement, wo.Tile, wo.DestinationTile, WorldObjectDefOf.Settlement, true, wo.PointDamage);
                    //    }
                    //    else
                    //    {
                    //        //Log.Message("to dest");
                    //        WorldUtility.CreateWarObjectOfType(wo, wo.RimWarPoints, wo.rimwarData, wo.ParentSettlement, wo.Tile, wo.DestinationTarget, wo.def, 0, false, true, wo.PointDamage);
                    //    }
                    //}
                    //else
                    //{
                        //Log.Message("to home");
                        WorldUtility.CreateWarObjectOfType(wo, wo.RimWarPoints, wo.rimwarData, wo.ParentSettlement, wo.Tile, wo.ParentSettlement, WorldObjectDefOf.Settlement, 0, false, true, wo.PointDamage);
                    //}
                }
            }
            else
            {
                let.Text = "RW_LetterBattleFailText".Translate(strD);
            }
            bs.Destroy();
            if (survivors.Count > 0)
            {
                let.lookTargets = survivors[0];
                let.relatedFaction = survivors[0].Faction;
            }
            RW_LetterMaker.Archive_RWLetter(let);
        }

        public static void AttackBattleSite(Caravan car, BattleSite bs, List<ActiveTransporterInfo> pods = null, PawnsArrivalModeDef arrivalMode = null)
        {
            if (!bs.HasMap)
            {
                LongEventHandler.QueueLongEvent(delegate
                {
                    AttackBattleSiteNow(car, bs, pods, arrivalMode);
                }, "GeneratingMapForNewEncounter", false, null);
            }
            else
            {
                AttackBattleSiteNow(car, bs, pods, arrivalMode);
            }
        }

        private static void AttackBattleSiteNow(Caravan car, BattleSite bs, List<ActiveTransporterInfo> pods = null, PawnsArrivalModeDef arrivalMode = null)
        {
            bool num = !bs.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(bs.Tile, null);
            TaggedString letterLabel = "RW_LetterLabelAttackBattleSite".Translate();
            TaggedString letterText = "";
            if (car != null)
            {
                letterText = "RW_LetterAttackBattleSite".Translate(car.Label, bs.GetUnitsWithPointsToString).CapitalizeFirst();
            }
            else if( pods != null && arrivalMode != null)
            {
                letterText = "RW_LetterDropOnBattleSite".Translate(bs.GetUnitsWithPointsToString).CapitalizeFirst();
            }
            //AffectRelationsOnAttacked_NewTmp(settlement, ref letterText);
            if (num)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(orGenerateMap.mapPawns.AllPawns, ref letterLabel, ref letterText, "LetterRelatedPawnsSettlement".Translate(Faction.OfPlayer.def.pawnsPlural), informEvenIfSeenBefore: true);
            }            
            if (car != null)
            {
                Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, car.PawnsListForReading, bs.Units[0].Faction);
                CaravanEnterMapUtility.Enter(car, orGenerateMap, CaravanEnterMode.Edge, CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
            }
            else if(pods != null && arrivalMode != null)
            {
                Thing lookTarget = TransportersArrivalActionUtility.GetLookTarget(pods);
                Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, lookTarget, null);
                arrivalMode.Worker.TravellingTransportersArrived(pods, orGenerateMap);
                //IntVec3 near = orGenerateMap.AllCells.RandomElement();
                //if (arrivalMode == PawnsArrivalModeDefOf.CenterDrop)
                //{
                //    RCellFinder.TryFindRandomPawnEntryCell(out near, orGenerateMap, .5f, false);
                //}
                //else
                //{
                //    RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(delegate (IntVec3 x)
                //    {
                //        if (x.Standable(orGenerateMap) && !x.Fogged(orGenerateMap))
                //        {
                //            return x.GetRoom(orGenerateMap).CellCount >= 40;
                //        }
                //        return false;
                //    }, orGenerateMap, out near);
                //}
                //TransportPodsArrivalActionUtility.DropTravelingTransportPods(pods, near, orGenerateMap);
            }
            GenerateSiteUnits(bs, orGenerateMap);
            //bs.Destroy();
        }

        public static void GenerateSettlementAttackers(RimWarSettlementComp rwsc, Map map)
        {
            List<Thing> spawnedList = new List<Thing>();
            spawnedList.Clear();
            IEnumerable<Pawn> list = from p in map.mapPawns.AllPawnsSpawned
                                     where (p.Faction != null && p.Faction == rwsc.parent.Faction)
                                     select p;
            spawnedList.AddRange(list);
            foreach(WarObject rwo in rwsc.AttackingUnits)
            {
                IntVec3 defPoint = default(IntVec3);
                IEnumerable<Pawn> rwoPawns = GeneratePawnGroup(rwo, map, HostileThingsFromList(spawnedList, rwo.Faction), out defPoint);
                InflictPointDamageToPawnGroup(rwo.Pawns, rwo.PointDamage);
                spawnedList.AddRange(rwoPawns);
            }
        }

        public static void GenerateSiteUnits(RimWarSite rws, Map map)
        {
            List<Thing> spawnedList = new List<Thing>();
            spawnedList.Clear();
            foreach (WarObject rwo in rws.Units)
            {
                IntVec3 defPoint = default(IntVec3);
                //RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(delegate (IntVec3 x)
                //{
                //    if (x.Standable(orGenerateMap) && !x.Fogged(orGenerateMap))
                //    {
                //        return x.GetRoom(orGenerateMap).CellCount >= 40;
                //    }
                //    return false;
                //}, orGenerateMap, out defPoint);
                IEnumerable<Pawn> rwoPawns = GeneratePawnGroup(rwo, map, HostileThingsFromList(spawnedList, rwo.Faction), out defPoint);
                InflictPointDamageToPawnGroup(rwo.Pawns, rwo.PointDamage);
                spawnedList.AddRange(rwoPawns);
                //LordJob_DefendPoint ljd = new LordJob_DefendPoint(defPoint);
                //LordMaker.MakeNewLord(rwo.Faction, ljd, orGenerateMap, rwoPawns);               
            }
        }

        public static List<Thing> HostileThingsFromList(List<Thing> inList, Faction f)
        {
            List<Thing> hostileThings = new List<Thing>();
            hostileThings.Clear();
            if (inList != null && inList.Count > 0)
            {
                for (int i = 0; i < inList.Count; i++)
                {
                    if (inList[i].Faction != null && inList[i].Faction.HostileTo(f))
                    {
                        hostileThings.Add(inList[i]);
                    }
                }
            }
            return hostileThings;
        }

        public static IEnumerable<Pawn> GeneratePawnGroup(WarObject rwo, Map map, List<Thing> hostileThings, out IntVec3 near)
        {
            RCellFinder.TryFindRandomCellNearTheCenterOfTheMapWith(delegate (IntVec3 x)
            {
                if (x.Standable(map) && !x.Fogged(map))
                {
                    return x.GetRoom(map).CellCount >= 20;
                }
                return false;
            }, map, out near);
            IEnumerable<IntVec3> cellBlock = GenRadial.RadialCellsAround(near, 20, true);

            PawnGroupMakerParms parms = new PawnGroupMakerParms();
            parms.faction = rwo.Faction;
            parms.dontUseSingleUseRocketLaunchers = true;
            parms.groupKind = PawnGroupKindDefOf.Combat;
            parms.points = rwo.RimWarPoints;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            IEnumerable<Pawn> pawnGroup = PawnGroupMakerUtility.GeneratePawns(parms);
            Lord lord = null;
            rwo.Pawns.Clear();
            foreach (Pawn newPawn in pawnGroup)
            {
                IntVec3 c = cellBlock.RandomElement();
                //GenPlace.TryPlaceThing(p, c, map, ThingPlaceMode.Near);
                GenSpawn.Spawn(newPawn, c, map);
                if (rwo.Faction != null && rwo.Faction != Faction.OfPlayer)
                {                    
                    if (newPawn.Map.mapPawns.SpawnedPawnsInFaction(rwo.Faction).Any((Pawn p) => p != newPawn))
                    {
                        lord = ((Pawn)GenClosest.ClosestThing_Global(newPawn.Position, newPawn.Map.mapPawns.SpawnedPawnsInFaction(rwo.Faction), 99999f, delegate (Thing p)
                        {
                            if (p != newPawn)
                            {
                                return ((Pawn)p).GetLord() != null;
                            }
                            return false;
                        })).GetLord();
                    }
                    if (lord == null)
                    {
                        if (hostileThings == null || hostileThings.Count <= 0)
                        {
                            LordJob_DefendPoint lordJob = new LordJob_DefendPoint(newPawn.Position);
                            lord = LordMaker.MakeNewLord(rwo.Faction, lordJob, map);
                        }
                        else
                        {
                            LordJob_AssaultColony lordJob = new LordJob_AssaultColony(rwo.Faction, false, false, false, false, false);
                            lord = LordMaker.MakeNewLord(rwo.Faction, lordJob, map);
                        }                        
                        
                    }
                    lord.AddPawn(newPawn);
                    rwo.Pawns.Add(newPawn);
                }
            }
            if(lord.faction.def.autoFlee)
            {
                for (int i = 0; i <lord.Graph.transitions.Count; i++)
                {
                    if (lord.Graph.transitions[i].target is LordToil_PanicFlee)
                    {
                        for (int j = 0; j < lord.Graph.transitions[i].triggers.Count; j++)
                        {
                            if (lord.Graph.transitions[i].triggers[j] is Trigger_FractionPawnsLost)
                            {
                                lord.Graph.transitions[i].triggers[j] = new Trigger_FractionPawnsLost(Rand.Range(.8f, 1f));
                            }
                        }
                    }
                }
            }
            return pawnGroup;
        }

        public static void InflictPointDamageToPawnGroup(IEnumerable<Pawn> pList, int amount)
        {
            if(pList != null)
            {
                while (amount > 0)
                {
                    float ptDam = Mathf.Clamp(Rand.Range(2f, 10f), 0, amount);
                    amount -= Mathf.RoundToInt(ptDam * 2f);
                    DamageInfo dinfo = new DamageInfo(RimWarDefOf.RW_CombatInjury, ptDam);
                    try { pList.RandomElement().TakeDamage(dinfo); } catch { }
                }
            }
        }

        public static void UpdateUnitCombatStatus(List<WarObject> units)
        {
            if (units != null && units.Count > 0)
            {
                List<Pawn> remList = new List<Pawn>();
                remList.Clear();
                //Log.Message("updating units " + units.Count);
                foreach (WarObject waro in units)
                {
                    float tmpPoints = 0;
                    //Log.Message("evaluating pawn group of " + waro.Pawns.Count);
                    foreach (Pawn p in waro.Pawns)
                    {
                        if (!p.DestroyedOrNull())
                        {
                            if (p.Dead || p.Downed)
                            {
                                tmpPoints += waro.PointsPerPawn;
                            }
                            else
                            {
                                //Log.Message("summary health for " + p.LabelShort + " is " + p.health.summaryHealth.SummaryHealthPercent);
                                tmpPoints += (waro.PointsPerPawn * (1f - p.health.summaryHealth.SummaryHealthPercent));
                            }
                        }
                        else
                        {
                            remList.Add(p);
                        }
                    }
                    waro.RimWarPoints -= Mathf.RoundToInt(remList.Count * waro.PointsPerPawn);
                    foreach (Pawn p in remList)
                    {
                        waro.Pawns.Remove(p);
                    }
                    //Log.Message("setting point damage to " + tmpPoints + " for " + waro.RimWarPoints);
                    waro.PointDamage = Mathf.RoundToInt(tmpPoints);
                }
            }
        }
    }
}
