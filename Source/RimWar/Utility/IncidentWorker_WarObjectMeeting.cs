using System;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;
using UnityEngine;
using HarmonyLib;
using RimWar.Planet;

namespace RimWar.Utility
{
    public class IncidentWorker_WarObjectMeeting : IncidentWorker
    {
        private const int MapSize = 100;
        WarObject wo = null;
        int pointDamage = 0;

        private int RequestFee
        {
            get
            {
                return Mathf.RoundToInt(wo.RimWarPoints / 4f);
            }
        }

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (parms.target is Map)
            {
                return true;
            }
            Faction faction;
            if (CaravanIncidentUtility.CanFireIncidentWhichWantsToGenerateMapAt(parms.target.Tile))
            {
                return TryFindFaction(out faction);
            }
            return false;
        }        

        private bool TryFindFaction(out Faction faction)
        {
            return Find.FactionManager.AllFactionsListForReading.Where(delegate (Faction x)
            {
                if (!x.IsPlayer && !x.HostileTo(Faction.OfPlayer) && !x.Hidden && x.def.humanlikeFaction && !x.temporary && x.def.caravanTraderKinds.Any())
                {
                    return !x.def.pawnGroupMakers.NullOrEmpty();
                }
                return false;
            }).TryRandomElement(out faction);
        }

        public bool PreExecuteWorker(IncidentParms parms, WarObject _wo, int _pointDamage = 0)
        {
            this.wo = _wo;
            this.pointDamage = _pointDamage;
            return TryExecuteWorker(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (parms.target is Map)
            {
                return IncidentDefOf.TravelerGroup.Worker.TryExecute(parms);
            }
            Caravan caravan = (Caravan)parms.target;
            Faction faction = parms.faction;
            bool factionCanTrade = WorldUtility.FactionCanTrade(parms.faction);
            int silverAvailable = IncidentUtility.TryGetAvailableSilver(caravan);
            RimWarSettlementComp rwsc = WorldUtility.GetClosestSettlementOfFaction(caravan.Faction, caravan.Tile, 40);
            Settlement colonySettlement = null;
            if(rwsc != null)
            {
                colonySettlement = rwsc.parent as Settlement;
            }
            
            List<Pawn> list = GenerateCaravanPawns(faction, wo.RimWarPoints);
            if (!list.Any())
            {
                Log.Error("IncidentWorker_CaravanMeeting could not generate any pawns.");
                return false;
            }
            Caravan metCaravan = CaravanMaker.MakeCaravan(list, faction, -1, addToWorldPawnsIfNotAlready: false);
            bool hostileToPlayer = faction.HostileTo(Faction.OfPlayer);
            CameraJumper.TryJumpAndSelect(caravan);
            DiaNode diaNode = new DiaNode((string)"CaravanMeeting".Translate(caravan.Name, faction.Name, PawnUtility.PawnKindsToLineList(from x in metCaravan.PawnsListForReading
                                                                                                                                         select x.kindDef, "  - ")).CapitalizeFirst());
            Pawn bestPlayerNegotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan, faction, metCaravan.TraderKind);
            if (metCaravan.CanTradeNow)
            {
                DiaOption diaOption = new DiaOption("CaravanMeeting_Trade".Translate());
                diaOption.action = delegate
                {
                    Find.WindowStack.Add(new Dialog_Trade(bestPlayerNegotiator, metCaravan));
                    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(metCaravan.Goods.OfType<Pawn>(), "LetterRelatedPawnsTradingWithOtherCaravan".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent);
                };
                if (bestPlayerNegotiator == null)
                {
                    if (metCaravan.TraderKind.permitRequiredForTrading != null && !caravan.pawns.Any(delegate (Pawn p)
                    {
                        if (p.royalty != null)
                        {
                            return p.royalty.HasPermit(metCaravan.TraderKind.permitRequiredForTrading, faction);
                        }
                        return false;
                    }))
                    {
                        RoyalTitleDef royalTitleDef = faction.def.RoyalTitlesAwardableInSeniorityOrderForReading.First(delegate (RoyalTitleDef t)
                        {
                            if (t.permits != null)
                            {
                                return t.permits.Contains(metCaravan.TraderKind.permitRequiredForTrading);
                            }
                            return false;
                        });
                        diaOption.Disable("CaravanMeeting_NoPermit".Translate(royalTitleDef.GetLabelForBothGenders(), faction).Resolve());
                    }
                    else if(hostileToPlayer)
                    {
                        diaOption.Disable("RW_CaravanMeeting_TradeUnwilling".Translate(faction.Name));
                    }
                    else
                    {
                        diaOption.Disable("CaravanMeeting_TradeIncapable".Translate());
                    }
                }
                else if(!factionCanTrade)
                {
                    diaOption.Disable("RW_CaravanMeeting_FactionIncapableOfTrade".Translate());
                }
                diaNode.options.Add(diaOption);
            }
            DiaOption diaOption2 = new DiaOption("CaravanMeeting_Attack".Translate());
            diaOption2.action = delegate
            {                
                LongEventHandler.QueueLongEvent(delegate
                {
                    Pawn t2 = caravan.PawnsListForReading[0];
                    
                    //faction.TrySetRelationKind(Faction.OfPlayer, FactionRelationKind.Hostile, true, "GoodwillChangedReason_AttackedCaravan".Translate(), t2);
                    Map map = CaravanIncidentUtility.GetOrGenerateMapForIncident(caravan, new IntVec3(100, 1, 100), WorldObjectDefOf.AttackedNonPlayerCaravan);
                    if (map != null && map.Parent != null && map.Parent.Faction == null)
                    {
                        map.Parent.SetFaction(faction);
                    }
                    TaggedString letterText = "RW_CaravanAttackedUnit".Translate(caravan.Label, wo.Label, wo.Faction.Name);
                    RimWorld.Planet.SettlementUtility.AffectRelationsOnAttacked(map.Parent, ref letterText);
                    MultipleCaravansCellFinder.FindStartingCellsFor2Groups(map, out IntVec3 playerSpot, out IntVec3 enemySpot);
                    CaravanEnterMapUtility.Enter(caravan, map, (Pawn p) => CellFinder.RandomClosewalkCellNear(playerSpot, map, 12), CaravanDropInventoryMode.DoNotDrop, draftColonists: true);
                    List<Pawn> list2 = metCaravan.PawnsListForReading.ToList();
                    CaravanEnterMapUtility.Enter(metCaravan, map, (Pawn p) => CellFinder.RandomClosewalkCellNear(enemySpot, map, 12));
                    LordMaker.MakeNewLord(faction, new LordJob_DefendAttackedTraderCaravan(list2[0].Position), map, list2);
                    Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                    CameraJumper.TryJumpAndSelect(t2);
                    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter_Send(list2, "LetterRelatedPawnsGroupGeneric".Translate(Faction.OfPlayer.def.pawnsPlural), LetterDefOf.NeutralEvent, informEvenIfSeenBefore: true);
                    while(pointDamage > 0)
                    {
                        float ptDam = Mathf.Clamp(Rand.Range(2f, 10f), 0, pointDamage);
                        pointDamage -= Mathf.RoundToInt(ptDam * 2f);
                        DamageInfo dinfo = new DamageInfo(RimWarDefOf.RW_CombatInjury, ptDam);
                        list.RandomElement().TakeDamage(dinfo);
                    }
                    wo.Destroy();
                }, "GeneratingMapForNewEncounter", false, null);
            };
            diaOption2.resolveTree = true;
            diaNode.options.Add(diaOption2);
            if (!hostileToPlayer)
            {
                string tradeColonyString = "RW_CaravanMeeting_TradeWithColony".Translate(RequestFee);
                DiaOption diaOption21 = new DiaOption(tradeColonyString);
                diaOption21.action = delegate
                {
                    ActionTradeWithColony(colonySettlement, caravan, RequestFee);
                };
                if (colonySettlement == null)
                {
                    diaOption21.Disable("RW_CaravanMeeting_TradeWithColonyDisabledDistance".Translate());
                }
                else if (silverAvailable < RequestFee)
                {
                    diaOption21.Disable("RW_CaravanMeeting_TradeWithColonyDisabled".Translate(silverAvailable, RequestFee));
                }
                diaOption21.resolveTree = true;
                diaNode.options.Add(diaOption21);
            }
            DiaOption diaOption3 = new DiaOption("CaravanMeeting_MoveOn".Translate());
            diaOption3.action = delegate
            {
                RemoveAllPawnsAndPassToWorld(metCaravan);
            };
            diaOption3.resolveTree = true;
            diaNode.options.Add(diaOption3);
            string title = "CaravanMeetingTitle".Translate(caravan.Label);
            Find.WindowStack.Add(new Dialog_NodeTreeWithFactionInfo(diaNode, faction, true, false, title));
            Find.Archive.Add(new ArchivedDialog(diaNode.text, title, faction));

            return true;
        }

        private List<Pawn> GenerateCaravanPawns(Faction faction, int points)
        {
            return PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Trader,
                faction = faction,
                points = points, // TraderCaravanUtility.GenerateGuardPoints(),
                dontUseSingleUseRocketLaunchers = true
            }).ToList();
        }

        private void RemoveAllPawnsAndPassToWorld(Caravan caravan)
        {
            List<Pawn> pawnsListForReading = caravan.PawnsListForReading;
            for (int i = 0; i < pawnsListForReading.Count; i++)
            {
                Find.WorldPawns.PassToWorld(pawnsListForReading[i]);
            }
            caravan.RemoveAllPawns();
        }

        private void ActionTradeWithColony(Settlement s, Caravan caravan, int fee)
        {
            IncidentUtility.CaravanPayment(caravan, fee);
            wo.DestinationTarget = s;
            wo.PathToTarget(s);
        }
    }
}
