using System;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;
using System.Linq;

namespace RimWar.Planet
{
    public class TransportPodsArrivalAction_Shuttle_JoinBattle : TransportersArrivalAction_TransportShip
    {
        private BattleSite bs;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref bs, "bs");
        }

        public TransportPodsArrivalAction_Shuttle_JoinBattle()
        {
        }

        public TransportPodsArrivalAction_Shuttle_JoinBattle(BattleSite _bs, MapParent _mapParent)
        {
            this.bs = _bs;
            this.mapParent = _mapParent;
        }

        //public override FloatMenuAcceptanceReport StillValid(IEnumerable<IThingHolder> pods, int destinationTile)
        //{
        //    FloatMenuAcceptanceReport floatMenuAcceptanceReport = base.StillValid(pods, destinationTile);
        //    if (!(bool)floatMenuAcceptanceReport)
        //    {
        //        return floatMenuAcceptanceReport;
        //    }
        //    if (bs != null && bs.Tile != destinationTile)
        //    {
        //        return false;
        //    }
        //    return CanAttack(pods, bs);
        //}

        //public static FloatMenuAcceptanceReport CanAttack(IEnumerable<IThingHolder> pods, BattleSite bs)
        //{
        //    if (bs == null || !bs.Spawned || !bs.Attackable)
        //    {
        //        return false;
        //    }
        //    if (!TransportPodsArrivalActionUtility.AnyNonDownedColonist(pods))
        //    {
        //        return false;
        //    }
        //    if (bs.EnterCooldownBlocksEntering())
        //    {
        //        return FloatMenuAcceptanceReport.WithFailReasonAndMessage("EnterCooldownBlocksEntering".Translate(), "MessageEnterCooldownBlocksEntering".Translate(bs.EnterCooldownTicksLeft().ToStringTicksToPeriod()));
        //    }
        //    return true;
        //}

        //public static IEnumerable<FloatMenuOption> GetFloatMenuOptions(CompLaunchable representative, IEnumerable<IThingHolder> pods, BattleSite bs)
        //{
        //    if (representative.parent.TryGetComp<CompShuttle>() != null)
        //    {
        //        foreach (FloatMenuOption floatMenuOption in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanAttack(pods, bs), () => new TransportPodsArrivalAction_JoinBattle(bs, PawnsArrivalModeDefOf.Shuttle), "AttackShuttle".Translate(bs.Label), representative, bs.Tile))
        //        {
        //            yield return floatMenuOption;
        //        }
        //    }
        //    else
        //    {
        //        foreach (FloatMenuOption floatMenuOption2 in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanAttack(pods, bs), () => new TransportPodsArrivalAction_JoinBattle(bs, PawnsArrivalModeDefOf.EdgeDrop), "AttackAndDropAtEdge".Translate(bs.Label), representative, bs.Tile))
        //        {
        //            yield return floatMenuOption2;
        //        }
        //        foreach (FloatMenuOption floatMenuOption3 in TransportPodsArrivalActionUtility.GetFloatMenuOptions(() => CanAttack(pods, bs), () => new TransportPodsArrivalAction_JoinBattle(bs, PawnsArrivalModeDefOf.CenterDrop), "AttackAndDropInCenter".Translate(bs.Label), representative, bs.Tile))
        //        {
        //            yield return floatMenuOption3;
        //        }
        //    }
        //}

        public override void Arrived(List<ActiveTransporterInfo> pods, PlanetTile tile)
        {
            //Thing lookTarget = TransportPodsArrivalActionUtility.GetLookTarget(pods);
            if (transportShip == null || transportShip.Disposed)
            {
                Log.Error("Trying to arrive in a null or disposed transport ship.");
                return;
            }
            bool flag_hasMap = !this.bs.HasMap;
            Map orGenerateMap = GetOrGenerateMapUtility.GetOrGenerateMap(bs.Tile, null);
            if (!cell.IsValid)
            {
                cell = DropCellFinder.GetBestShuttleLandingSpot(orGenerateMap, Faction.OfPlayer);
            }
            LookTargets lookTargets = new LookTargets(cell, orGenerateMap);
            if (!cell.IsValid)
            {
                Log.Error("Could not find cell for transport ship arrival.");
                return;
            }
            Settlement settlement;
            if ((settlement = (orGenerateMap.Parent as Settlement)) != null && settlement.Faction != Faction.OfPlayer)
            {
                TaggedString letterLabel = "LetterLabelCaravanEnteredEnemyBase".Translate();
                TaggedString letterText = "LetterTransportPodsLandedInEnemyBase".Translate(bs.Label).CapitalizeFirst();
                //SettlementUtility.AffectRelationsOnAttacked_NewTmp(sbs, ref letterText);
                if (flag_hasMap)
                {
                    Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
                    PawnRelationUtility.Notify_PawnsSeenByPlayer_Letter(orGenerateMap.mapPawns.AllPawns, ref letterLabel, ref letterText, "LetterRelatedPawnsInMapWherePlayerLanded".Translate(Faction.OfPlayer.def.pawnsPlural), informEvenIfSeenBefore: true);
                }
                Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, lookTargets);
            }
            for (int i = 0; i < pods.Count; i++)
            {
                transportShip.TransporterComp.innerContainer.TryAddRangeOrTransfer(pods[i].innerContainer, canMergeWithExistingStacks: true, destroyLeftover: true);
            }
            transportShip.ArriveAt(cell, mapParent);
            //foreach (ActiveDropPodInfo pod in pods)
            //{

            //    pod.missionShuttleHome = missionShuttleHome;
            //    pod.missionShuttleTarget = missionShuttleTarget;
            //    pod.sendAwayIfQuestFinished = sendAwayIfQuestFinished;
            //    pod.questTags = questTags;
            //}
            //PawnsArrivalModeDefOf.Shuttle.Worker.TravelingTransportPodsArrived(pods, orGenerateMap);
            Messages.Message("MessageShuttleArrived".Translate(), lookTargets, MessageTypeDefOf.TaskCompletion);
            IncidentUtility.GenerateSiteUnits(bs, orGenerateMap);
        }
    }
}
