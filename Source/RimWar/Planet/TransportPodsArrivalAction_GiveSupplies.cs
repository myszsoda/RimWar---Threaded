using System;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using System.Linq;

namespace RimWar.Planet
{
    public class TransportPodsArrivalAction_GiveSupplies : TransportersArrivalAction
    {
        private Settlement settlement;

        public override bool GeneratesMap => false;

        public TransportPodsArrivalAction_GiveSupplies()
        {
        }

        public TransportPodsArrivalAction_GiveSupplies(Settlement settlement)
        {
            this.settlement = settlement;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref settlement, "settlement");
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
        {
            FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
            if (!(bool)floatMenuAcceptanceReport)
            {
                return floatMenuAcceptanceReport;
            }
            if (settlement != null && settlement.Tile != destinationTile)
            {
                return false;
            }
            return CanGiveSuppliesTo(pods, settlement);
        }

        public override void Arrived(List<ActiveTransporterInfo> pods, PlanetTile tile)
        {
            for (int i = 0; i < pods.Count; i++)
            {
                for (int j = 0; j < pods[i].innerContainer.Count; j++)
                {
                    if (!(pods[i].innerContainer[j] is Pawn pawn))
                    {
                        continue;
                    }
                    if (pawn.RaceProps.Humanlike)
                    {
                        Pawn result;
                        if (pawn.HomeFaction == settlement.Faction)
                        {
                            GenGuest.AddHealthyPrisonerReleasedThoughts(pawn);
                        }
                        else if (PawnsFinder.AllMapsCaravansAndTravellingTransporters_AliveSpawned_FreeColonists.TryRandomElement(out result))
                        {
                            Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.SoldSlave, result.Named(HistoryEventArgsNames.Doer)));
                        }
                    }
                    else if (pawn.RaceProps.Animal && pawn.relations != null)
                    {
                        Pawn firstDirectRelationPawn = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond);
                        if (firstDirectRelationPawn != null && firstDirectRelationPawn.needs.mood != null)
                        {
                            pawn.relations.RemoveDirectRelation(PawnRelationDefOf.Bond, firstDirectRelationPawn);
                            firstDirectRelationPawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SoldMyBondedAnimalMood);
                        }
                    }
                }
            }
            GiveGift(pods, settlement);
        }

        public static FloatMenuAcceptanceReport CanGiveSuppliesTo(IEnumerable<IThingHolder> pods, Settlement settlement)
        {
            foreach (IThingHolder pod in pods)
            {
                ThingOwner directlyHeldThings = pod.GetDirectlyHeldThings();
                for (int i = 0; i < directlyHeldThings.Count; i++)
                {
                    Pawn p;
                    if ((p = (directlyHeldThings[i] as Pawn)) != null && p.IsQuestLodger())
                    {
                        return false;
                    }
                }
            }
            return settlement != null && settlement.Spawned && settlement.Faction != null && settlement.Faction != Faction.OfPlayer && !settlement.Faction.def.permanentEnemy && !settlement.HasMap;
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            Action<PlanetTile, TransportersArrivalAction> representative, IEnumerable<IThingHolder> pods,
            Settlement settlement)
        {
            if (settlement.Faction == Faction.OfPlayer)
            {
                return Enumerable.Empty<FloatMenuOption>();
            }
            return TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanGiveSuppliesTo(pods, settlement), () => new TransportPodsArrivalAction_GiveSupplies(settlement), "RW_GiveSuppliesViaTransportPods".Translate(settlement.Label, (FactionGiftUtility.GetGoodwillChange(pods, settlement) * 50).ToStringWithSign()), representative, settlement.Tile, delegate (Action action)
            {
                TradeRequestComp tradeReqComp = settlement.GetComponent<TradeRequestComp>();
                if (tradeReqComp != null && tradeReqComp.ActiveRequest && pods.Any((IThingHolder p) => p.GetDirectlyHeldThings().Contains(tradeReqComp.requestThingDef)))
                {
                    Find.WindowStack.Add(new Dialog_MessageBox("GiveGiftViaTransportPodsTradeRequestWarning".Translate(), "Yes".Translate(), delegate
                    {
                        action();
                    }, "No".Translate()));
                }
                else
                {
                    action();
                }
            });
        }

        public static void GiveGift(List<ActiveTransporterInfo> pods, Settlement giveTo)
        {
            int powerChange = 90 * FactionGiftUtility.GetGoodwillChange(pods.Cast<IThingHolder>(), giveTo);
            for (int i = 0; i < pods.Count; i++)
            {
                ThingOwner innerContainer = pods[i].innerContainer;
                for (int num = innerContainer.Count - 1; num >= 0; num--)
                {
                    GiveGiftInternal(innerContainer[num], innerContainer[num].stackCount, giveTo.Faction);
                    if (num < innerContainer.Count)
                    {
                        innerContainer.RemoveAt(num);
                    }
                }
            }
            RimWarSettlementComp rwsc = giveTo.GetComponent<RimWarSettlementComp>();
            if (rwsc != null)
            {
                rwsc.RimWarPoints += powerChange;
            }
        }

        private static void GiveGiftInternal(Thing thing, int count, Faction giveTo)
        {
            Thing thing2 = thing.SplitOff(count);
            Pawn pawn;
            if ((pawn = (thing2 as Pawn)) != null)
            {
                pawn.SetFaction(giveTo);
                pawn.guest.SetGuestStatus(null);
            }
            thing2.DestroyOrPassToWorld();
        }
    }
}
