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
using RimWar.RocketTools;
using RimWar.Options;
using System.Diagnostics;
using System.Threading;
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
        private int factionCount = 0;
        private int nextEvaluationTick = 20;
        private int targetRangeDivider = 100;
        private int totalTowns = 10;
        public Faction victoryFaction = null;
        private bool victoryDeclared = false;
        private bool rwdInitialized = false;
        private bool rwdInitVictory = false;

        //Stored variables
        public List<CaravanTargetData> caravanTargetData;

        public int preventActionsAgainstPlayerUntilTick = 60000;
        public int minimumHeatForPlayerAction = 0;

        public class ContextStorage
        {
            public Dictionary<string, object> rwd = new Dictionary<string, object>();
            internal int targetRange;
            internal SettingsRef settingsRef;
            internal List<WorldObject> woList;
            internal List<Settlement> tmpSettlements;
            internal object warSettlement;
            internal bool shouldExecute;
            internal WorldObject destinationTarget;
            internal PlanetTile destinationTile;
        }

        public static RocketTasker<ContextStorage> tasker = new RocketTasker<ContextStorage>();
        public static int MainthreadID = -1;

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

        private List<RimWarSettlementComp> allRimWarSettlements;
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
        List<WorldObject> worldObjectsOfPlayer = new List<WorldObject>();

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
                foreach(RimWarData rwd in value)
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
            //if (Options.Settings.Instance.threadingEnabled)
            //{
                WorldUtility.CopyData();
            //}
            if (!doOnce)
            {
                if(this.victoryFaction != null)
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
                MainthreadID = Thread.CurrentThread.ManagedThreadId;
            }
            if (currentTick % 60 == 0)
            {
                AdjustCaravanTargets();
            }
            if(currentTick % (int)settingsRef.heatFrequency == 0)
            {
                UpdatePlayerAggression();
            }
            if (currentTick % settingsRef.rwdUpdateFrequency == 0)
            {
                // Update existing factions before update
                CheckForNewFactions();

                if (Options.Settings.Instance.threadingEnabled)
                {
                    tasker.Register(() =>
                    {
                        UpdateFactions();
                        if (settingsRef.useRimWarVictory && !victoryDeclared && this.rwdInitVictory)
                        {
                            CheckVictoryConditions();
                        }
                        return null;
                    }, (context) =>
                    {
                    });
                }
                else
                {
                    UpdateFactions();
                    if (settingsRef.useRimWarVictory && !victoryDeclared && this.rwdInitVictory)
                    {
                        CheckVictoryConditions();
                    }
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
            if (Options.Settings.Instance.threadingEnabled)
            {
                tasker.Tick();
            }
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
            if (this.caravanTargetData != null && this.caravanTargetData.Count > 0)
            {
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
        }

        public void AdjustCaravanTargets()
        {
            if (this.caravanTargetData != null && this.caravanTargetData.Count > 0)
            {
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
                if (factionList[i] == Faction.OfTradersGuild)
                {
                    count--;
                }
            }

            return count;
        }

        public void CheckForNewFactions()
        {
            if(WorldComponent_PowerTracker.CountOnPlanetFactions() > this.RimWarData.Count)
            {
                Utility.RimWar_DebugToolsPlanet.ResetFactions(false, true);
            }
        }

        public void DoGlobalRWDAction()
        {
            RimWarData rwd = this.Active_RWD.RandomElement();
            if (rwd.behavior != RimWarBehavior.Player && rwd.behavior != RimWarBehavior.Vassal && rwd.behavior != RimWarBehavior.Excluded)
            {
                this.globalActions++;
                RimWarAction newAction = rwd.GetWeightedSettlementAction();
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                if (newAction == RimWarAction.Caravan)
                {
                    RimWarSettlementComp rwsc = rwd.WorldSettlements.RandomElement().GetComponent<RimWarSettlementComp>();
                    if (rwsc != null)
                    {
                        rwsc.RimWarPoints += Rand.Range(100, 200);
                    }
                }
                else if (newAction == RimWarAction.Diplomat)
                {
                    if (settingsRef.createDiplomats)
                    {
                        RimWarSettlementComp rwdSettlement = rwd.WorldSettlements.RandomElement().GetComponent<RimWarSettlementComp>();
                        RimWorld.Planet.Settlement rwdPlayerSettlement = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements.RandomElement();
                        if (rwdSettlement != null && rwdPlayerSettlement != null)
                        {
                            WorldUtility.CreateDiplomat(WorldUtility.CalculateDiplomatPoints(rwdSettlement), rwd, rwdSettlement.parent as RimWorld.Planet.Settlement, rwdSettlement.parent.Tile, rwdPlayerSettlement, WorldObjectDefOf.Settlement);
                        }
                    }
                }
                else if (newAction == RimWarAction.LaunchedWarband)
                {
                    RimWarSettlementComp rwdTown = rwd.WorldSettlements.RandomElement().GetComponent<RimWarSettlementComp>();
                    RimWorld.Planet.Settlement rwdPlayerSettlement = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements.RandomElement();
                    if (rwdTown != null && rwdPlayerSettlement != null && rwd.RimWarFaction.HostileTo(Faction.OfPlayerSilentFail) && rwd.CanLaunch)
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

                        WorldUtility.CreateLaunchedWarband(pts, rwd, rwdTown.parent as RimWorld.Planet.Settlement, rwdTown.parent.Tile, rwdPlayerSettlement, WorldObjectDefOf.Settlement);
                    }
                }
                else if (newAction == RimWarAction.ScoutingParty)
                {
                    RimWarSettlementComp rwdTown = rwd.WorldSettlements.RandomElement().GetComponent<RimWarSettlementComp>();
                    RimWorld.Planet.Settlement rwdPlayerSettlement = WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements.RandomElement();
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
                    if (rwdSecond.RimWarFaction != rwd.RimWarFaction && rwdSecond.RimWarFaction != Faction.OfPlayerSilentFail)
                    {
                        rwd.RimWarFaction.TryAffectGoodwillWith(rwdSecond.RimWarFaction, factionAdjustment, true, true, null, null);
                    }
                }
                else if (newAction == RimWarAction.Warband)
                {
                    int factionAdjustment = Rand.Range(-20, 0);
                    RimWarData rwdSecond = this.Active_RWD.RandomElement();
                    if (rwdSecond.RimWarFaction != rwd.RimWarFaction && rwdSecond.RimWarFaction != Faction.OfPlayerSilentFail)
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
                        for(int k = 0; k < rimwarFactions.Count; k++)
                        {
                            if(allFactionsVisible[i].randomKey == rimwarFactions[k].randomKey)
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
            if (this.victoryFaction == null)
            {
                if (Active_RWD != null && Active_RWD.Count > 0)
                {
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
                            if(Active_RWD[i].RimWarFaction.def.defName == Settings.Instance.factionDefForRival.defName)
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
            }
        }

        public void UpdatePlayerAggression()
        {
            this.minimumHeatForPlayerAction = Mathf.Clamp(this.minimumHeatForPlayerAction - Rand.RangeInclusive(1, 2), 0, 10000);
        }

        public void UpdateFactions()
        {            
            IncrementSettlementGrowth();
            ReconstituteSettlements();
            UpdateFactionSettlements(this.RimWarData.RandomElement());
        }

        public void IncrementSettlementGrowth()
        {
            this.totalTowns = 0;
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
                        totalTowns++;
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
                                    if(rwdTown.bonusGrowthCount > 0)
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
                            if(rwd.behavior == RimWarBehavior.Vassal)
                            {
                                rwdTown.vassalHeat = Mathf.Clamp(rwdTown.vassalHeat - Rand.RangeInclusive(1, 3), 0, 10000);
                            }
                        }
                    }
                }

                if(rwd.behavior == RimWarBehavior.Vassal)
                {
                    foreach (Faction f in Find.FactionManager.AllFactionsVisible)
                    {
                        if (f != rwd.RimWarFaction && f != Faction.OfPlayer)
                        {                            
                            FactionRelation frPlayer = Faction.OfPlayer.RelationWith(f);
                            FactionRelation frVassal = rwd.RimWarFaction.RelationWith(f);
                            FactionRelationKind frVassalKind = frVassal.kind;
                            if (frPlayer !=null && frPlayer.kind != frVassalKind)
                            {
                                rwd.RimWarFaction.SetRelation(frPlayer);
                                bool letterSent;
                                rwd.RimWarFaction.Notify_RelationKindChanged(f, frVassalKind, false, "Vassal Alignment", rwd.GetCapitol, out letterSent);
                            }
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
            if (!CheckForRimWarFaction(faction))
            {
                RimWarData newRimWarFaction = new RimWarData(faction);
                if (faction != null)
                {
                    GenerateFactionBehavior(newRimWarFaction);
                    AssignFactionSettlements(newRimWarFaction);
                }
                Settlement s = newRimWarFaction.GetCapitol;
                this.RimWarData.Add(newRimWarFaction);
            }
        }

        private bool CheckForRimWarFaction(Faction faction)
        {
            if (this.RimWarData != null)
            {
                for (int i = 0; i < this.RimWarData.Count; i++)
                {
                    //Log.Message("checking faction " + faction + " against rwd faction: " + this.RimWarData[i].RimWarFaction);
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

        private void AttemptWarbandActionAgainstTownOffMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements = rwd.HostileSettlements;
            }
            else if (forcePlayer)
            {
                tmpSettlements.AddRange(WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements);
            }
            else
            {
                tmpSettlements = rwsComp.NearbyHostileSettlements;
            }
            if (rwd.IsAtWar)
            {
                RimWorld.Planet.Settlement warSettlement = WorldUtility.GetClosestSettlementInRWDTo(rwd, rwsComp.parent.Tile, Mathf.Min(Mathf.RoundToInt((rwsComp.RimWarPoints * 1.5f) / (settingsRef.settlementScanRangeDivider)), (int)settingsRef.maxSettelementScanRange));
                if (warSettlement != null)
                {
                    tmpSettlements.Add(warSettlement);
                }
                targetRange = Mathf.RoundToInt(targetRange * 1.5f);
            }

            context.settingsRef = settingsRef;
            context.tmpSettlements = tmpSettlements;
            context.targetRange = targetRange;
        }

        private void AttemptWarbandActionAgainstTownOnMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            var settingsRef = context.settingsRef;
            var tmpSettlements = context.tmpSettlements;
            var targetRange = context.targetRange;

            if (rwd != null && rwsComp != null)
            {
                if (tmpSettlements != null && tmpSettlements.Count > 0)
                {
                    RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                    if (targetTown != null && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange || ignoreRestrictions))
                    {
                        if (targetTown.Faction == Faction.OfPlayer && preventActionsAgainstPlayerUntilTick > Find.TickManager.TicksGame && !ignoreRestrictions)
                        {
                            return;
                        }
                        if (targetTown.Faction == Faction.OfPlayer && rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                        {
                            return;
                        }
                        RimWarSettlementComp rwscVassal = targetTown.GetComponent<RimWarSettlementComp>();
                        if (WorldUtility.IsVassalFaction(targetTown.Faction) && rwscVassal != null && rwsComp.PlayerHeat < rwscVassal.vassalHeat && !ignoreRestrictions)
                        {
                            return;
                        }
                        if (targetTown.Faction == Faction.OfPlayer && !WorldUtility.FactionCanFight(200, parentSettlement.Faction))
                        {
                            if (!forcePlayer)
                            {
                                return;
                            }
                        }
                        int pts = WorldUtility.CalculateWarbandPointsForRaid(targetTown.GetComponent<RimWarSettlementComp>());
                        if (rwd.behavior == RimWarBehavior.Cautious)
                        {
                            pts = Mathf.RoundToInt(pts * 1.1f);
                        }
                        else if (rwd.behavior == RimWarBehavior.Warmonger)
                        {
                            pts = Mathf.RoundToInt(pts * 1.25f);
                        }

                        if (rwd.IsAtWarWith(targetTown.Faction))
                        {
                            pts = Mathf.RoundToInt(pts * 1.2f);
                            if (rwsComp.RimWarPoints * .85f >= pts || ignoreRestrictions)
                            {
                                WorldUtility.CreateWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                                rwsComp.RimWarPoints -= pts;
                                if (targetTown.Faction == Faction.OfPlayer)
                                {
                                    rwsComp.PlayerHeat = 0;
                                    minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Warband);
                                }
                            }
                        }
                        else if (rwsComp.RimWarPoints * .75f >= pts || ignoreRestrictions)
                        {
                            WorldUtility.CreateWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                            rwsComp.RimWarPoints -= pts;
                            if (targetTown.Faction == Faction.OfPlayer)
                            {
                                rwsComp.PlayerHeat = 0;
                                minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Warband);
                            }
                            else if(WorldUtility.IsVassalFaction(targetTown.Faction))
                            {
                                rwsComp.PlayerHeat = 0;
                                rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.Warband));
                            }
                        }
                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwsComp " + rwsComp);
            }
        }
        public void AttemptWarbandActionAgainstTown_UnThreaded(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements = rwd.HostileSettlements;
            }
            else if (forcePlayer)
            {
                tmpSettlements.AddRange(WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements);
            }
            else
            {
                tmpSettlements = rwsComp.NearbyHostileSettlements;
            }
            if (rwd.IsAtWar)
            {
                RimWorld.Planet.Settlement warSettlement = WorldUtility.GetClosestSettlementInRWDTo(rwd, rwsComp.parent.Tile, Mathf.Min(Mathf.RoundToInt((rwsComp.RimWarPoints * 1.5f) / (settingsRef.settlementScanRangeDivider)), (int)settingsRef.maxSettelementScanRange));
                if (warSettlement != null)
                {
                    tmpSettlements.Add(warSettlement);
                }
                targetRange = Mathf.RoundToInt(targetRange * 1.5f);
            }

            if (rwd != null && rwsComp != null)
            {
                if (tmpSettlements != null && tmpSettlements.Count > 0)
                {
                    RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                    if (targetTown != null && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange || ignoreRestrictions))
                    {
                        if (targetTown.Faction == Faction.OfPlayer && preventActionsAgainstPlayerUntilTick > Find.TickManager.TicksGame && !ignoreRestrictions)
                        {
                            return;
                        }
                        if (targetTown.Faction == Faction.OfPlayer && rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                        {
                            return;
                        }
                        RimWarSettlementComp rwscVassal = targetTown.GetComponent<RimWarSettlementComp>();
                        if (WorldUtility.IsVassalFaction(targetTown.Faction) && rwscVassal != null && rwsComp.PlayerHeat < rwscVassal.vassalHeat && !ignoreRestrictions)
                        {
                            return;
                        }
                        if (targetTown.Faction == Faction.OfPlayer && !WorldUtility.FactionCanFight(200, parentSettlement.Faction))
                        {
                            if (!forcePlayer)
                            {
                                return;
                            }
                        }
                        int pts = WorldUtility.CalculateWarbandPointsForRaid(targetTown.GetComponent<RimWarSettlementComp>());
                        if (rwd.behavior == RimWarBehavior.Cautious)
                        {
                            pts = Mathf.RoundToInt(pts * 1.1f);
                        }
                        else if (rwd.behavior == RimWarBehavior.Warmonger)
                        {
                            pts = Mathf.RoundToInt(pts * 1.25f);
                        }

                        if (rwd.IsAtWarWith(targetTown.Faction))
                        {
                            pts = Mathf.RoundToInt(pts * 1.2f);
                            if (rwsComp.RimWarPoints * .85f >= pts || ignoreRestrictions)
                            {
                                WorldUtility.CreateWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                                rwsComp.RimWarPoints -= pts;
                                if (targetTown.Faction == Faction.OfPlayer)
                                {
                                    rwsComp.PlayerHeat = 0;
                                    minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Warband);
                                }
                                else if (WorldUtility.IsVassalFaction(targetTown.Faction))
                                {
                                    rwsComp.PlayerHeat = 0;
                                    rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.Warband));
                                }
                            }
                        }
                        else if (rwsComp.RimWarPoints * .75f >= pts || ignoreRestrictions)
                        {
                            WorldUtility.CreateWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                            rwsComp.RimWarPoints -= pts;
                            if (targetTown.Faction == Faction.OfPlayer)
                            {
                                rwsComp.PlayerHeat = 0;
                                minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Warband);
                            }
                            else if (WorldUtility.IsVassalFaction(targetTown.Faction))
                            {
                                rwsComp.PlayerHeat = 0;
                                rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.Warband));
                            }
                        }

                    }
                }
            }
            else
            {
                Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwsComp " + rwsComp);
            }
        }

        public void AttemptWarbandActionAgainstTown(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            if (false)  //Options.Settings.Instance.threadingEnabled)
            {
                if (rwd != null && rwsComp != null)
                {
                    tasker.Register((Func<ContextStorage>)(() =>
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        var context = new ContextStorage();
                        this.AttemptWarbandActionAgainstTownOffMainThread(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, context);

                        stopwatch.Stop();
                        if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                            string.Format("RIMWAR: warband against town mission off thread took {0} ms", stopwatch.ElapsedMilliseconds));
                        return context;
                    }),
                    (Action<ContextStorage>)((context) =>
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        this.AttemptWarbandActionAgainstTownOnMainThread(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, context);

                        stopwatch.Stop();
                        if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                            string.Format("RIMWAR: warband against town mission on thread took {0} ms", stopwatch.ElapsedMilliseconds));
                    }));
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a scout: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
            else
            {
                if (rwd != null && rwsComp != null)
                {
                    this.AttemptWarbandActionAgainstTown_UnThreaded(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions);
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a scout: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
        }

        public void AttemptLaunchedWarbandAgainstTownOffMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange * 2;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements = rwd.HostileSettlements;
            }
            else if (forcePlayer)
            {
                tmpSettlements.AddRange(WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements);
            }
            else
            {
                tmpSettlements = rwsComp.NearbyHostileSettlements;
            }
            if (rwd.IsAtWar)
            {
                RimWorld.Planet.Settlement warSettlement = WorldUtility.GetClosestSettlementInRWDTo(rwd, parentSettlement.Tile, Mathf.Min(Mathf.RoundToInt(targetRange), (int)settingsRef.maxSettelementScanRange));
                if (warSettlement != null)
                {
                    tmpSettlements.Add(warSettlement);
                }
                targetRange = Mathf.RoundToInt(targetRange * 1.5f);
            }

            context.settingsRef = settingsRef;
            context.tmpSettlements = tmpSettlements;
            context.targetRange = targetRange;
        }

        public void AttemptLaunchedWarbandAgainstTownOnMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            var settingsRef = context.settingsRef;
            var tmpSettlements = context.tmpSettlements;
            var targetRange = context.targetRange;

            if (tmpSettlements != null && tmpSettlements.Count > 0)
            {
                RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                if (targetTown != null && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange || ignoreRestrictions))
                {
                    if (targetTown.Faction == Faction.OfPlayer && preventActionsAgainstPlayerUntilTick > Find.TickManager.TicksGame && !ignoreRestrictions)
                    {
                        return;
                    }
                    if (targetTown.Faction == Faction.OfPlayer && rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                    {
                        return;
                    }
                    RimWarSettlementComp rwscVassal = targetTown.GetComponent<RimWarSettlementComp>();
                    if (WorldUtility.IsVassalFaction(targetTown.Faction) && rwscVassal != null && rwsComp.PlayerHeat < rwscVassal.vassalHeat && !ignoreRestrictions)
                    {
                        return;
                    }
                    if (targetTown.Faction == Faction.OfPlayer && !WorldUtility.FactionCanFight(200, parentSettlement.Faction))
                    {
                        if (!forcePlayer)
                        {
                            return;
                        }
                    }
                    int pts = WorldUtility.CalculateWarbandPointsForRaid(targetTown.GetComponent<RimWarSettlementComp>());
                    if (rwd.behavior == RimWarBehavior.Cautious)
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }

                    if (rwd.IsAtWarWith(targetTown.Faction))
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                        if (rwsComp.RimWarPoints * .8f >= pts || ignoreRestrictions)
                        {
                            WorldUtility.CreateLaunchedWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                            rwsComp.RimWarPoints -= pts;
                            if (targetTown.Faction == Faction.OfPlayer)
                            {
                                rwsComp.PlayerHeat = 0;
                                minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.LaunchedWarband);
                            }
                            else if(WorldUtility.IsVassalFaction(targetTown.Faction))
                            {
                                rwsComp.PlayerHeat = 0;
                                rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.LaunchedWarband));
                            }
                        }
                    }
                    else if (rwsComp.RimWarPoints * .6f >= pts || ignoreRestrictions)
                    {
                        //Log.Message("launching warband from " + rwsComp.RimWorld_Settlement.Name);
                        WorldUtility.CreateLaunchedWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                        rwsComp.RimWarPoints -= pts;
                        if (targetTown.Faction == Faction.OfPlayer)
                        {
                            rwsComp.PlayerHeat = 0;
                            minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.LaunchedWarband);
                        }
                        else if (WorldUtility.IsVassalFaction(targetTown.Faction))
                        {
                            rwsComp.PlayerHeat = 0;
                            rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.LaunchedWarband));
                        }
                    }
                }
            }
        }

        public void AttemptLaunchedWarbandAgainstTown_UnThreaded(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange * 2;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements = rwd.HostileSettlements;
            }
            else if (forcePlayer)
            {
                tmpSettlements.AddRange(WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements);
            }
            else
            {
                tmpSettlements = rwsComp.NearbyHostileSettlements;
            }
            if (rwd.IsAtWar)
            {
                RimWorld.Planet.Settlement warSettlement = WorldUtility.GetClosestSettlementInRWDTo(rwd, parentSettlement.Tile, Mathf.Min(Mathf.RoundToInt(targetRange), (int)settingsRef.maxSettelementScanRange));
                if (warSettlement != null)
                {
                    tmpSettlements.Add(warSettlement);
                }
                targetRange = Mathf.RoundToInt(targetRange * 1.5f);
            }

            if (tmpSettlements != null && tmpSettlements.Count > 0)
            {
                RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                if (targetTown != null && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange || ignoreRestrictions))
                {
                    if (targetTown.Faction == Faction.OfPlayer && preventActionsAgainstPlayerUntilTick > Find.TickManager.TicksGame && !ignoreRestrictions)
                    {
                        return;
                    }
                    if (targetTown.Faction == Faction.OfPlayer && rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                    {
                        return;
                    }
                    RimWarSettlementComp rwscVassal = targetTown.GetComponent<RimWarSettlementComp>();
                    if (WorldUtility.IsVassalFaction(targetTown.Faction) && rwscVassal != null && rwsComp.PlayerHeat < rwscVassal.vassalHeat && !ignoreRestrictions)
                    {
                        return;
                    }
                    if (targetTown.Faction == Faction.OfPlayer && !WorldUtility.FactionCanFight(200, parentSettlement.Faction))
                    {
                        if (!forcePlayer)
                        {
                            return;
                        }
                    }
                    int pts = WorldUtility.CalculateWarbandPointsForRaid(targetTown.GetComponent<RimWarSettlementComp>());
                    if (rwd.behavior == RimWarBehavior.Cautious)
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }

                    if (rwd.IsAtWarWith(targetTown.Faction))
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                        if (rwsComp.RimWarPoints * .8f >= pts || ignoreRestrictions)
                        {
                            WorldUtility.CreateLaunchedWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                            rwsComp.RimWarPoints -= pts;
                            if (targetTown.Faction == Faction.OfPlayer)
                            {
                                rwsComp.PlayerHeat = 0;
                                minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.LaunchedWarband);
                            }
                            else if (WorldUtility.IsVassalFaction(targetTown.Faction))
                            {
                                rwsComp.PlayerHeat = 0;
                                rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.LaunchedWarband));
                            }
                        }
                    }
                    else if (rwsComp.RimWarPoints * .6f >= pts || ignoreRestrictions)
                    {
                        //Log.Message("launching warband from " + rwsComp.RimWorld_Settlement.Name);
                        WorldUtility.CreateLaunchedWarband(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                        rwsComp.RimWarPoints -= pts;
                        if (targetTown.Faction == Faction.OfPlayer)
                        {
                            rwsComp.PlayerHeat = 0;
                            minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.LaunchedWarband);
                        }
                        else if (WorldUtility.IsVassalFaction(targetTown.Faction))
                        {
                            rwsComp.PlayerHeat = 0;
                            rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.LaunchedWarband));
                        }
                    }
                }
            }
        }

        public void AttemptLaunchedWarbandAgainstTown(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            if (false)	//Options.Settings.Instance.threadingEnabled)
            {
                if (rwsComp.RimWarPoints >= 1000 || ignoreRestrictions)
                {
                    if (rwd != null && rwsComp != null)
                    {
                        tasker.Register((Func<ContextStorage>)(() =>
                        {
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();

                            var context = new ContextStorage();
                            this.AttemptLaunchedWarbandAgainstTownOffMainThread(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, context);

                            stopwatch.Stop();
                            if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                                string.Format("RIMWAR: warband lauched against town mission off thread took {0} ms", stopwatch.ElapsedMilliseconds));
                            return context;
                        }),
                        (Action<ContextStorage>)((context) =>
                        {
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            this.AttemptLaunchedWarbandAgainstTownOnMainThread(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, context);

                            stopwatch.Stop();
                            if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                                string.Format("RIMWAR: warband lauched against town mission on thread took {0} ms", stopwatch.ElapsedMilliseconds));
                        }));
                    }
                    else
                    {
                        Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwdTown " + rwsComp);
                    }
                }
            }
            else
            {
                if (rwsComp.RimWarPoints >= 1000 || ignoreRestrictions)
                {
                    if (rwd != null && rwsComp != null)
                    {
                        this.AttemptLaunchedWarbandAgainstTown_UnThreaded(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions);
                    }
                    else
                    {
                        Log.Warning("Found null when attempting to generate a warband: rwd " + rwd + " rwdTown " + rwsComp);
                    }
                }
            }
        }

        private void AttemptScoutOnMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayerTown = false, bool forcePlayerCaravan = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            var wo = context.destinationTarget;
            var shouldExecute = context.shouldExecute;
            if (shouldExecute)
            {
                if (wo is Caravan)
                {
                    Caravan playerCaravan = wo as Caravan;
                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, Mathf.RoundToInt(playerCaravan.PlayerWealthForStoryteller / 200));
                    WorldUtility.CreateScout(pts, rwd, parentSettlement, parentSettlement.Tile, wo, WorldObjectDefOf.Caravan);
                    rwsComp.RimWarPoints -= pts;
                    rwsComp.PlayerHeat = 0;
                    minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.ScoutingParty);
                }
                else if (wo is WarObject)
                {
                    WarObject warObject = wo as WarObject;
                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, warObject.RimWarPoints);
                    if (rwd.IsAtWarWith(warObject.Faction))
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }
                    WorldUtility.CreateScout(pts, rwd, parentSettlement, parentSettlement.Tile, wo, RimWarDefOf.RW_WarObject);
                    rwsComp.RimWarPoints -= pts;
                    if (wo.Faction == Faction.OfPlayer || WorldUtility.IsVassalFaction(wo.Faction))
                    {
                        rwsComp.PlayerHeat = 0;
                        minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.ScoutingParty);
                    }
                }
                else if (wo is RimWorld.Planet.Settlement)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(wo.Tile);
                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, rwsc.RimWarPoints);
                    if (rwd.IsAtWarWith(rwsc.parent.Faction))
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }
                    WorldUtility.CreateScout(pts, rwd, parentSettlement, parentSettlement.Tile, wo, WorldObjectDefOf.Settlement);
                    rwsComp.RimWarPoints -= pts;
                    if (wo.Faction == Faction.OfPlayer)
                    {
                        rwsComp.PlayerHeat = 0;
                        minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.ScoutingParty);
                    }
                    else if(WorldUtility.IsVassalFaction(wo.Faction))
                    {
                        rwsComp.PlayerHeat = 0;
                        RimWarSettlementComp rwscVassal = wo.GetComponent<RimWarSettlementComp>();
                        rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.ScoutingParty));
                    }
                }
            }
        }

        private void AttemptScoutOffMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayerTown = false, bool forcePlayerCaravan = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;
            bool shouldExecute = false;
            if (rwd.behavior == RimWarBehavior.Expansionist)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.3f);
            }
            else if (rwd.behavior == RimWarBehavior.Warmonger)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.2f);
            }
            else if (rwd.behavior == RimWarBehavior.Aggressive)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.1f);
            }
            List<WorldObject> woList = new List<WorldObject>();
            if (settingsRef.forceRandomObject)
            {
                woList.Add(Find.WorldObjects.AllWorldObjects.RandomElement());
            }
            else if (forcePlayerCaravan)
            {
                woList.Add(Find.WorldObjects.Caravans.RandomElement());
            }
            else if (forcePlayerTown)
            {
                woList.Add(WorldUtility.GetClosestSettlementOfFaction(Faction.OfPlayer, parentSettlement.Tile, 500).parent);
            }
            else
            {
                woList = WorldUtility.GetWorldObjectsInRange(parentSettlement.Tile, targetRange);
            }
            for (int i = 0; i < woList.Count; i++)
            {
                WorldObject wo = woList[i];
                if (wo.Faction != null && ((wo.Faction.HostileTo(rwd.RimWarFaction) && Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, wo.Tile) <= targetRange) || ignoreRestrictions))
                {
                    if (wo.Faction == Faction.OfPlayer && preventActionsAgainstPlayerUntilTick > Find.TickManager.TicksGame && !ignoreRestrictions)
                    {
                        continue;
                    }
                    if (wo.Faction == Faction.OfPlayer && rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                    {
                        continue;
                    }
                    RimWarSettlementComp rwscVassal = wo.GetComponent<RimWarSettlementComp>();
                    if (WorldUtility.IsVassalFaction(wo.Faction) && rwscVassal != null && rwsComp.PlayerHeat < rwscVassal.vassalHeat && !ignoreRestrictions)
                    {
                        return;
                    }
                    if (wo.Faction == Faction.OfPlayer && !WorldUtility.FactionCanFight(200, parentSettlement.Faction))
                    {
                        if (!forcePlayerCaravan && !forcePlayerTown)
                        {
                            continue;
                        }
                    }
                    if (wo is Caravan)
                    {
                        Caravan playerCaravan = wo as Caravan;
                        if ((playerCaravan.PlayerWealthForStoryteller / 200) <= (rwsComp.RimWarPoints * .5f) && ((Find.WorldGrid.TraversalDistanceBetween(wo.Tile, parentSettlement.Tile) <= Mathf.RoundToInt(targetRange * playerCaravan.Visibility)) || ignoreRestrictions))
                        {
                            context.destinationTarget = wo;
                            shouldExecute = true;
                            break;
                        }
                    }
                    else if (wo is WarObject)
                    {
                        WarObject warObject = wo as WarObject;
                        if (warObject.RimWarPoints <= (rwsComp.RimWarPoints * .5f) || ignoreRestrictions)
                        {
                            context.destinationTarget = wo;
                            shouldExecute = true;
                            break;
                        }
                    }
                    else if (wo is RimWorld.Planet.Settlement)
                    {
                        RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(wo.Tile);
                        if (rwsc != null && (rwsc.RimWarPoints <= (rwsComp.RimWarPoints * .5f) || ignoreRestrictions))
                        {
                            context.destinationTarget = wo;
                            shouldExecute = true;
                            break;
                        }
                    }
                }
            }
            context.shouldExecute = shouldExecute;
        }

        public void AttemptScoutMission_UnThreaded(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayerTown = false, bool forcePlayerCaravan = false, bool ignoreRestrictions = false)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;
            bool shouldExecute = false;
            if (rwd.behavior == RimWarBehavior.Expansionist)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.3f);
            }
            else if (rwd.behavior == RimWarBehavior.Warmonger)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.2f);
            }
            else if (rwd.behavior == RimWarBehavior.Aggressive)
            {
                targetRange = Mathf.RoundToInt(targetRange * 1.1f);
            }
            List<WorldObject> woList = new List<WorldObject>();
            if (settingsRef.forceRandomObject)
            {
                woList.Add(Find.WorldObjects.AllWorldObjects.RandomElement());
            }
            else if (forcePlayerCaravan)
            {
                woList.Add(Find.WorldObjects.Caravans.RandomElement());
            }
            else if (forcePlayerTown)
            {
                woList.Add(WorldUtility.GetClosestSettlementOfFaction(Faction.OfPlayer, parentSettlement.Tile, 500).parent);
            }
            else
            {
                woList = WorldUtility.GetWorldObjectsInRange(parentSettlement.Tile, targetRange);
            }
            WorldObject wo = null;
            for (int i = 0; i < woList.Count; i++)
            {
                wo = woList[i];
                if (wo.Faction != null && ((wo.Faction.HostileTo(rwd.RimWarFaction) && Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, wo.Tile) <= targetRange) || ignoreRestrictions))
                {
                    if (wo.Faction == Faction.OfPlayer && preventActionsAgainstPlayerUntilTick > Find.TickManager.TicksGame && !ignoreRestrictions)
                    {
                        continue;
                    }
                    if (wo.Faction == Faction.OfPlayer && rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                    {
                        continue;
                    }
                    RimWarSettlementComp rwscVassal = wo.GetComponent<RimWarSettlementComp>();
                    if (WorldUtility.IsVassalFaction(wo.Faction) && rwscVassal != null && rwsComp.PlayerHeat < rwscVassal.vassalHeat && !ignoreRestrictions)
                    {
                        return;
                    }
                    if (wo.Faction == Faction.OfPlayer && !WorldUtility.FactionCanFight(200, parentSettlement.Faction))
                    {
                        if (!forcePlayerCaravan && !forcePlayerTown)
                        {
                            continue;
                        }
                    }
                    if (wo is Caravan)
                    {
                        Caravan playerCaravan = wo as Caravan;
                        if ((playerCaravan.PlayerWealthForStoryteller / 200) <= (rwsComp.RimWarPoints * .5f) && ((Find.WorldGrid.TraversalDistanceBetween(wo.Tile, parentSettlement.Tile) <= Mathf.RoundToInt(targetRange * playerCaravan.Visibility)) || ignoreRestrictions))
                        {
                            shouldExecute = true;
                            break;
                        }
                    }
                    else if (wo is WarObject)
                    {
                        WarObject warObject = wo as WarObject;
                        if (warObject.RimWarPoints <= (rwsComp.RimWarPoints * .5f) || ignoreRestrictions)
                        {
                            shouldExecute = true;
                            break;
                        }
                    }
                    else if (wo is RimWorld.Planet.Settlement)
                    {
                        RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(wo.Tile);
                        if (rwsc != null && (rwsc.RimWarPoints <= (rwsComp.RimWarPoints * .5f) || ignoreRestrictions))
                        {
                            shouldExecute = true;
                            break;
                        }
                    }
                }
            }

            if (shouldExecute && wo != null)
            {
                if (wo is Caravan)
                {
                    Caravan playerCaravan = wo as Caravan;
                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, Mathf.RoundToInt(playerCaravan.PlayerWealthForStoryteller / 200));
                    WorldUtility.CreateScout(pts, rwd, parentSettlement, parentSettlement.Tile, wo, WorldObjectDefOf.Caravan);
                    rwsComp.RimWarPoints -= pts;
                    rwsComp.PlayerHeat = 0;
                    minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.ScoutingParty);
                }
                else if (wo is WarObject)
                {
                    WarObject warObject = wo as WarObject;
                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, warObject.RimWarPoints);
                    if (rwd.IsAtWarWith(warObject.Faction))
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }
                    WorldUtility.CreateScout(pts, rwd, parentSettlement, parentSettlement.Tile, wo, RimWarDefOf.RW_WarObject);
                    rwsComp.RimWarPoints -= pts;
                    if (wo.Faction == Faction.OfPlayer || WorldUtility.IsVassalFaction(wo.Faction))
                    {
                        rwsComp.PlayerHeat = 0;
                        minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.ScoutingParty);
                    }
                }
                else if (wo is RimWorld.Planet.Settlement)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(wo.Tile);
                    int pts = WorldUtility.CalculateScoutMissionPoints(rwd, rwsc.RimWarPoints);
                    if (rwd.IsAtWarWith(rwsc.parent.Faction))
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }
                    WorldUtility.CreateScout(pts, rwd, parentSettlement, parentSettlement.Tile, wo, WorldObjectDefOf.Settlement);
                    rwsComp.RimWarPoints -= pts;
                    if (wo.Faction == Faction.OfPlayer)
                    {
                        rwsComp.PlayerHeat = 0;
                        minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.ScoutingParty);
                    }
                    else if (WorldUtility.IsVassalFaction(wo.Faction))
                    {
                        rwsComp.PlayerHeat = 0;
                        RimWarSettlementComp rwscVassal = wo.GetComponent<RimWarSettlementComp>();
                        rwscVassal.vassalHeat += (2 * GetHeatForAction(RimWarAction.ScoutingParty));
                    }
                }
            }
        }

        public void AttemptScoutMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayerTown = false, bool forcePlayerCaravan = false, bool ignoreRestrictions = false)
        {
            if (false) //Options.Settings.Instance.threadingEnabled)
            {
                if (rwd != null && rwsComp != null)
                {
                    tasker.Register((Func<ContextStorage>)(() =>
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        var context = new ContextStorage();
                        this.AttemptScoutOffMainThread((RimWarData)rwd, (Settlement)parentSettlement, (RimWarSettlementComp)rwsComp, (bool)forcePlayerTown, (bool)forcePlayerCaravan, (bool)ignoreRestrictions, (ContextStorage)context);

                        stopwatch.Stop();
                        if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                            string.Format("RIMWAR: scouting mission off thread took {0} ms", stopwatch.ElapsedMilliseconds));
                        return context;
                    }),
                    (Action<ContextStorage>)((context) =>
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        this.AttemptScoutOnMainThread((RimWarData)rwd, (Settlement)parentSettlement, (RimWarSettlementComp)rwsComp, (bool)forcePlayerTown, (bool)forcePlayerCaravan, (bool)ignoreRestrictions, (ContextStorage)context);
                        stopwatch.Stop();
                        if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                            string.Format("RIMWAR: scouting mission on thread took {0} ms", stopwatch.ElapsedMilliseconds));
                    }));
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a scout: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
            else
            {
                if (rwd != null && rwsComp != null)
                {
                    this.AttemptScoutMission_UnThreaded((RimWarData)rwd, (Settlement)parentSettlement, (RimWarSettlementComp)rwsComp, (bool)forcePlayerTown, (bool)forcePlayerCaravan, (bool)ignoreRestrictions);
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a scout: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
        }

        private void AttemptSettlerOffMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool ignoreRestrictions = false, bool ignoreNearbyTown = false, ContextStorage context = null)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            bool shouldExecute = false;
            if (rwsComp.RimWarPoints > 500 || ignoreRestrictions)
            {
                int targetRange = Mathf.Clamp(rwsComp.SettlementScanRange, 11, Mathf.Max((int)settingsRef.maxSettelementScanRange, 12));
                if (rwd.behavior == RimWarBehavior.Expansionist)
                {
                    targetRange = Mathf.RoundToInt(targetRange * 1.2f);
                }
                else if (rwd.behavior == RimWarBehavior.Warmonger)
                {
                    targetRange = Mathf.RoundToInt(targetRange * .8f);
                }
                List<PlanetTile> tmpTiles = new List<PlanetTile>();
                tmpTiles.Clear();
                for (int i = 0; i < 5; i++)
                {
                    PlanetTile tile = PlanetTile.Invalid;
                    TileFinder.TryFindPassableTileWithTraversalDistance(parentSettlement.Tile, 10, targetRange, out tile);
                    if (tile.Valid)
                    {
                        Tile t = Find.WorldGrid[tile];
                        if (t.PrimaryBiome != null && !t.PrimaryBiome.isExtremeBiome && t.PrimaryBiome.canBuildBase)
                        {
                            tmpTiles.Add(tile);
                        }
                    }
                }
                if (tmpTiles != null && tmpTiles.Count > 0)
                {
                    for (int i = 0; i < tmpTiles.Count; i++)
                    {
                        PlanetTile destinationTile = tmpTiles[i];
                        if (destinationTile.Valid && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, destinationTile) <= targetRange || ignoreRestrictions))
                        {
                            List<WorldObject> worldObjects = WorldUtility.GetWorldObjectsInRange(destinationTile, 10);
                            bool nearbySettlement = false;
                            for (int j = 0; j < worldObjects.Count; j++)
                            {
                                if (worldObjects[j].def == WorldObjectDefOf.Settlement)
                                {
                                    nearbySettlement = true;
                                }
                            }
                            if (!nearbySettlement || ignoreRestrictions)
                            {                                 
                                shouldExecute = true;
                                context.destinationTile = destinationTile;
                                break;
                            }
                        }
                    }
                }
            }
            context.shouldExecute = shouldExecute;
        }

        private void AttemptSettlerOnMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayerTown = false, bool ignoreNearbyTown = false, ContextStorage context = null)
        {
            var shouldExecute = context.shouldExecute;
            var destinationTile = context.destinationTile;
            if(shouldExecute)
            {
                int pts = Mathf.RoundToInt(Rand.Range(.4f, .6f) * 500);
                WorldUtility.CreateSettler(pts, rwd, parentSettlement, parentSettlement.Tile, destinationTile, null); 
                rwsComp.RimWarPoints -= pts;
            }
        }

        public void AttemptSettler_UnThreaded(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool ignoreRestrictions = false, bool ignoreNearbyTown = false)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            bool shouldExecute = false;
            PlanetTile destinationTile = PlanetTile.Invalid;
            if (rwsComp.RimWarPoints > 500 || ignoreRestrictions)
            {
                int targetRange = Mathf.Clamp(rwsComp.SettlementScanRange, 11, Mathf.Max((int)settingsRef.maxSettelementScanRange, 12));
                if (rwd.behavior == RimWarBehavior.Expansionist)
                {
                    targetRange = Mathf.RoundToInt(targetRange * 1.2f);
                }
                else if (rwd.behavior == RimWarBehavior.Warmonger)
                {
                    targetRange = Mathf.RoundToInt(targetRange * .8f);
                }
                List<PlanetTile> tmpTiles = new List<PlanetTile>();
                tmpTiles.Clear();
                for (int i = 0; i < 5; i++)
                {
                    PlanetTile tile = PlanetTile.Invalid;
                    TileFinder.TryFindPassableTileWithTraversalDistance(parentSettlement.Tile, 10, targetRange, out tile);
                    if (tile.Valid)
                    {
                        Tile t = Find.WorldGrid[tile];
                        if (t.PrimaryBiome != null && !t.PrimaryBiome.isExtremeBiome && t.PrimaryBiome.canBuildBase)
                        {                            
                            tmpTiles.Add(tile);
                        }
                    }
                }
                if (tmpTiles != null && tmpTiles.Count > 0)
                {
                    for (int i = 0; i < tmpTiles.Count; i++)
                    {
                        destinationTile = tmpTiles[i];
                        if (destinationTile > 0 && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, destinationTile) <= targetRange || ignoreRestrictions))
                        {
                            List<WorldObject> worldObjects = WorldUtility.GetWorldObjectsInRange(destinationTile, 10);
                            bool nearbySettlement = false;
                            for (int j = 0; j < worldObjects.Count; j++)
                            {
                                if (worldObjects[j].def == WorldObjectDefOf.Settlement)
                                {
                                    nearbySettlement = true;
                                }
                            }
                            if (!nearbySettlement || ignoreRestrictions)
                            {
                                shouldExecute = true;
                                break;
                            }
                        }
                    }
                }
            }

            if (shouldExecute && destinationTile > 0)
            {
                int pts = Mathf.RoundToInt(Rand.Range(.4f, .6f) * 500);
                WorldUtility.CreateSettler(pts, rwd, parentSettlement, parentSettlement.Tile, destinationTile, null);
                rwsComp.RimWarPoints -= pts;
            }
        }

        public void AttemptSettlerMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool ignoreRestrictions = false, bool ignoreNearbyTown = false)
        {
            if (Options.Settings.Instance.threadingEnabled)
            {
                if (rwd != null && rwsComp != null)
                {
                    tasker.Register((Func<ContextStorage>)(() =>
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();

                        var context = new ContextStorage();
                        this.AttemptSettlerOffMainThread(rwd, parentSettlement, rwsComp, ignoreRestrictions, ignoreNearbyTown, context);

                        stopwatch.Stop();
                        if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                            string.Format("RIMWAR: settler mission off thread took {0} ms", stopwatch.ElapsedMilliseconds));
                        return context;
                    }),
                    (Action<ContextStorage>)((context) =>
                    {
                        Stopwatch stopwatch = new Stopwatch();
                        stopwatch.Start();
                        this.AttemptSettlerOnMainThread(rwd, parentSettlement, rwsComp, ignoreRestrictions, ignoreNearbyTown, context);

                        stopwatch.Stop();
                        if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                            string.Format("RIMWAR: settler mission on thread took {0} ms", stopwatch.ElapsedMilliseconds));
                    }));
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a settler: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
            else
            {
                if (rwd != null && rwsComp != null)
                {
                    this.AttemptSettler_UnThreaded(rwd, parentSettlement, rwsComp, ignoreRestrictions, ignoreNearbyTown);
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a settler: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
        }

        public void AttemptTradeMissionOffMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements.Add(rwd.NonHostileSettlements.RandomElement());
            }
            else if (forcePlayer)
            {
                tmpSettlements.AddRange(WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements);
            }
            else
            {
                tmpSettlements.AddRange(rwsComp.NearbyFriendlySettlements);
            }

            context.targetRange = targetRange;
            context.settingsRef = settingsRef;
            context.tmpSettlements = tmpSettlements;
        }

        public void AttemptTradeMissionOnMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false, ContextStorage context = null)
        {
            var targetRange = context.targetRange;
            var tmpSettlements = context.tmpSettlements;

            if (tmpSettlements != null && tmpSettlements.Count > 0)
            {
                RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                if (targetTown != null && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange || ignoreRestrictions))
                {
                    if (targetTown.Faction == Faction.OfPlayer)
                    {
                        if (rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                        {
                            return;
                        }
                        if (!WorldUtility.FactionCanTrade(rwsComp.parent.Faction) && !ignoreRestrictions && !forcePlayer)
                        {
                            return;
                        }
                    }
                    int pts = WorldUtility.CalculateTraderPoints(parentSettlement.GetComponent<RimWarSettlementComp>());
                    if (rwd.behavior == RimWarBehavior.Cautious)
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        pts = Mathf.RoundToInt(pts * .8f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Merchant)
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }
                    int maxPts = Mathf.RoundToInt(rwsComp.RimWarPoints * .5f);
                    if (maxPts >= pts || ignoreRestrictions)
                    {
                        Trader tdr = WorldUtility.CreateTrader(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                        rwsComp.RimWarPoints -= pts;
                        if (targetTown.Faction == Faction.OfPlayer)
                        {
                            rwsComp.PlayerHeat = 0;
                            minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Caravan);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        public void AttemptTradeMission_UnThreaded(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements.Add(rwd.NonHostileSettlements.RandomElement());
            }
            else if (forcePlayer)
            {
                tmpSettlements.AddRange(WorldUtility.GetRimWarDataForFaction(Faction.OfPlayer).WorldSettlements);
            }
            else
            {
                tmpSettlements.AddRange(rwsComp.NearbyFriendlySettlements);
            }

            if (tmpSettlements != null && tmpSettlements.Count > 0)
            {
                RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                if (targetTown != null && (Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange || ignoreRestrictions))
                {
                    if (targetTown.Faction == Faction.OfPlayer)
                    {
                        if (rwsComp.PlayerHeat < minimumHeatForPlayerAction && !ignoreRestrictions)
                        {
                            return;
                        }
                        if (!WorldUtility.FactionCanTrade(rwsComp.parent.Faction) && !ignoreRestrictions && !forcePlayer)
                        {
                            return;
                        }
                    }
                    int pts = WorldUtility.CalculateTraderPoints(parentSettlement.GetComponent<RimWarSettlementComp>());
                    if (rwd.behavior == RimWarBehavior.Cautious)
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        pts = Mathf.RoundToInt(pts * .8f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Merchant)
                    {
                        pts = Mathf.RoundToInt(pts * 1.2f);
                    }
                    int maxPts = Mathf.RoundToInt(rwsComp.RimWarPoints * .5f);
                    if (maxPts >= pts || ignoreRestrictions)
                    {
                        Trader tdr = WorldUtility.CreateTrader(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                        rwsComp.RimWarPoints -= pts;
                        if (targetTown.Faction == Faction.OfPlayer)
                        {
                            rwsComp.PlayerHeat = 0;
                            minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Caravan);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        public void AttemptTradeMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            if (false) //Options.Settings.Instance.threadingEnabled)
            {
                if (rwd != null && rwsComp != null && parentSettlement != null)
                {
                    Options.SettingsRef settingsRef = new Options.SettingsRef();
                    if (rwsComp.RimWarPoints > 200 || ignoreRestrictions)
                    {
                        tasker.Register((Func<ContextStorage>)(() =>
                        {
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();

                            var context = new ContextStorage();
                            this.AttemptTradeMissionOffMainThread(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, context);

                            stopwatch.Stop();
                            if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                                string.Format("RIMWAR: trade mission lauched against town mission off thread took {0} ms", stopwatch.ElapsedMilliseconds));
                            return context;
                        }),
                        (Action<ContextStorage>)((context) =>
                        {
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            this.AttemptTradeMissionOnMainThread(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions, context);

                            stopwatch.Stop();
                            if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                                string.Format("RIMWAR: trade mission lauched against town mission on thread took {0} ms", stopwatch.ElapsedMilliseconds));
                        }));
                    }
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a trader: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
            else
            {
                if (rwd != null && rwsComp != null && parentSettlement != null)
                {
                    Options.SettingsRef settingsRef = new Options.SettingsRef();
                    //if (rwsComp.RimWarPoints > 200 || ignoreRestrictions)
                    //{
                        this.AttemptTradeMission_UnThreaded(rwd, parentSettlement, rwsComp, forcePlayer, ignoreRestrictions);
                    //}
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a trader: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
        }

        private void AttemptDiplomatMissionOffMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, ContextStorage context = null)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements.Add(rwd.NonHostileSettlements.RandomElement());
                tmpSettlements.Add(rwd.HostileSettlements.RandomElement());
            }
            else
            {
                tmpSettlements = WorldUtility.GetRimWorldSettlementsInRange(parentSettlement.Tile, targetRange);
            }

            context.targetRange = targetRange;
            context.settingsRef = settingsRef;
            context.tmpSettlements = tmpSettlements;
        }

        private void AttemptDiplomatMissionOnMainThread(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, ContextStorage context = null)
        {
            var targetRange = context.targetRange;
            var tmpSettlements = context.tmpSettlements;

            if (tmpSettlements != null && tmpSettlements.Count > 0)
            {
                RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                if (targetTown != null && Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange)
                {
                    int pts = WorldUtility.CalculateDiplomatPoints(rwsComp);
                    if (rwd.behavior == RimWarBehavior.Cautious)
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        pts = Mathf.RoundToInt(pts * .8f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Merchant)
                    {
                        pts = Mathf.RoundToInt(pts * 1.3f);
                    }
                    float maxPts = rwsComp.RimWarPoints * .5f;
                    if (maxPts >= pts)
                    {
                        //Log.Message("sending warband from " + rwsComp.RimWorld_Settlement.Name);
                        WorldUtility.CreateDiplomat(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                        rwsComp.RimWarPoints -= pts;
                        if (targetTown.Faction == Faction.OfPlayer)
                        {
                            rwsComp.PlayerHeat = 0;
                            minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Diplomat);
                        }
                    }
                }
            }
        }

        public void AttemptDiplomatMission_UnThreaded(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp)
        {
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = rwsComp.SettlementScanRange;

            List<RimWorld.Planet.Settlement> tmpSettlements = new List<RimWorld.Planet.Settlement>();
            if (settingsRef.forceRandomObject)
            {
                tmpSettlements.Add(rwd.NonHostileSettlements.RandomElement());
                tmpSettlements.Add(rwd.HostileSettlements.RandomElement());
            }
            else
            {
                tmpSettlements = WorldUtility.GetRimWorldSettlementsInRange(parentSettlement.Tile, targetRange);
            }

            if (tmpSettlements != null && tmpSettlements.Count > 0)
            {
                RimWorld.Planet.Settlement targetTown = tmpSettlements.RandomElement();
                if (targetTown != null && Find.WorldGrid.ApproxDistanceInTiles(parentSettlement.Tile, targetTown.Tile) <= targetRange)
                {
                    int pts = WorldUtility.CalculateDiplomatPoints(rwsComp);
                    if (rwd.behavior == RimWarBehavior.Cautious)
                    {
                        pts = Mathf.RoundToInt(pts * 1.1f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Warmonger)
                    {
                        pts = Mathf.RoundToInt(pts * .8f);
                    }
                    else if (rwd.behavior == RimWarBehavior.Merchant)
                    {
                        pts = Mathf.RoundToInt(pts * 1.3f);
                    }
                    float maxPts = rwsComp.RimWarPoints * .5f;
                    if (maxPts >= pts)
                    {
                        //Log.Message("sending warband from " + rwsComp.RimWorld_Settlement.Name);
                        WorldUtility.CreateDiplomat(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
                        rwsComp.RimWarPoints -= pts;
                        if (targetTown.Faction == Faction.OfPlayer)
                        {
                            rwsComp.PlayerHeat = 0;
                            minimumHeatForPlayerAction += GetHeatForAction(RimWarAction.Diplomat);
                        }
                    }
                }
            }
        }

        private void AttemptDiplomatMission(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp)
        {
            if (Options.Settings.Instance.threadingEnabled)
            {
                if (rwd != null && rwsComp != null)
                {
                    Options.SettingsRef settingsRef = new Options.SettingsRef();
                    if (rwsComp.RimWarPoints > 1000)
                    {
                        tasker.Register((Func<ContextStorage>)(() =>
                        {
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();

                            var context = new ContextStorage();
                            this.AttemptDiplomatMissionOffMainThread(rwd, parentSettlement, rwsComp, context);

                            stopwatch.Stop();
                            if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                                string.Format("RIMWAR: diplomatic mission lauched against town mission off thread took {0} ms", stopwatch.ElapsedMilliseconds));
                            return context;
                        }),
                        (Action<ContextStorage>)((context) =>
                        {
                            Stopwatch stopwatch = new Stopwatch();
                            stopwatch.Start();
                            this.AttemptDiplomatMissionOnMainThread(rwd, parentSettlement, rwsComp, context);

                            stopwatch.Stop();
                            if (Prefs.DevMode && Prefs.LogVerbose) Log.Message(
                                string.Format("RIMWAR: diplomatic mission lauched against town mission on thread took {0} ms", stopwatch.ElapsedMilliseconds));
                        }));
                    }
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a diplomat: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
            else
            {
                if (rwd != null && rwsComp != null)
                {
                    Options.SettingsRef settingsRef = new Options.SettingsRef();
                    if (rwsComp.RimWarPoints > 1000)
                    {
                        this.AttemptDiplomatMission_UnThreaded(rwd, parentSettlement, rwsComp);
                    }
                }
                else
                {
                    Log.Warning("Found null when attempting to generate a diplomat: rwd " + rwd + " rwsComp " + rwsComp);
                }
            }
        }

        public void AttemptReinforcement(RimWarData rwd, RimWorld.Planet.Settlement parentSettlement, RimWarSettlementComp rwsComp, Settlement targetSettlement, bool forcePlayer = false, bool ignoreRestrictions = false)
        {
            if (rwd != null && rwsComp != null && parentSettlement != null)
            {
                Options.SettingsRef settingsRef = new Options.SettingsRef();
                if (rwsComp.RimWarPoints > 1000 || ignoreRestrictions)
                {
                    RimWorld.Planet.Settlement targetTown = targetSettlement;
                    if (targetTown != null)
                    {
                        int pts = Mathf.RoundToInt(.4f * rwsComp.RimWarPoints);
                        Trader tdr = WorldUtility.CreateTrader(pts, rwd, parentSettlement, parentSettlement.Tile, targetTown, WorldObjectDefOf.Settlement);
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
