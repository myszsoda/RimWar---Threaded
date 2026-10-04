using System;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using System.Linq;

namespace RimWar.Planet
{
    public class TransportPodsArrivalAction_JoinBattle : TransportersArrivalAction
    {
        private BattleSite bs;
        private PawnsArrivalModeDef arrivalMode;

        public override bool GeneratesMap => true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref bs, "bs");
            Scribe_Defs.Look(ref arrivalMode, "arrivalMode");
        }

        public override bool ShouldUseLongEvent(List<ActiveTransporterInfo> pods, PlanetTile tile)
        {
            return !bs.HasMap;
        }

        public TransportPodsArrivalAction_JoinBattle()
        {
        }

        public TransportPodsArrivalAction_JoinBattle(BattleSite _bs, PawnsArrivalModeDef _arrivalMode)
        {
            this.bs = _bs;
            this.arrivalMode = _arrivalMode;
        }

        public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, PlanetTile destinationTile)
        {
            FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
            if (!(bool)floatMenuAcceptanceReport)
            {
                return floatMenuAcceptanceReport;
            }
            if (bs != null && bs.Tile != destinationTile)
            {
                return false;
            }
            return CanAttack(pods, bs);
        }

        public static FloatMenuAcceptanceReport CanAttack(IEnumerable<IThingHolder> pods, BattleSite bs)
        {
            if (bs == null || !bs.Spawned || !bs.Attackable)
            {
                return false;
            }
            if (!TransportersArrivalActionUtility.AnyNonDownedColonist(pods))
            {
                return false;
            }
            if (bs.EnterCooldownBlocksEntering())
            {
                return FloatMenuAcceptanceReport.WithFailReasonAndMessage("EnterCooldownBlocksEntering".Translate(), "MessageEnterCooldownBlocksEntering".Translate(bs.EnterCooldownTicksLeft().ToStringTicksToPeriod()));
            }
            return true;
        }

        public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(
            Action<PlanetTile, TransportersArrivalAction> representative, IEnumerable<IThingHolder> pods, BattleSite bs)
        {
            //if (representative.parent.TryGetComp<CompShuttle>() != null)
            //{
            //    foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanAttack(pods, bs), () => new TransportPodsArrivalAction_JoinBattle(bs, PawnsArrivalModeDefOf.Shuttle), "AttackShuttle".Translate(bs.Label), representative, bs.Tile))
            //    {
            //        yield return floatMenuOption;
            //    }
            //}
            //else
            //{
            foreach (FloatMenuOption floatMenuOption2 in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanAttack(pods, bs), () => new TransportPodsArrivalAction_JoinBattle(bs, PawnsArrivalModeDefOf.EdgeDrop), "AttackAndDropAtEdge".Translate(bs.Label), representative, bs.Tile))
            {
                yield return floatMenuOption2;
            }
            foreach (FloatMenuOption floatMenuOption3 in TransportersArrivalActionUtility.GetFloatMenuOptions(() => CanAttack(pods, bs), () => new TransportPodsArrivalAction_JoinBattle(bs, PawnsArrivalModeDefOf.CenterDrop), "AttackAndDropInCenter".Translate(bs.Label), representative, bs.Tile))
            {
                yield return floatMenuOption3;
            }
            //}
        }

        public override void Arrived(List<ActiveTransporterInfo> pods, PlanetTile tile)
        {
            Thing lookTarget = TransportersArrivalActionUtility.GetLookTarget(pods);
            bool num = !bs.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(bs.Tile, null);
            TaggedString letterLabel = "LetterLabelCaravanEnteredEnemyBase".Translate();
            TaggedString letterText = "LetterTransportPodsLandedInEnemyBase".Translate(bs.Label).CapitalizeFirst();
            //SettlementUtility.AffectRelationsOnAttacked_NewTmp(sbs, ref letterText);
            if (num)
            {
                Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(orGenerateMap.mapPawns.AllPawns, ref letterLabel, ref letterText, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), informEvenIfSeenBefore: true);
            }
            Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, lookTarget);
            arrivalMode.Worker.TravellingTransportersArrived(pods, orGenerateMap);
            IncidentUtility.GenerateSiteUnits(bs, orGenerateMap);
        }
    }
}
