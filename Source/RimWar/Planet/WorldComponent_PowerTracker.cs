using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using RimWar;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWar.Options;
using RimWar.Threading;
//using FactionColonies;

namespace RimWar.Planet
{
    public class WorldComponent_PowerTracker : WorldComponent
    {
        //Historic Variables
        public int objectsCreated = 0;
        public int creationAttempts = 0;
        public int settlementSearches = 0;
        public int globalActions = 0;

        //Do Once per load
        private bool factionsLoaded = false;
        private int nextEvaluationTick = 20;
        public Faction victoryFaction = null;
        private bool victoryDeclared = false;
        private bool rwdInitialized = false;
        private bool rwdInitVictory = false;

        //Stored variables
        public List<CaravanTargetData> caravanTargetData;

        public int preventActionsAgainstPlayerUntilTick = 60000;
        public int minimumHeatForPlayerAction = 0;

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            // Jobs scheduled for a previous game must never commit into this one.
            JobScheduler.ResetForNewGame();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.rwdInitialized, "rwdInitialized", false, false);
            Scribe_Values.Look<bool>(ref this.rwdInitVictory, "rwdInitVictory", false, false);
            Scribe_Values.Look<bool>(ref this.victoryDeclared, "victoryDeclared", false, false);
            Scribe_Values.Look<int>(ref this.objectsCreated, "objectsCreated", 0, false);
            Scribe_Values.Look<int>(ref this.creationAttempts, "creationAttewmpts", 0, false);
            Scribe_Values.Look<int>(ref this.settlementSearches, "settlementSearches", 0, false);
            Scribe_Values.Look<int>(ref this.globalActions, "globalActions", 0, false);
            Scribe_Values.Look<int>(ref this.minimumHeatForPlayerAction, "minimumHeatForPlayerAction", 0, false);
            Scribe_References.Look<Faction>(ref this.victoryFaction, "victoryFaction");
            Scribe_Collections.Look<RimWarData>(ref this.rimwarData, "rimwarData", LookMode.Deep);//, new object[0]);
            Scribe_Collections.Look<CaravanTargetData>(ref this.caravanTargetData, "caravanTargetData", LookMode.Deep);

            bool flagLoad = Scribe.mode == LoadSaveMode.PostLoadInit;
            if (flagLoad)
            {
                Utility.RimWar_DebugToolsPlanet.ValidateAndResetSettlements();
                WorldUtility.ValidateFactions(true);
            }
        }

        private List<WarObject> allWarObjects;
        public List<WarObject> AllWarObjects
        {
            get
            {
                bool flag = allWarObjects == null;
                if (flag)
                {
                    allWarObjects = new List<WarObject>();
                    allWarObjects.Clear();
                }
                return allWarObjects;
            }
            set
            {
                bool flag = allWarObjects == null;
                if (flag)
                {
                    allWarObjects = new List<WarObject>();
                    allWarObjects.Clear();
                }
                this.allWarObjects = value;
            }
        }

        public List<RimWarSettlementComp> AllRimWarSettlements
        {
            get
            {
                return WorldUtility.GetRimWarSettlements(this.RimWarData);
            }
        }

        private List<WorldObject> worldObjects;
        public List<WorldObject> WorldObjects
        {
            get
            {
                bool flag = worldObjects == null;
                if (flag)
                {
                    worldObjects = new List<WorldObject>();
                    worldObjects.Clear();
                }
                return this.worldObjects;
            }
            set
            {
                bool flag = worldObjects == null;
                if (flag)
                {
                    worldObjects = new List<WorldObject>();
                    worldObjects.Clear();
                }
                this.worldObjects = value;
            }
        }

        private List<RimWarData> rimwarData;
        public List<RimWarData> RimWarData
        {
            get
            {
                bool flag = this.rimwarData == null;
                if (flag)
                {
                    this.rimwarData = new List<RimWarData>();
                }
                return this.rimwarData;
            }
            set
            {
                this.rimwarData.Clear();
                foreach (RimWarData rwd in value)
                {
                    rimwarData.Add(rwd);
                }
            }
        }

        public List<RimWarData> Active_RWD
        {
            get
            {
                return RimWarData.Where((RimWarData x) => x.behavior != RimWarBehavior.Excluded).ToList();
            }
        }

        public WorldComponent_PowerTracker(World world) : base(world)
        {
        }

        private bool doOnce = false;
        public override void WorldComponentTick()
        {
            //DrawOverlays();
            int currentTick = Find.TickManager.TicksGame;
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            // Apply the results of jobs scheduled CommitDelayTicks ago before this tick's logic reads the world.
            JobScheduler.CommitDue();
            if (!doOnce)
            {
                if (this.victoryFaction != null)
                {
                    this.victoryFaction.allowGoodwillRewards = false;
                    //this.victoryFaction.def.naturalColonyGoodwill = new IntRange(-100, -100);
                }
                doOnce = true;
            }
            if (currentTick >= 10 && !this.rwdInitialized)
            {
                Initialize();
                this.rwdInitialized = true;
            }
            if (currentTick % 60 == 0)
            {
                AdjustCaravanTargets();
            }
            if (currentTick % (int)settingsRef.heatFrequency == 0)
            {
                UpdatePlayerAggression();
            }
            if (currentTick % settingsRef.rwdUpdateFrequency == 0)
            {
                // Update existing factions before update
                CheckForNewFactions();

                // Game thread: changes settlement points, faction relations, letters and the victory dialog.
                UpdateFactions();
                if (settingsRef.useRimWarVictory && !victoryDeclared && this.rwdInitVictory)
                {
                    CheckVictoryConditions();
                }
            }
            if (currentTick % 60000 == 0)
            {
                DoGlobalRWDAction();
            }
            if (currentTick >= this.nextEvaluationTick)
            {
                this.nextEvaluationTick = currentTick + Rand.Range((int)(settingsRef.averageEventFrequency * .5f), (int)(settingsRef.averageEventFrequency * 1.5f));

                if (this.Active_RWD != null && this.Active_RWD.Count > 0)
                {
                    RimWarData rwd = this.Active_RWD.RandomElement();
                    if (rwd.WorldSettlements != null && rwd.WorldSettlements.Count > 0)
                    {
                        RimWorld.Planet.Settlement settlement = rwd.WorldSettlements.RandomElement();
                        if (WorldUtility.IsValidSettlement(settlement))
                        {
                            RimWarSettlementComp rwsComp = settlement.GetComponent<RimWarSettlementComp>();
                            if (rwsComp != null)
                            {
                                if (rwsComp.nextEventTick - currentTick > settingsRef.settlementEventDelay * 2)
                                {
                                    rwsComp.nextEventTick = currentTick;
                                }
                                if (rwd.behavior != RimWarBehavior.Player && rwd.behavior != RimWarBehavior.Vassal && rwsComp.nextEventTick <= currentTick && ((!CaravanNightRestUtility.RestingNowAt(rwsComp.parent.Tile) && !rwd.movesAtNight) || (CaravanNightRestUtility.RestingNowAt(rwsComp.parent.Tile) && rwd.movesAtNight)))
                                {
                                    if (rwd.rwdNextUpdateTick < currentTick)
                                    {
                                        rwd.rwdNextUpdateTick = currentTick + settingsRef.rwdUpdateFrequency;
                                        WorldUtility.UpdateRWDSettlementLists(rwd);
                                    }

                                    RimWarAction newAction = rwd.GetWeightedSettlementAction();
                                    if (rwd.IsAtWar && !(newAction == RimWarAction.LaunchedWarband || newAction == RimWarAction.ScoutingParty || newAction == RimWarAction.Warband))
                                    {
                                        newAction = rwd.GetWeightedSettlementAction();
                                    }

                                    if (newAction != RimWarAction.None)
                                    {
                                        this.objectsCreated++;
                                        if (Rand.Chance(.02f))
                                        {
                                            rwsComp.RimWarPoints += Rand.Range(10, 100);
                                            this.globalActions++;
                                        }
                                        //bool requestedReinforcement = false;
                                        if (rwsComp.ShouldRequestReinforcements && rwsComp.CanReinforce)
                                        {
                                            List<Settlement> reinforcementSettlements = rwsComp.NearbyFriendlySettlementsWithinRange(50);
                                            if (reinforcementSettlements != null && reinforcementSettlements.Count > 0)
                                            {
                                                foreach (Settlement s in reinforcementSettlements)
                                                {
                                                    RimWarSettlementComp rwsc = s.GetComponent<RimWarSettlementComp>();
                                                    if (rwsc != null && !rwsc.ShouldRequestReinforcements && rwsc.RimWarPoints > 1000)
                                                    {
                                                        //Log.Message("" + settlement.Label + " requested reinforcements from " + s.Label);
                                                        AttemptReinforcement(rwd, s, rwsc, settlement);
                                                        rwsComp.lastReinforcementTick = Find.TickManager.TicksGame;
                                                        break;
                                                        //requestedReinforcement = true;
                                                    }
                                                }
                                            }
                                        }
                                        if (true)//!requestedReinforcement)
                                        {
                                            if (newAction == RimWarAction.Caravan)
                                            {
                                                AttemptTradeMission(rwd, settlement, rwsComp);
                                            }
                                            else if (newAction == RimWarAction.Diplomat)
                                            {
                                                if (settingsRef.createDiplomats)
                                                {
                                                    AttemptDiplomatMission(rwd, settlement, rwsComp);
                                                }
                                                else
                                                {
                                                    this.creationAttempts++;
                                                }
                                            }
                                            else if (newAction == RimWarAction.LaunchedWarband)
                                            {
                                                AttemptLaunchedWarbandAgainstTown(rwd, settlement, rwsComp);
                                            }
                                            else if (newAction == RimWarAction.ScoutingParty)
                                            {
                                                AttemptScoutMission(rwd, settlement, rwsComp);
                                            }
                                            else if (newAction == RimWarAction.Settler && rwd.WorldSettlements.Count < settingsRef.maxFactionSettlements)
                                            {
                                                AttemptSettlerMission(rwd, settlement, rwsComp);
                                            }
                                            else if (newAction == RimWarAction.Warband)
                                            {
                                                AttemptWarbandActionAgainstTown(rwd, settlement, rwsComp);
                                            }
                                            else
                                            {
                                                if (rwd.WorldSettlements.Count >= settingsRef.maxFactionSettlements)
                                                {
                                                    if (Rand.Chance(.3f))
                                                    {
                                                        AttemptTradeMission(rwd, settlement, rwsComp);
                                                    }
                                                }
                                                else
                                                {
                                                    Log.Warning("attempted to generate undefined RimWar settlement action");
                                                }
                                            }
                                        }
                                        if (rwsComp.isCapitol)
                                        {
                                            rwsComp.nextEventTick = currentTick + Mathf.RoundToInt((float)settingsRef.settlementEventDelay / 1.5f);
                                        }
                                        else
                                        {
                                            rwsComp.nextEventTick = currentTick + settingsRef.settlementEventDelay; //one day (60000) default
                                        }
                                    }
                                    else
                                    {
                                        this.creationAttempts++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // Hand this tick's jobs (from world-object ticks and the logic above) to the worker, or compute them inline.
            JobScheduler.Dispatch(this);
            base.WorldComponentTick();
        }

        public List<CaravanTargetData> GetCaravaTargetData
        {
            get
            {
                if (this.caravanTargetData == null)
                {
                    this.caravanTargetData = new List<CaravanTargetData>();
                    this.caravanTargetData.Clear();
                }
                return this.caravanTargetData;
            }
        }

        public void RemoveCaravanTarget(WorldObject wo)
        {
            if (this.caravanTargetData == null || this.caravanTargetData.Count <= 0)
            {
                return;
            }
            for (int i = 0; i < this.caravanTargetData.Count; i++)
            {
                CaravanTargetData ctd = caravanTargetData[i];
                if (ctd.IsValid())
                {
                    if (ctd.caravan == wo || ctd.caravanTarget == wo)
                    {
                        this.caravanTargetData.Remove(ctd);
                        break;
                    }
                }
                else
                {
                    this.caravanTargetData.Remove(ctd);
                    break;
                }
            }
        }

        public void AdjustCaravanTargets()
        {
            if (this.caravanTargetData == null || this.caravanTargetData.Count <= 0)
            {
                return;
            }
            for (int i = 0; i < this.caravanTargetData.Count; i++)
            {
                CaravanTargetData ctd = caravanTargetData[i];
                if (ctd.IsValid())
                {
                    if (!ctd.caravanTarget.interactable)
                    {
                        //Messages.Message("No longer able to interact with " + ctd.caravanTarget.Name + " - clearing target.", MessageTypeDefOf.RejectInput);
                        this.caravanTargetData.Remove(ctd);
                    }
                    if (ctd.CaravanTargetTile.Valid && ctd.CaravanDestination != ctd.CaravanTargetTile)
                    {
                        ctd.caravan.pather.StartPath(ctd.CaravanTargetTile, ctd.caravan.pather.ArrivalAction, true);
                    }
                }
                else
                {
                    this.caravanTargetData.Remove(ctd);
                    break;
                }
            }
        }

        public void AssignCaravanTargets(Caravan caravan, WarObject warObject)
        {
            if (this.caravanTargetData == null)
            {
                this.caravanTargetData = new List<CaravanTargetData>();
                this.caravanTargetData.Clear();
            }
            for (int i = 0; i < this.caravanTargetData.Count; i++)
            {
                if (caravanTargetData[i].caravan == caravan)
                {
                    RemoveCaravanTarget(caravan);
                }
            }
            CaravanTargetData ctd = new CaravanTargetData();
            caravan.GetComponent<RimWarCaravanComp>().currentTarget = warObject;
            ctd.caravan = caravan;
            ctd.caravanTarget = warObject;
            this.caravanTargetData.Add(ctd);
        }

        private static int CountOnPlanetFactions()
        {
            List<Faction> factionList = Find.World.factionManager.AllFactionsVisible.ToList();
            int count = factionList.Count;

            for (int i = 0; i < factionList.Count; i++)
            {
                /* AllFactionsVisible does ignore standard hidden factions,
                 * but Traders Guild (guys in space) are not hidden,
                 * but outside standard calculations.
                 * So we need to ignore them manually.
                 */
                if (factionList[i] != Faction.OfTradersGuild)
                {
                    continue;
                }
                count--;
            }

            return count;
        }

        private void CheckForNewFactions()
        {
            if (WorldComponent_PowerTracker.CountOnPlanetFactions() > this.RimWarData.Count)
            {
                Utility.RimWar_DebugToolsPlanet.ResetFactions(false, true);
            }
        }

        public void DoGlobalRWDAction()
        {
            RimWarData rwd = this.Active_RWD.RandomElement();
            if (rwd == null)
            {
                return;
            }
            if (rwd.behavior != RimWarBehavior.Player && rwd.behavior != RimWarBehavior.Vassal && rwd.behavior != RimWarBehavior.Excluded)
            {
                this.globalActions++;
                RimWarAction newAction = rwd.GetWeightedSettlementAction();
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                if (newAction == RimWarAction.Caravan)
                {
                    RimWorld.Planet.Settlement settlement = rwd.WorldSettlements.RandomElement();
                    RimWarSettlementComp rwsc = settlement != null ? settlement.GetComponent<RimWarSettlementComp>() : null;
                    if (rwsc != null)
                    {
                        rwsc.RimWarPoints += Rand.Range(100, 200);
                    }
                }
                else if (newAction == RimWarAction.Diplomat)
                {
                    if (settingsRef.createDiplomats)
                    {
                        RimWorld.Planet.Settlement settlement = rwd.WorldSettlements.RandomElement();
                        RimWarSettlementComp rwdSettlement = settlement != null ? settlement.GetComponent<RimWarSettlementComp>() : null;
                        RimWarData playerRwd = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer);
                        RimWorld.Planet.Settlement rwdPlayerSettlement = playerRwd != null ? playerRwd.WorldSettlements.RandomElement() : null;
                        if (rwdSettlement != null && rwdPlayerSettlement != null)
                        {
                            WorldUtility.CreateDiplomat(WorldUtility.CalculateDiplomatPoints(rwdSettlement), rwd, rwdSettlement.parent as RimWorld.Planet.Settlement, rwdSettlement.parent.Tile, rwdPlayerSettlement, WorldObjectDefOf.Settlement);
                        }
                    }
                }
                else if (newAction == RimWarAction.LaunchedWarband)
                {
                    RimWorld.Planet.Settlement settlement = rwd.WorldSettlements.RandomElement();
                    RimWarSettlementComp rwdTown = settlement != null ? settlement.GetComponent<RimWarSettlementComp>() : null;
                    RimWarData playerRwd = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer);
                    RimWorld.Planet.Settlement rwdPlayerSettlement = playerRwd != null ? playerRwd.WorldSettlements.RandomElement() : null;
                    if (rwdTown != null && rwdPlayerSettlement != null && rwd.RimWarFaction != null && rwd.RimWarFaction.HostileTo(Faction.OfPlayerSilentFail) && rwd.CanLaunch)
                    {
                        int pts = WorldUtility.CalculateWarbandPointsForRaid(rwdPlayerSettlement.GetComponent<RimWarSettlementComp>());
                        if (rwd.behavior == RimWarBehavior.Cautious)
                        {
                            pts = Mathf.RoundToInt(pts * 1.1f);
                        }
                        else if (rwd.behavior == RimWarBehavior.Warmonger)
                        {
                            pts = Mathf.RoundToInt(pts * 1.25f);
                        }

                        float maxCommitment = rwd.IsAtWarWith(Faction.OfPlayer) ? .8f : .6f;
                        if (pts <= (maxCommitment * rwdTown.RimWarPoints))
                        {
                            WorldUtility.CreateLaunchedWarband(pts, rwd, rwdTown.parent as RimWorld.Planet.Settlement, rwdTown.parent.Tile, rwdPlayerSettlement, WorldObjectDefOf.Settlement);
                        }
                    }
                }
                else if (newAction == RimWarAction.ScoutingParty)
                {
                    RimWorld.Planet.Settlement settlement = rwd.WorldSettlements.RandomElement();
                    RimWarSettlementComp rwdTown = settlement != null ? settlement.GetComponent<RimWarSettlementComp>() : null;
                    RimWarData playerRwd = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer);
                    RimWorld.Planet.Settlement rwdPlayerSettlement = playerRwd != null ? playerRwd.WorldSettlements.RandomElement() : null;
                    if (rwdTown != null && rwdPlayerSettlement != null && Find.WorldGrid.ApproxDistanceInTiles(rwdTown.parent.Tile, rwdPlayerSettlement.Tile) < 120)
                    {
                        int pts = WorldUtility.CalculateScoutMissionPoints(rwd, rwdPlayerSettlement.GetComponent<RimWarSettlementComp>().RimWarPoints);
                        if (pts < (.8f * rwdTown.RimWarPoints))
                        {
                            WorldUtility.CreateScout(pts, rwd, rwdTown.parent as RimWorld.Planet.Settlement, rwdTown.parent.Tile, rwdPlayerSettlement, WorldObjectDefOf.Settlement);
                        }
                    }
                }
                else if (newAction == RimWarAction.Settler)
                {
                    int factionAdjustment = Rand.Range(0, 20);
                    RimWarData rwdSecond = this.Active_RWD.RandomElement();
                    if (rwdSecond != null && rwd.RimWarFaction != null && rwdSecond.RimWarFaction != rwd.RimWarFaction && rwdSecond.RimWarFaction != Faction.OfPlayerSilentFail)
                    {
                        rwd.RimWarFaction.TryAffectGoodwillWith(rwdSecond.RimWarFaction, factionAdjustment, true, true, null, null);
                    }
                }
                else if (newAction == RimWarAction.Warband)
                {
                    int factionAdjustment = Rand.Range(-20, 0);
                    RimWarData rwdSecond = this.Active_RWD.RandomElement();
                    if (rwdSecond != null && rwd.RimWarFaction != null && rwdSecond.RimWarFaction != rwd.RimWarFaction && rwdSecond.RimWarFaction != Faction.OfPlayerSilentFail)
                    {
                        rwd.RimWarFaction.TryAffectGoodwillWith(rwdSecond.RimWarFaction, factionAdjustment, true, true, null, null);
                    }
                }
                else
                {
                    Log.Warning("attempted to generate undefined RimWar settlement action");
                }
            }
        }

        public void Initialize()
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            if (!factionsLoaded)
            {
                if (settingsRef.randomizeFactionBehavior)
                {
                    RimWarFactionUtility.RandomizeAllFactionRelations();
                }
                //WorldUtility.ValidateFactions(true);
                List<Faction> rimwarFactions = new List<Faction>();
                rimwarFactions.Clear();
                this.preventActionsAgainstPlayerUntilTick = (int)(90000 / Find.Storyteller.difficulty.threatScale);
                for (int i = 0; i < RimWarData.Count; i++)
                {
                    rimwarFactions.Add(RimWarData[i].RimWarFaction);
                }
                List<Faction> allFactionsVisible = world.factionManager.AllFactionsVisible.ToList();
                if (allFactionsVisible != null && allFactionsVisible.Count > 0)
                {
                    for (int i = 0; i < allFactionsVisible.Count; i++)
                    {
                        bool duplicate = false;
                        for (int k = 0; k < rimwarFactions.Count; k++)
                        {
                            if (allFactionsVisible[i].randomKey == rimwarFactions[k].randomKey)
                            {
                                duplicate = true;
                            }
                        }
                        if (!duplicate)
                        {
                            AddRimWarFaction(allFactionsVisible[i]);
                        }

                        if (settingsRef.playerVS && allFactionsVisible[i] != Faction.OfPlayer)
                        {
                            allFactionsVisible[i].TryAffectGoodwillWith(Faction.OfPlayer, -80, true, true, RimWarDefOf.RW_DiplomacyAction, null);
                            for (int j = 0; j < 5; j++)
                            {
                                Faction otherFaction = allFactionsVisible.RandomElement();
                                if (otherFaction != allFactionsVisible[i] && otherFaction != Faction.OfPlayer)
                                {
                                    allFactionsVisible[i].TryAffectGoodwillWith(otherFaction, 50, true, true, RimWarDefOf.RW_DiplomacyAction, null);
                                }
                            }
                            if (allFactionsVisible[i].PlayerGoodwill <= -80)
                            {
                                RimWarFactionUtility.DeclareWarOn(allFactionsVisible[i], Faction.OfPlayer);
                            }
                        }
                    }
                }
                this.factionsLoaded = true;
            }
            if (settingsRef.useRimWarVictory && this.victoryFaction == null)
            {
                GetFactionForVictoryChallenge();
            }
            this.rwdInitVictory = settingsRef.useRimWarVictory;
        }

        public void CheckVictoryConditions()
        {
            GetFactionForVictoryChallenge();
            CheckVictoryFactionForDefeat();
        }

        public void CheckVictoryFactionForDefeat()
        {
            List<RimWorld.Planet.Settlement> rivalBases = new List<RimWorld.Planet.Settlement>();
            rivalBases.Clear();
            List<RimWorld.Planet.Settlement> allBases = Find.World.worldObjects.SettlementBases;
            if (allBases != null && allBases.Count > 0)
            {
                for (int i = 0; i < allBases.Count; i++)
                {
                    if (allBases[i].Faction == this.victoryFaction)
                    {
                        rivalBases.Add(allBases[i]);
                    }
                }
            }
            if (rivalBases.Count <= 0)
            {
                this.victoryDeclared = true;
                AnnounceVictory();
            }
        }

        private void AnnounceVictory()
        {
            GenGameEnd.EndGameDialogMessage("RW_VictoryAchieved".Translate(this.victoryFaction));
        }

        private void GetFactionForVictoryChallenge()
        {
            if (this.victoryFaction != null)
            {
                return;
            }
            if (Active_RWD == null || Active_RWD.Count <= 0)
            {
                return;
            }
            List<Faction> potentialFactions = new List<Faction>();
            potentialFactions.Clear();
            for (int i = 0; i < Active_RWD.Count; i++)
            {
                if (Settings.Instance.factionDefForRival == null)
                {
                    if (!Active_RWD[i].RimWarFaction.def.hidden && Active_RWD[i].RimWarFaction.def.humanlikeFaction && Active_RWD[i].RimWarFaction != Faction.OfPlayer && !WorldUtility.IsVassalFaction(Active_RWD[i].RimWarFaction))
                    {
                        potentialFactions.Add(Active_RWD[i].RimWarFaction);
                    }
                }
                else
                {
                    if (Active_RWD[i].RimWarFaction.def.defName == Settings.Instance.factionDefForRival.defName)
                    {
                        potentialFactions.Add(Active_RWD[i].RimWarFaction);
                    }
                }
            }
            if (potentialFactions.Count > 0)
            {
                this.victoryFaction = potentialFactions.RandomElement();
                //this.victoryFaction.def.naturalColonyGoodwill = new IntRange(-100, -100);
                List<RimWorld.Planet.Settlement> wosList = WorldUtility.GetRimWarDataForFaction(this.victoryFaction).WorldSettlements;
                if (wosList != null)
                {
                    for (int j = 0; j < wosList.Count; j++)
                    {
                        RimWarSettlementComp rwsc = wosList[j].GetComponent<RimWarSettlementComp>();
                        if (rwsc != null)
                        {
                            rwsc.RimWarPoints = Mathf.RoundToInt(rwsc.RimWarPoints * Rand.Range(1.3f, 1.8f));
                        }
                    }
                }
            }
            else
            {
                this.victoryFaction = Active_RWD.RandomElement().RimWarFaction;
            }
            Find.LetterStack.ReceiveLetter("RW_VictoryChallengeLabel".Translate(), "RW_VictoryChallengeMessage".Translate(this.victoryFaction.Name), LetterDefOf.ThreatBig);
        }

        public void UpdatePlayerAggression()
        {
            this.minimumHeatForPlayerAction = Mathf.Clamp(this.minimumHeatForPlayerAction - Rand.RangeInclusive(1, 2), 0, 10000);
        }

        private void CheckFactionDefeated()
        {
            /* This shouldn't be needed as Rimworld should mark faction as
             * defeated by itself, but this does not seem to be a case.
             * Usually faction can be only defeated by player destroying last
             * settlement, but this will happen without player intervention.
             * As such, mark faction as defeated ourselves if not already.
             */
            List<Faction> factionList = Find.World.factionManager.AllFactionsVisible.ToList();

            for (int i = 0; i < factionList.Count; i++)
            {
                /* TODO Actually check both layers, but traders can be killed only by player currently.
                 * TODO This will also defeat "Refugee faction" but I find it neat,
                 *      them coming to player defeated. Sadly RimWorld itself does not
                 *      have pretty check for these factions.
                 *      But need to fix this.
                 */
                if (factionList[i].IsPlayer || factionList[i] == Faction.OfTradersGuild || factionList[i].temporary)
                    continue;

                if (!Find.WorldObjects.AnyFactionSettlementOnRootSurface(factionList[i]))
                {
                    // Check for active parties
                    List<WarObject> wosList = WorldUtility.GetRimWarDataForFaction(factionList[i]).FactionUnits;
                    if (wosList != null && wosList.Count > 0)
                        continue;

                    if (factionList[i].defeated)
                        continue;

                    factionList[i].defeated = true;

                    Find.LetterStack.ReceiveLetter(
                        // TODO add translation
                        "Faction destroyed",
                        string.Format("All settlements of {0} have been destroyed.", factionList[i].Name),
                        LetterDefOf.PositiveEvent
                    );
                }
            }
        }

        public void UpdateFactions()
        {
            CheckFactionDefeated();
            IncrementSettlementGrowth();
            ReconstituteSettlements();
            UpdateFactionSettlements(this.RimWarData.RandomElement());
        }

        public void IncrementSettlementGrowth()
        {
            Options.SettingsRef settingsref = new Options.SettingsRef();
            for (int i = 0; i < this.RimWarData.Count; i++)
            {
                RimWarData rwd = RimWarData[i];
                if (rwd.behavior != RimWarBehavior.Player && rwd.behavior != RimWarBehavior.Excluded)
                {
                    float mult = (settingsref.rwdUpdateFrequency / 10000f);  //default update frequency is 2500, mult = .25f
                    if (rwd.behavior == RimWarBehavior.Expansionist)
                    {
                        mult *= 1.1f;
                    }
                    for (int j = 0; j < rwd.WorldSettlements.Count; j++)
                    {
                        RimWarSettlementComp rwdTown = rwd.WorldSettlements[j].GetComponent<RimWarSettlementComp>();
                        if (rwdTown != null)
                        {
                            int maxPts = 50000;

                            if (rwdTown.parent.def.defName == "City_Citadel")
                            {
                                maxPts += 5000;
                                mult += .02f;
                            }
                            if (rwdTown.isCapitol)
                            {
                                if (rwd.behavior == RimWarBehavior.Vassal)
                                {
                                    maxPts += 1000;
                                    mult += .05f;
                                }
                                else
                                {
                                    maxPts += 5000;
                                    mult += .075f;
                                }
                            }
                            if (rwdTown.PointDamage > 0)
                            {
                                float healAdjustment = (Rand.Range(.005f, .01f) * rwdTown.RimWarPoints);
                                rwdTown.PointDamage = Mathf.RoundToInt(Mathf.Clamp(rwdTown.PointDamage - (2 * healAdjustment), 0, rwdTown.RimWarPoints));
                                rwdTown.RimWarPoints = Mathf.RoundToInt(Mathf.Clamp(rwdTown.RimWarPoints - healAdjustment, 0, rwdTown.RimWarPoints));
                            }
                            else
                            {
                                if (rwdTown.RimWarPoints <= maxPts)
                                {
                                    float pts = (Rand.Range(2f, 3f)) + WorldUtility.GetBiomeMultiplier(Find.WorldGrid[rwdTown.parent.Tile].PrimaryBiome); //.1f - 3.5f
                                    pts = pts * mult * WorldUtility.GetFactionTechLevelMultiplier(rwd.RimWarFaction) * rwd.growthAttribute * settingsref.settlementGrowthRate;
                                    rwdTown.RimWarPoints += Mathf.RoundToInt(Mathf.Clamp(pts, 1f, 100f));
                                    if (rwdTown.bonusGrowthCount > 0)
                                    {
                                        rwdTown.RimWarPoints += 10;
                                        rwdTown.bonusGrowthCount--;
                                    }
                                }
                            }
                            if (rwdTown.PlayerHeat < 10000)
                            {
                                rwdTown.PlayerHeat += Rand.Range(1, 3);
                                if (rwdTown.isCapitol)
                                {
                                    rwdTown.PlayerHeat += Rand.Range(1, 3);
                                }
                            }
                            if (rwd.behavior == RimWarBehavior.Vassal)
                            {
                                rwdTown.vassalHeat = Mathf.Clamp(rwdTown.vassalHeat - Rand.RangeInclusive(1, 3), 0, 10000);
                            }
                        }
                    }
                }

                if (rwd.behavior == RimWarBehavior.Vassal)
                {
                    foreach (Faction f in Find.FactionManager.AllFactionsVisible)
                    {
                        if (f == rwd.RimWarFaction || f == Faction.OfPlayer)
                        {
                            continue;
                        }
                        FactionRelation frPlayer = Faction.OfPlayer.RelationWith(f);
                        FactionRelation frVassal = rwd.RimWarFaction.RelationWith(f);
                        FactionRelationKind frVassalKind = frVassal.kind;
                        if (frPlayer != null && frPlayer.kind != frVassalKind)
                        {
                            rwd.RimWarFaction.SetRelation(frPlayer);
                            rwd.RimWarFaction.Notify_RelationKindChanged(f, frVassalKind, false, "Vassal Alignment", rwd.GetCapitol, out _);
                        }
                    }
                }
            }
        }

        public void ReconstituteSettlements()
        {
            for (int i = 0; i < this.RimWarData.Count; i++)
            {
                RimWarData rwd = RimWarData[i];
                if (rwd.WorldSettlements != null && rwd.WorldSettlements.Count > 0)
                {
                    for (int j = 0; j < rwd.WorldSettlements.Count; j++)
                    {
                        RimWarSettlementComp rwdTown = rwd.WorldSettlements[j].GetComponent<RimWarSettlementComp>();
                        if (rwdTown != null && rwdTown.SettlementPointGains != null && rwdTown.SettlementPointGains.Count > 0)
                        {
                            for (int k = 0; k < rwdTown.SettlementPointGains.Count; k++)
                            {
                                if (rwdTown.SettlementPointGains[k].delay <= Find.TickManager.TicksGame)
                                {
                                    rwdTown.RimWarPoints += rwdTown.SettlementPointGains[k].points;
                                    rwdTown.SettlementPointGains.Remove(rwdTown.SettlementPointGains[k]);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public void AddRimWarFaction(Faction faction)
        {
            if (CheckForRimWarFaction(faction))
            {
                return;
            }
            RimWarData newRimWarFaction = new RimWarData(faction);
            if (faction != null)
            {
                GenerateFactionBehavior(newRimWarFaction);
                AssignFactionSettlements(newRimWarFaction);
            }
            _ = newRimWarFaction.GetCapitol;
            this.RimWarData.Add(newRimWarFaction);
        }

        private bool CheckForRimWarFaction(Faction faction)
        {
            if (this.RimWarData == null)
            {
                return false;
            }
            for (int i = 0; i < this.RimWarData.Count; i++)
            {
                //Log.Message("checking faction " + faction + " against rwd faction: " + this.RimWarData[i].RimWarFaction);
                if (RimWarData[i].RimWarFaction == null)
                {
                    continue;
                }
                if (RimWarData[i].RandomKey == faction.randomKey)
                {
                    return true;
                }
                else if (RimWarData[i].RimWarFaction.HasName && RimWarData[i].RimWarFactionKey == (faction.Name + faction.randomKey).ToString())
                {
                    //Log.Message("faction names same, factiond different");
                    RimWarData[i].RimWarFaction = faction;
                    return true;
                }
            }
            return false;
        }

        private void GenerateFactionBehavior(RimWarData rimwarObject)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();

            bool factionFound = false;
            List<RimWarDef> rwd = DefDatabase<RimWarDef>.AllDefsListForReading;
            //IEnumerable<RimWarDef> enumerable = from def in DefDatabase<RimWarDef>.AllDefs
            //                                    select def;
            //Log.Message("enumerable count is " + enumerable.Count());
            //Log.Message("searching for match to " + rimwarObject.RimWarFaction.def.ToString());
            if (rwd != null && rwd.Count > 0)
            {
                for (int i = 0; i < rwd.Count; i++)
                {
                    //Log.Message("current " + rwd[i].defName);
                    //Log.Message("with count " + rwd[i].defDatas.Count);
                    for (int j = 0; j < rwd[i].defDatas.Count; j++)
                    {
                        RimWarDefData defData = rwd[i].defDatas[j];
                        //Log.Message("checking faction " + defData.factionDefname);
                        if (defData.factionDefname.ToString() == rimwarObject.RimWarFaction.def.ToString())
                        {
                            if (!settingsRef.randomizeFactionBehavior || defData.forceBehavior)
                            {
                                factionFound = true;
                                //Log.Message("found faction match in rimwardef for " + defData.factionDefname.ToString());
                                rimwarObject.movesAtNight = defData.movesAtNight;
                                rimwarObject.behavior = defData.behavior;
                                rimwarObject.createsSettlements = defData.createsSettlements;
                                rimwarObject.hatesPlayer = defData.hatesPlayer;
                                if (settingsRef.randomizeAttributes)
                                {
                                    rimwarObject.movementAttribute = defData.movementBonus;
                                    rimwarObject.growthAttribute = defData.growthBonus;
                                    rimwarObject.combatAttribute = defData.combatBonus;
                                }
                                break;
                            }
                        }
                    }
                }
                if (!factionFound)
                {
                    RandomizeFactionBehavior(rimwarObject);
                }
            }
            else
            {
                RandomizeFactionBehavior(rimwarObject);
            }
            //Log.Message("generating faction behavior for " + rimwarObject.RimWarFaction);
            WorldUtility.CalculateFactionBehaviorWeights(rimwarObject);
        }

        private void RandomizeFactionBehavior(RimWarData rimwarObject)
        {
            //Log.Message("randomizing faction behavior for " + rimwarObject.RimWarFaction.Name);
            if (rimwarObject.RimWarFaction.def.isPlayer)
            {
                rimwarObject.behavior = RimWarBehavior.Player;
                rimwarObject.createsSettlements = false;
                rimwarObject.hatesPlayer = false;
                rimwarObject.movesAtNight = false;
            }
            else if (WorldUtility.IsVassalFaction(rimwarObject.RimWarFaction))
            {
                rimwarObject.behavior = RimWarBehavior.Vassal;
                rimwarObject.createsSettlements = false;
                rimwarObject.hatesPlayer = false;
                rimwarObject.movesAtNight = false;
                if (Options.Settings.Instance.randomizeAttributes)
                {
                    rimwarObject.combatAttribute = Rand.Range(.75f, 1.25f);
                    rimwarObject.growthAttribute = Rand.Range(.75f, 1.25f);
                    rimwarObject.movementAttribute = Rand.Range(.75f, 1.25f);
                }
            }
            else
            {
                rimwarObject.behavior = WorldUtility.GetRandomBehavior;
                rimwarObject.createsSettlements = true;
                rimwarObject.hatesPlayer = rimwarObject.RimWarFaction.def.permanentEnemy;
                if (Options.Settings.Instance.randomizeAttributes)
                {
                    rimwarObject.combatAttribute = Rand.Range(.75f, 1.25f);
                    rimwarObject.growthAttribute = Rand.Range(.75f, 1.25f);
                    rimwarObject.movementAttribute = Rand.Range(.75f, 1.25f);
                }
            }
        }

        private void AssignFactionSettlements(RimWarData rimwarObject)
        {
            //Log.Message("assigning settlements to " + rimwarObject.RimWarFaction.Name);
            this.WorldObjects = Find.WorldObjects.AllWorldObjects.ToList();
            if (worldObjects != null && worldObjects.Count > 0)
            {
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    //Log.Message("faction for " + worldObjects[i] + " is " + rimwarObject);
                    if (worldObjects[i].Faction != null && rimwarObject != null && rimwarObject.RimWarFaction != null && worldObjects[i].Faction.randomKey == rimwarObject.RimWarFaction.randomKey)
                    {
                        WorldUtility.CreateRimWarSettlement(rimwarObject, worldObjects[i]);
                    }
                }
            }
        }

        public void UpdateFactionSettlements(RimWarData rwd)
        {
            this.WorldObjects = Find.WorldObjects.AllWorldObjects.ToList();

            if (worldObjects != null && worldObjects.Count > 0 && rwd != null && rwd.RimWarFaction != null)
            {
                //look for settlements not assigned a RimWar Settlement
                for (int i = 0; i < worldObjects.Count; i++)
                {
                    RimWorld.Planet.Settlement wos = worldObjects[i] as RimWorld.Planet.Settlement;
                    if (WorldUtility.IsValidSettlement(wos) && wos.Faction.randomKey == rwd.RimWarFaction.randomKey)
                    {
                        bool hasSettlement = false;
                        if (rwd.WorldSettlements != null && rwd.WorldSettlements.Count > 0)
                        {
                            for (int j = 0; j < rwd.WorldSettlements.Count; j++)
                            {
                                RimWorld.Planet.Settlement rwdTown = rwd.WorldSettlements[j];
                                if (rwdTown.Tile == wos.Tile)
                                {
                                    hasSettlement = true;
                                    break;
                                }
                            }
                        }
                        if (!hasSettlement)
                        {
                            WorldUtility.CreateRimWarSettlement(rwd, wos);
                        }
                    }
                }
                //look for settlements assigned without corresponding world objects
                for (int i = 0; i < rwd.WorldSettlements.Count; i++)
                {
                    RimWorld.Planet.Settlement rwdTown = rwd.WorldSettlements[i];
                    bool hasWorldObject = false;
                    for (int j = 0; j < worldObjects.Count; j++)
                    {
                        RimWorld.Planet.Settlement wos = worldObjects[j] as RimWorld.Planet.Settlement;
                        if (wos != null && wos.Tile == rwdTown.Tile && wos.Faction.randomKey == rwdTown.Faction.randomKey)
                        {
                            hasWorldObject = true;
                            break;
                        }
                    }
                    if (!hasWorldObject)
                    {
                        rwd.WorldSettlements.Remove(rwdTown);
                        break;
                    }
                }
            }
        }

        // Settlement actions. Each schedules a job: the decision is computed from a WorldSnapshot (on the RimWar worker when
        // threading is enabled) and applied on the game thread JobScheduler.CommitDelayTicks later. See Planet/Jobs.

        public void AttemptWarbandActionAgainstTown(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement,
                                                    RimWarSettlementComp rwsComp, bool forcePlayer = false,
                                                    bool ignoreRestrictions = false)
        {
            if (rwd != null && rwsComp != null && parentSettlement != null)
            {
                JobScheduler.Schedule(new WarbandActionJob(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, launched: false));
            }
            else
            {
                Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwsComp " + rwsComp);
            }
        }

        public void AttemptLaunchedWarbandAgainstTown(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement,
                                                      RimWarSettlementComp rwsComp, bool forcePlayer = false,
                                                      bool ignoreRestrictions = false)
        {
            if (rwd == null || rwsComp == null || parentSettlement == null)
            {
                Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwdTown " + rwsComp);
                return;
            }
            if (rwsComp.RimWarPoints >= 1000 || ignoreRestrictions)
            {
                JobScheduler.Schedule(new WarbandActionJob(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, launched: true));
            }
        }

        public void AttemptScoutMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement,
                                        RimWarSettlementComp rwsComp, bool forcePlayerTown = false,
                                        bool forcePlayerCaravan = false, bool ignoreRestrictions = false)
        {
            if (rwd != null && rwsComp != null && parentSettlement != null)
            {
                JobScheduler.Schedule(new ScoutActionJob(rwd, parentSettlement, rwsComp, forcePlayerTown, forcePlayerCaravan, ignoreRestrictions));
            }
            else
            {
                Log.Warning("Found null when attempting to generate a scout: rwd " + rwd + " rwsComp " + rwsComp);
            }
        }

        public void AttemptSettlerMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement,
                                          RimWarSettlementComp rwsComp, bool ignoreRestrictions = false,
                                          bool ignoreNearbyTown = false)
        {
            if (rwd != null && rwsComp != null && parentSettlement != null)
            {
                JobScheduler.Schedule(new SettlerActionJob(rwd, parentSettlement, rwsComp, ignoreRestrictions));
            }
            else
            {
                Log.Warning("Found null when attempting to generate a settler: rwd " + rwd + " rwsComp " + rwsComp);
            }
        }

        public void AttemptTradeMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement,
                                        RimWarSettlementComp rwsComp, bool forcePlayer = false,
                                        bool ignoreRestrictions = false)
        {
            if (rwd != null && rwsComp != null && parentSettlement != null)
            {
                JobScheduler.Schedule(new TradeActionJob(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions));
            }
            else
            {
                Log.Warning("Found null when attempting to generate a trader: rwd " + rwd + " rwsComp " + rwsComp);
            }
        }

        private void AttemptDiplomatMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement,
                                            RimWarSettlementComp rwsComp)
        {
            if (rwd == null || rwsComp == null || parentSettlement == null)
            {
                Log.Warning("Found null when attempting to generate a diplomat: rwd " + rwd + " rwsComp " + rwsComp);
                return;
            }
            if (rwsComp.RimWarPoints > 1000)
            {
                JobScheduler.Schedule(new DiplomatActionJob(rwd, parentSettlement, rwsComp));
            }
        }

        public void AttemptReinforcement(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement,
                                         RimWarSettlementComp rwsComp, Settlement targetSettlement,
                                         bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            if (rwd != null && rwsComp != null && parentSettlement != null)
            {
                if (rwsComp.RimWarPoints > 1000 || ignoreRestrictions)
                {
                    RimWorld.Planet.Settlement targetTown = targetSettlement;
                    if (targetTown != null)
                    {
                        int pts = Mathf.RoundToInt(.4f * rwsComp.RimWarPoints);
                        WorldUtility.CreateTrader(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                        rwsComp.RimWarPoints -= pts;
                        rwsComp.bonusGrowthCount += Mathf.RoundToInt((float)pts / 10f);
                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a trader: rwd " + rwd + " rwsComp " + rwsComp);
            }
        }

        public int GetHeatForAction(RimWarAction action)
        {
            float pts = 0;
            if (action == RimWarAction.Caravan)
            {
                pts += 50;
            }
            else if (action == RimWarAction.Diplomat)
            {
                pts += 20;
            }
            else if (action == RimWarAction.LaunchedWarband)
            {
                pts += 120;
            }
            else if (action == RimWarAction.ScoutingParty)
            {
                pts += 80;
            }
            else if (action == RimWarAction.Warband)
            {
                pts += 100;
            }
            else
            {
                pts += 50;
            }
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            if (settingsRef.storytellerBasedDifficulty)
            {
                pts = pts / Find.Storyteller.difficulty.threatScale;
            }
            else
            {
                pts = pts / settingsRef.rimwarDifficulty;
            }
            return Mathf.RoundToInt(pts * settingsRef.heatMultiplier);
        }
    }
}
