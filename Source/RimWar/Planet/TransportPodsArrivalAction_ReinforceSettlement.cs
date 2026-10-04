using System;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using System.Linq;

namespace RimWar.Planet
{
    public class TransportPodsArrivalAction_ReinforceSettlement : TransportersArrivalAction
    {
        private Settlement settlement;
        private PawnsArrivalModeDef arrivalMode;

        public override bool GeneratesMap => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref arrivalMode, "arrivalMode");
        }

        public TransportPodsArrivalAction_ReinforceSettlement()
        {
        }

        public TransportPodsArrivalAction_ReinforceSettlement(Settlement _settlement, PawnsArrivalModeDef _arrivalMode)
        {
            this.settlement = _settlement;
            this.arrivalMode = _arrivalMode;
        }

        public override bool ShouldUseLongEvent(List<ActiveTransporterInfo> pods, PlanetTile tile)
        {
            return !settlement.HasMap;
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
            return CanReinforce(pods, settlement);
        }

        public static FloatMenuAcceptanceReport CanReinforce(IEnumerable<IThingHolder> pods, Settlement settlement)
        {
            if (settlement == null || !settlement.Spawned || !settlement.Attackable)
            {
                return false;
            }
            if (!TransportersArrivalActionUtility.AnyNonDownedColonist(pods))
            {
                return false;
            }
            if (settlement.EnterCooldownBlocksEntering())
            {
                return FloatMenuAcceptanceReport.WithFailReasonAndMessage("EnterCooldownBlocksEntering".Translate(), "MessageEnterCooldownBlocksEntering".Translate(settlement.EnterCooldownTicksLeft().ToStringTicksToPeriod()));
            }
            RimWarSettlementComp rwsc = settlement.GetComponent<RimWarSettlementComp>();
            if (rwsc == null || !rwsc.Reinforceable)
            {
                return false;
            }
            return true;
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            Action<PlanetTile, TransportersArrivalAction> representative, IEnumerable<IThingHolder> pods,
            Settlement settlement)
        {
            //if (representative.parent.TryGetComp<CompShuttle>() != null)
            //{
            //    foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanReinforce(pods, settlement), () => new TransportPodsArrivalAction_ReinforceSettlement(settlement, PawnsArrivalModeDefOf.Shuttle), "RW_ReinforceShuttle".Translate(settlement.Label), representative, settlement.Tile))
            //    {
            //        yield return floatMenuOption;
            //    }
            //}
            //else
            //{
            foreach (FloatMenuOption floatMenuOption2 in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanReinforce(pods, settlement), () => new TransportPodsArrivalAction_ReinforceSettlement(settlement, PawnsArrivalModeDefOf.EdgeDrop), "RW_ReinforceAndDropAtEdge".Translate(settlement.Label), representative, settlement.Tile))
            {
                yield return floatMenuOption2;
            }
            foreach (FloatMenuOption floatMenuOption3 in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanReinforce(pods, settlement), () => new TransportPodsArrivalAction_ReinforceSettlement(settlement, PawnsArrivalModeDefOf.CenterDrop), "RW_ReinforceAndDropInCenter".Translate(settlement.Label), representative, settlement.Tile))
            {
                yield return floatMenuOption3;
            }
            //}
        }

        public override void Arrived(List<ActiveTransporterInfo> pods, PlanetTile tile)
        {
            Thing lookTarget = TransportersArrivalActionUtility.GetLookTarget(pods);
            bool num = !settlement.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(settlement.Tile, null);
            TaggedString letterLabel = "LetterLabelCaravanEnteredEnemyBase".Translate();
            TaggedString letterText = "LetterTransportPodsLandedInEnemyBase".Translate(settlement.Label).CapitalizeFirst();
            //SettlementUtility.AffectRelationsOnAttacked_NewTmp(sbs, ref letterText);
            if (num)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(orGenerateMap.mapPawns.AllPawns, ref letterLabel, ref letterText, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), informEvenIfSeenBefore: true);
            }
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, lookTarget);
            arrivalMode.Worker.TravellingTransportersArrived(pods, orGenerateMap);
            RimWarSettlementComp rwsc = settlement.GetComponent<RimWarSettlementComp>();
            if (rwsc != null && rwsc.UnderAttack)
            {
                IncidentUtility.GenerateSettlementAttackers(rwsc, orGenerateMap);
            }
        }
    }
}
