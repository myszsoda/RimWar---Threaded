using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimWar.Options
{
    public class Controller : Mod
    {
        public static Controller Instance;
        private Vector2 scrollPosition = Vector2.zero;

        public override string SettingsCategory()
        {
            return "RimWar";
        }

        public Controller(ModContentPack content) : base(content)
        {
            Controller.Instance = this;
            Settings.Instance = base.GetSettings<Settings>();
            LongEventHandler.ExecuteWhenFinished(new Action(Controller.AddGroupPawnMakers));
            LongEventHandler.ExecuteWhenFinished(new Action(Controller.FactionAdjustments));
        }

        private static void FactionAdjustments()
        {
            //if(Settings.Instance.noPermanentEnemies)
            //{
            //    IEnumerable<FactionDef> enumerable = from def in DefDatabase<FactionDef>.AllDefs
            //                                         where (!def.hidden && !def.isPlayer && def.humanlikeFaction)
            //                                         select def;
            //    foreach(FactionDef fdef in enumerable)
            //    {
            //        fdef.permanentEnemy = false;
            //    }
            //}
        }

        private static void AddGroupPawnMakers()
        {
            IEnumerable<TraderKindDef> tKinds = from def in DefDatabase<TraderKindDef>.AllDefs
                                                 where (def.permitRequiredForTrading == null && !def.orbital)
                                                 select def;

            IEnumerable<FactionDef> enumerable = from def in DefDatabase<FactionDef>.AllDefs
                                               where (!def.hidden && !def.isPlayer)
                                               select def;

            List<PawnGenOption> pgoListTraderOptions = new List<PawnGenOption>();
            pgoListTraderOptions.Clear();

            List<PawnGenOption> pgoListCarrierOptions = new List<PawnGenOption>();
            pgoListCarrierOptions.Clear();

            foreach (FactionDef fd in enumerable)
            {
                bool hasCombat = false;
                bool hasSettlement = false;
                bool hasPeaceful = false;
                bool hasTrader = false;
                int factionDefGroupCount = 0;
                List<PawnGenOption> pgoListOptions = new List<PawnGenOption>();
                pgoListOptions.Clear();
                float totalCommonality = 0;
                if (fd.pawnGroupMakers != null && fd.pawnGroupMakers.Count > 0)
                {
                    factionDefGroupCount = fd.pawnGroupMakers.Count;
                    for(int i = 0; i < fd.pawnGroupMakers.Count; i++)
                    {
                        PawnGroupMaker pgm = fd.pawnGroupMakers[i];
                        foreach(PawnGenOption pgo in pgm.options)
                        {
                            pgoListOptions.Add(pgo);
                        }
                        if(pgm.kindDef == PawnGroupKindDefOf.Combat)
                        {
                            hasCombat = true;
                            totalCommonality += pgm.commonality;
                        }
                        else if(pgm.kindDef == PawnGroupKindDefOf.Peaceful)
                        {
                            hasPeaceful = true;
                            totalCommonality += pgm.commonality;
                        }
                        else if(pgm.kindDef == PawnGroupKindDefOf.Settlement)
                        {
                            hasSettlement = true;
                            totalCommonality += pgm.commonality;
                        }
                        else if(pgm.kindDef == PawnGroupKindDefOf.Trader)
                        {
                            hasTrader = true;
                            totalCommonality += pgm.commonality;
                            pgoListTraderOptions.AddRange(pgm.traders);
                            if (pgm.carriers == null)
                            {
                                pgm.carriers = new List<PawnGenOption>();
                                pgm.carriers.Clear();
                            }
                            if(pgm.carriers != null)
                            {
                                foreach (PawnGenOption pg in pgm.carriers)
                                {
                                    if (!pgoListCarrierOptions.Contains(pg))
                                    {
                                        pgoListCarrierOptions.Add(pg);
                                    }
                                }
                                PawnGenOption pgo1 = new PawnGenOption();
                                pgo1.kind = PawnKindDef.Named("Muffalo");
                                pgo1.selectionWeight = 1;
                                pgm.carriers.AddDistinct(pgo1);
                                PawnGenOption pgo2 = new PawnGenOption();
                                pgo2.kind = PawnKindDef.Named("Dromedary");
                                pgo2.selectionWeight = 1;
                                pgm.carriers.AddDistinct(pgo2);
                                PawnGenOption pgo3 = new PawnGenOption();
                                pgo3.kind = PawnKindDef.Named("Alpaca");
                                pgo3.selectionWeight = 1;
                                pgm.carriers.AddDistinct(pgo3);
                                PawnGenOption pgo4 = new PawnGenOption();
                                pgo4.kind = PawnKindDef.Named("Elephant");
                                pgo4.selectionWeight = 1;
                                pgm.carriers.AddDistinct(pgo4);                                
                            }
                        }
                    }
                }
                if(!hasCombat)
                {
                    //Make a combat group
                    Log.Message("Rim War: adding combat pawngroupmaker to FactionDef " + fd.defName);
                    PawnGroupMaker pgmCombat = new PawnGroupMaker();
                    pgmCombat.kindDef = PawnGroupKindDefOf.Combat;
                    pgmCombat.commonality = (totalCommonality/pgoListOptions.Count);
                    int ct = Rand.Range(2, 7);
                    for (int i = 0; i < ct; i++)
                    {
                        pgmCombat.options.Add(pgoListOptions.RandomElement());
                    }
                    fd.pawnGroupMakers.Add(pgmCombat);
                }
                if(!hasPeaceful)
                {
                    Log.Message("Rim War: adding peaceful pawngroupmaker to FactionDef " + fd.defName);
                    PawnGroupMaker pgmPeaceful = new PawnGroupMaker();
                    pgmPeaceful.kindDef = PawnGroupKindDefOf.Peaceful;
                    pgmPeaceful.commonality = 1;
                    int ct = Rand.Range(1, 5);
                    for (int i = 0; i < ct; i++)
                    {
                        pgmPeaceful.options.Add(pgoListOptions.RandomElement());
                    }
                    ct = Rand.Range(2, 7);
                    for (int i = 0; i < ct; i++)
                    {
                        pgmPeaceful.guards.Add(pgoListOptions.RandomElement());
                    }
                    if (pgoListTraderOptions != null && pgoListTraderOptions.Count > 0)
                    {
                        pgmPeaceful.traders.Add(pgoListTraderOptions.RandomElement());
                    }
                    else
                    {
                        pgmPeaceful.traders.Add(pgoListOptions.RandomElement());
                    }
                    foreach (PawnGenOption pgoc in pgoListCarrierOptions)
                    {
                        pgmPeaceful.carriers.Add(pgoc);
                    }
                    fd.pawnGroupMakers.Add(pgmPeaceful);
                }
                if(!hasTrader)
                {
                    Log.Message("Rim War: adding trader pawngroupmaker to FactionDef " + fd.defName);
                    PawnGroupMaker pgmTrader = new PawnGroupMaker();
                    pgmTrader.kindDef = PawnGroupKindDefOf.Trader;
                    pgmTrader.commonality = 1;                    
                    int ct = Rand.Range(2, 7);
                    for (int i = 0; i < ct; i++)
                    {
                        pgmTrader.guards.Add(pgoListOptions.RandomElement());
                    }
                    if (pgoListTraderOptions != null && pgoListTraderOptions.Count > 0)
                    {
                        pgmTrader.traders.Add(pgoListTraderOptions.RandomElement());
                    }
                    else
                    {
                        pgmTrader.traders.Add(pgoListOptions.RandomElement());
                    }                    
                    foreach(PawnGenOption pgoc in pgoListCarrierOptions)
                    {
                        pgmTrader.carriers.Add(pgoc);
                    }
                    fd.pawnGroupMakers.Add(pgmTrader);
                }
                if(!hasSettlement)
                {
                    Log.Message("Rim War: adding settlement pawngroupmaker to FactionDef " + fd.defName);
                    PawnGroupMaker pgmSettlement = new PawnGroupMaker();
                    pgmSettlement.kindDef = PawnGroupKindDefOf.Settlement;
                    pgmSettlement.commonality = 1;
                    int ct = Rand.Range(3, 10);
                    for (int i = 0; i < ct; i++)
                    {
                        pgmSettlement.options.Add(pgoListOptions.RandomElement());
                    }
                    fd.pawnGroupMakers.Add(pgmSettlement);
                }

                if (tKinds != null && tKinds.Count() > 0)
                {
                    if (fd.baseTraderKinds == null || fd.baseTraderKinds.Count == 0)
                    {
                        fd.baseTraderKinds = new List<TraderKindDef>();
                        fd.baseTraderKinds.Add(tKinds.RandomElement());
                    }
                }
                if (tKinds != null && tKinds.Count() > 0)
                {
                    if (fd.caravanTraderKinds == null || fd.caravanTraderKinds.Count == 0)
                    {
                        fd.caravanTraderKinds = new List<TraderKindDef>();
                        fd.caravanTraderKinds.Add(tKinds.RandomElement());
                    }
                }
            }
        }

        public override void DoSettingsWindowContents(Rect canvas)
        {
            int num = 0;
            float rowHeight = 32f;

            Rect sRect = new Rect(canvas.x, canvas.y, canvas.width - 36f, canvas.height + 116f);
            scrollPosition = GUI.BeginScrollView(canvas, scrollPosition, sRect, false, true);
            //Widgets.BeginScrollView(canvas, ref scrollPosition, canvas, true);

            SettingsRef settingsRef = new SettingsRef();
            Rect rect1 = new Rect(canvas);
            rect1.width *= .4f;
            num++;
            num++;
            Rect rowRect1 = UIHelper.GetRowRect(rect1, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect1, "RW_storytellerBasedDifficulty".Translate(), ref Settings.Instance.storytellerBasedDifficulty, false);
            TooltipHandler.TipRegion(rowRect1, "RW_storytellerBasedDifficultyInfo".Translate());
            Rect rowRect1ShiftRight = UIHelper.GetRowRect(rowRect1, rowHeight, num);
            rowRect1ShiftRight.x += rowRect1.width + 56f;
            if (!Settings.Instance.storytellerBasedDifficulty)
            {
                Settings.Instance.rimwarDifficulty = Widgets.HorizontalSlider(rowRect1ShiftRight, Settings.Instance.rimwarDifficulty, .5f, 2f, false, "RW_rimwarDifficulty".Translate() + " " + Settings.Instance.rimwarDifficulty, "0.5", "2", .1f);
            }
            num++;
            Rect rowRect12 = UIHelper.GetRowRect(rect1, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect12, "RW_useRimWarVictory".Translate(), ref Settings.Instance.useRimWarVictory, false);
            TooltipHandler.TipRegion(rowRect12, "RW_useRimWarVictoryInfo".Translate());            
            Rect rowRect12ShiftRight = UIHelper.GetRowRect(rowRect12, rowHeight, num);
            rowRect12ShiftRight.x += rowRect12.width + 56f;
            Settings.Instance.heatMultiplier = Widgets.HorizontalSlider(rowRect12ShiftRight, Settings.Instance.heatMultiplier, 0f, 2f, false, "RW_heatOffset".Translate() + " " + Settings.Instance.heatMultiplier.ToString("P0"), "0%", "200%", .1f);
            TooltipHandler.TipRegion(rowRect12ShiftRight, "RW_heatOffsetInfo".Translate());
            num++;
            Rect rowRect = UIHelper.GetRowRect(rect1, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect, "RW_randomizeFactionBehavior".Translate(), ref Settings.Instance.randomizeFactionBehavior, false);
            TooltipHandler.TipRegion(rowRect, "RW_randomizeFactionBehaviorInfo".Translate());
            Rect rowRectShiftRight = UIHelper.GetRowRect(rowRect, rowHeight, num);
            rowRectShiftRight.x += rowRect.width + 56f;
            Settings.Instance.heatFrequency = Widgets.HorizontalSlider(rowRectShiftRight, Settings.Instance.heatFrequency, 800f, 10000f, false, "RW_heatFrequency".Translate(((Settings.Instance.heatFrequency/2500f)).ToString("#.#")), "1/4", "4", 10f);
            TooltipHandler.TipRegion(rowRectShiftRight, "RW_heatFrequencyInfo".Translate());
            num++;
            Rect rowRect11 = UIHelper.GetRowRect(rowRect1, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect11, "RW_forceRandomObject".Translate(), ref Settings.Instance.forceRandomObject, false);
            TooltipHandler.TipRegion(rowRect11, "RW_forceRandomObjectInfo".Translate());
            Rect rowRect11ShiftRight = UIHelper.GetRowRect(rowRect, rowHeight, num);
            rowRect11ShiftRight.x += rowRect.width + 56f;
            Settings.Instance.settlementGrowthRate = Widgets.HorizontalSlider(rowRect11ShiftRight, Settings.Instance.settlementGrowthRate, 0f, 10f, false, "RW_settlementGrowthRate".Translate(Settings.Instance.settlementGrowthRate.ToString("P0")), "0%", "1000%", .1f);
            TooltipHandler.TipRegion(rowRect11ShiftRight, "RW_settlementGrowthRateInfo".Translate());
            //Widgets.CheckboxLabeled(rowRect11, "RW_createDiplomats".Translate(), ref Settings.Instance.createDiplomats, false);
            num++;
            Rect rowRect13 = UIHelper.GetRowRect(rowRect12, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect13, "RW_restrictEvents".Translate(), ref Settings.Instance.restrictEvents, false);
            TooltipHandler.TipRegion(rowRect13, "RW_restrictEventsInfo".Translate());
            num++;
            Rect rowRect131 = UIHelper.GetRowRect(rowRect13, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect131, "RW_allowDropPodRaids".Translate(), ref Settings.Instance.allowDropPodRaids, false);
            TooltipHandler.TipRegion(rowRect131, "RW_allowDropPodRaidsInfo".Translate());
            num++;
            Rect rowRect14 = UIHelper.GetRowRect(rowRect131, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect14, "RW_threadingEnabled".Translate(), ref Settings.Instance.threadingEnabled, false);
            TooltipHandler.TipRegion(rowRect14, "RW_threadingEnabledInfo".Translate());
            num++;
            //num++;
            Rect rowRect6 = UIHelper.GetRowRect(rowRect13, rowHeight, num);
            rowRect6.width = canvas.width * .8f;
            Settings.Instance.averageEventFrequency = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect6, Settings.Instance.averageEventFrequency, 10, 1000, false, "RW_eventFrequency".Translate() + " " + Settings.Instance.averageEventFrequency, "Fast", "Slow", 1f));
            TooltipHandler.TipRegion(rowRect6, "RW_eventFrequencyInfo".Translate());            
            num++;
            Rect rowRect4 = UIHelper.GetRowRect(rowRect6, rowHeight, num);
            Settings.Instance.settlementScanRangeDivider = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect4, Settings.Instance.settlementScanRangeDivider, 20, 200, false, "RW_scanRange".Translate() + " " + Mathf.RoundToInt(1000 / Settings.Instance.settlementScanRangeDivider), "Far", "Close", 1f));
            TooltipHandler.TipRegion(rowRect4, "RW_scanRangeInfo".Translate());
            num++;
            Rect rowRect3 = UIHelper.GetRowRect(rowRect4, rowHeight, num);
            Settings.Instance.maxSettlementScanRange = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect3, Settings.Instance.maxSettlementScanRange, 20, 200, false, "RW_maxScanRange".Translate() + " " + Settings.Instance.maxSettlementScanRange, "20", "200", 1f));
            TooltipHandler.TipRegion(rowRect3, "RW_maxScanRangeInfo".Translate());
            num++;
            Rect rowRect91 = UIHelper.GetRowRect(rowRect3, rowHeight, num);
            Settings.Instance.rwdUpdateFrequency = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect91, Settings.Instance.rwdUpdateFrequency, 2500, 60000, false, "RW_rwdUpdateFrequency".Translate() + " " + Mathf.RoundToInt(Settings.Instance.rwdUpdateFrequency / 2500), "1", "24", 1f));
            TooltipHandler.TipRegion(rowRect91, "RW_rwdUpdateFrequencyInfo".Translate());            
            num++;
            Rect rowRect2 = UIHelper.GetRowRect(rowRect91, rowHeight, num);
            Settings.Instance.maxFactionSettlements = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect2, Settings.Instance.maxFactionSettlements, 1, 100, false, "RW_maxFactionSettlements".Translate() + " " + Settings.Instance.maxFactionSettlements, "1", "100", 1f));
            TooltipHandler.TipRegion(rowRect2, "RW_maxFactionSettlementsInfo".Translate());
            num++;
            Rect rowRect7 = UIHelper.GetRowRect(rowRect2, rowHeight, num);
            Settings.Instance.settlementEventDelay = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect7, Settings.Instance.settlementEventDelay, 2500, 240000, false, "RW_settlementEventFrequency".Translate() + " " + Mathf.RoundToInt(Settings.Instance.settlementEventDelay/2500f), "1", "96", 10f));
            TooltipHandler.TipRegion(rowRect7, "RW_settlementEventFrequencyInfo".Translate());
            num++;
            Rect rowRect8 = UIHelper.GetRowRect(rowRect7, rowHeight, num);
            Settings.Instance.settlementScanDelay = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect8, Settings.Instance.settlementScanDelay, 2500, 240000, false, "RW_settlementScanFrequency".Translate() + " " + Mathf.RoundToInt(Settings.Instance.settlementScanDelay/2500f), "1", "96", 10f));
            TooltipHandler.TipRegion(rowRect8, "RW_settlementScanFrequencyInfo".Translate());
            num++;
            Rect rowRect9 = UIHelper.GetRowRect(rowRect8, rowHeight, num);
            Settings.Instance.woEventFrequency = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect9, Settings.Instance.woEventFrequency, 10, 1000, false, "RW_warobjectActionFrequency".Translate() + " " + ((float)(Settings.Instance.woEventFrequency/60f)).ToString("#.0"), "Fast", "Slow", .1f));
            TooltipHandler.TipRegion(rowRect9, "RW_warobjectActionFrequencyInfo".Translate());
            num++;
            Rect rowRect5 = UIHelper.GetRowRect(rowRect4, rowHeight, num);
            Settings.Instance.objectMovementMultiplier = Widgets.HorizontalSlider(rowRect5, Settings.Instance.objectMovementMultiplier, .2f, 5f, false, "RW_objectMovementMultiplier".Translate() + " " + Settings.Instance.objectMovementMultiplier, "Slow", "Fast", .1f);
            TooltipHandler.TipRegion(rowRect5, "RW_objectMovementMultiplierInfo".Translate());
            num++;
            Rect rowRect92 = UIHelper.GetRowRect(rowRect91, rowHeight, num);
            Settings.Instance.alertRange = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect92, Settings.Instance.alertRange, 0, 20, false, "RW_alertRange".Translate() + " " + Mathf.RoundToInt(Settings.Instance.alertRange), "0", "20", 1f));
            TooltipHandler.TipRegion(rowRect92, "RW_alertRangeInfo".Translate());
            num++;
            Rect rowRect93 = UIHelper.GetRowRect(rowRect92, rowHeight, num);
            Settings.Instance.letterNotificationRange = Mathf.RoundToInt(Widgets.HorizontalSlider(rowRect93, Settings.Instance.letterNotificationRange, 0, 10, false, "RW_letterNotificationRange".Translate() + " " + Mathf.RoundToInt(Settings.Instance.letterNotificationRange), "0", "10", 1f));
            TooltipHandler.TipRegion(rowRect93, "RW_letterNotificationRangeInfo".Translate());
            //Widgets.CheckboxLabeled(rowRect92, "RW_forceRandomObject".Translate(), ref Settings.Instance.forceRandomObject, false);
            num++;
            Rect rowRect94 = UIHelper.GetRowRect(rowRect1, rowHeight, num);
            Widgets.CheckboxLabeled(rowRect94, "RW_alertVassal".Translate(), ref Settings.Instance.vassalNotification, false);
            TooltipHandler.TipRegion(rowRect94, "RW_alertVassalInfo".Translate());
            num++;
            num++;
            Rect rowRect20 = UIHelper.GetRowRect(rowRect92, rowHeight, num);
            rowRect20.width = 120f;

            bool resetDefault = Widgets.ButtonText(rowRect20, "Default", true, false, true);
            if (resetDefault)
            {
                Settings.Instance.storytellerBasedDifficulty = true;
                Settings.Instance.rimwarDifficulty = 1f;                
                Settings.Instance.heatMultiplier = 1f;
                Settings.Instance.heatFrequency = 3000f;
                Settings.Instance.randomizeFactionBehavior = false;              
                Settings.Instance.createDiplomats = false;
                Settings.Instance.forceRandomObject = false;
                Settings.Instance.settlementGrowthRate = 1f;
                
                Settings.Instance.averageEventFrequency = 150;

                Settings.Instance.settlementScanRangeDivider = 70f;
                Settings.Instance.maxSettlementScanRange = 75;
                Settings.Instance.rwdUpdateFrequency = 2500;
                Settings.Instance.maxFactionSettlements = 40;
                Settings.Instance.settlementScanDelay = 60000;
                Settings.Instance.settlementEventDelay = 50000;

                Settings.Instance.woEventFrequency = 200;
                Settings.Instance.objectMovementMultiplier = 1f;

                Settings.Instance.letterNotificationRange = 7;                
                Settings.Instance.alertRange = 6;
                Settings.Instance.vassalNotification = false;
                Settings.Instance.allowDropPodRaids = true;
            }

            Rect rowRect21 = UIHelper.GetRowRect(rowRect20, rowHeight, num);
            rowRect21.x = rowRect20.x + 130;
            bool setPerformance = Widgets.ButtonText(rowRect21, "Performance", true, false, true);
            if (setPerformance)
            {
                //Settings.Instance.storytellerBasedDifficulty = true;
                //Settings.Instance.rimwarDifficulty = 1f;
                //Settings.Instance.heatMultiplier = 1f;
                //Settings.Instance.randomizeFactionBehavior = false;
                //Settings.Instance.createDiplomats = false;
                Settings.Instance.forceRandomObject = false;

                Settings.Instance.averageEventFrequency = 600;

                Settings.Instance.settlementScanRangeDivider = 120f;
                Settings.Instance.maxSettlementScanRange = 50;
                Settings.Instance.rwdUpdateFrequency = 10000;
                Settings.Instance.maxFactionSettlements = 20;
                Settings.Instance.settlementScanDelay = 120000;
                Settings.Instance.settlementEventDelay = 120000;

                Settings.Instance.woEventFrequency = 300;
                Settings.Instance.objectMovementMultiplier = 1f;

                Settings.Instance.letterNotificationRange = 4;
                Settings.Instance.alertRange = 0;
            }

            Rect rowRect22 = UIHelper.GetRowRect(rowRect21, rowHeight, num);
            rowRect22.x = rowRect21.x + 130;
            bool setLargeMap = Widgets.ButtonText(rowRect22, "Large Maps", true, false, true);
            if (setLargeMap)
            {
                //Settings.Instance.storytellerBasedDifficulty = true;
                //Settings.Instance.rimwarDifficulty = 1f;
                //Settings.Instance.heatMultiplier = 1f;
                //Settings.Instance.randomizeFactionBehavior = false;
                //Settings.Instance.createDiplomats = false;
                Settings.Instance.forceRandomObject = false;

                Settings.Instance.averageEventFrequency = 100;

                //Settings.Instance.settlementScanRangeDivider = 70f;
                //Settings.Instance.maxSettlementScanRange = 75;
                Settings.Instance.rwdUpdateFrequency = 5000;
                Settings.Instance.maxFactionSettlements = 50;
                Settings.Instance.settlementScanDelay = 120000;
                Settings.Instance.settlementEventDelay = 100000;

                Settings.Instance.woEventFrequency = 550;
                Settings.Instance.objectMovementMultiplier = 1.2f;

                Settings.Instance.letterNotificationRange = 5;
                Settings.Instance.alertRange = 4;
            }

            Rect rowRect23 = UIHelper.GetRowRect(rowRect22, rowHeight, num);
            rowRect23.x = rowRect22.x + 130;
            bool setDifficultyEasy = Widgets.ButtonText(rowRect23, "Easy", true, false, true);
            if (setDifficultyEasy)
            {
                Settings.Instance.storytellerBasedDifficulty = false;
                Settings.Instance.rimwarDifficulty = .75f;
                Settings.Instance.heatMultiplier = 1.5f;
                Settings.Instance.heatFrequency = 4000f;
                Settings.Instance.settlementGrowthRate = .8f;
                Settings.Instance.randomizeFactionBehavior = true;
                //Settings.Instance.createDiplomats = false;
                //Settings.Instance.forceRandomObject = true;

                //Settings.Instance.averageEventFrequency = 100;

                //Settings.Instance.settlementScanRangeDivider = 70f;
                //Settings.Instance.maxSettlementScanRange = 75;
                //Settings.Instance.rwdUpdateFrequency = 5000;
                Settings.Instance.maxFactionSettlements = 20;
                Settings.Instance.settlementScanDelay = 120000;
                Settings.Instance.settlementEventDelay = 100000;

                Settings.Instance.woEventFrequency = 450;
                Settings.Instance.objectMovementMultiplier = .8f;

                Settings.Instance.letterNotificationRange = 7;
                Settings.Instance.alertRange = 6;
                Settings.Instance.allowDropPodRaids = false;
            }

            Rect rowRect24 = UIHelper.GetRowRect(rowRect23, rowHeight, num);
            rowRect24.x = rowRect23.x + 130;
            bool setDifficultyHard = Widgets.ButtonText(rowRect24, "Hard", true, false, true);
            if (setDifficultyHard)
            {
                Settings.Instance.storytellerBasedDifficulty = false;
                Settings.Instance.rimwarDifficulty = 1.5f;
                Settings.Instance.heatFrequency = 1500f;
                Settings.Instance.heatMultiplier = .8f;
                Settings.Instance.settlementGrowthRate = 1.4f;
                Settings.Instance.randomizeFactionBehavior = true;
                Settings.Instance.allowDropPodRaids = true;
                //Settings.Instance.createDiplomats = false;
                //Settings.Instance.forceRandomObject = true;

                //Settings.Instance.averageEventFrequency = 100;

                //Settings.Instance.settlementScanRangeDivider = 70f;
                //Settings.Instance.maxSettlementScanRange = 75;
                //Settings.Instance.rwdUpdateFrequency = 5000;
                Settings.Instance.maxFactionSettlements = 50;
                Settings.Instance.settlementScanDelay = 30000;
                Settings.Instance.settlementEventDelay = 30000;

                Settings.Instance.woEventFrequency = 200;
                Settings.Instance.objectMovementMultiplier = 1f;

                Settings.Instance.letterNotificationRange = 0;
                Settings.Instance.alertRange = 0;
            }

            //Widgets.EndScrollView();
            GUI.EndScrollView();

        }

        public static class UIHelper
        {
            public static Rect GetRowRect(Rect inRect, float rowHeight, int row)
            {
                float y = rowHeight * (float)row;
                Rect result = new Rect(inRect.x, y, inRect.width, rowHeight);
                return result;
            }
        }

    }
}
