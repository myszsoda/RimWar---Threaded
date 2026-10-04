using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using RimWorld.Planet;
using RimWorld.BaseGen;
using RimWorld.SketchGen;
using UnityEngine;
using Verse;
using Verse.AI;
using RimWar.Planet;
using LudeonTK;

namespace RimWar.Utility
{
    public static class RimWar_DebugToolsPlanet
    {
        [DebugAction("Rim War", "Add 1k pts", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void Add1000RWP()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null && s.Faction != Faction.OfPlayer)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null && rwsc.parent.Faction != Faction.OfPlayer)
                    {
                        rwsc.RimWarPoints += 1000;
                    }
                }
                WarObject rwo = Find.WorldObjects.WorldObjectAt(tile, RimWarDefOf.RW_WarObject) as WarObject;
                if (rwo != null)
                {
                    rwo.RimWarPoints += 1000;
                }
            }
        }

        [DebugAction("Rim War", "Damage 100", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void Add100PtDamage()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null && s.Faction != Faction.OfPlayer)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null && rwsc.parent.Faction != Faction.OfPlayer)
                    {
                        rwsc.PointDamage += 100;
                    }
                }
                WarObject rwo = Find.WorldObjects.WorldObjectAt(tile, RimWarDefOf.RW_WarObject) as WarObject;
                if (rwo != null)
                {
                    rwo.PointDamage += 100;
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnTrader()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptTradeMission(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, false, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnTraderToPlayer()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptTradeMission(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, true, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnScout()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptScoutMission(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, false, false, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnScoutToPlayerTown()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptScoutMission(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, true, false, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnScoutToPlayerCaravan()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptScoutMission(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, false, true, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnScoutToVassal()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        List<Settlement> validVassalSettlements = new List<Settlement>();
                        validVassalSettlements.Clear();
                        for (int i = 0; i < Find.WorldObjects.Settlements.Count; i++)
                        {
                            Settlement v = Find.WorldObjects.Settlements[i];
                            if (WorldUtility.IsVassalFaction(v.Faction))
                            {
                                validVassalSettlements.Add(v);
                            }
                        }
                        if (validVassalSettlements != null && validVassalSettlements.Count > 0)
                        {
                            Settlement closest = validVassalSettlements[0];
                            for (int i = 1; i < validVassalSettlements.Count; i++)
                            {
                                closest = (Settlement)WorldUtility.ReturnCloserWorldObjectTo(closest, validVassalSettlements[i], tile);
                            }
                            if (closest != null)
                            {
                                WorldUtility.CreateScout(Rand.Range(100, 2000), rwsc.RWD, s, s.Tile, closest, WorldObjectDefOf.Settlement);
                            }
                        }
                        else
                        {
                            Log.Message("No vassal settlements found");
                        }
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnWarband()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptWarbandActionAgainstTown(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, false, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnWarbandToPlayer()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptWarbandActionAgainstTown(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, true, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnLaunchedWarband()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptLaunchedWarbandAgainstTown(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, false, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnLaunchedWarbandToPlayer()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptLaunchedWarbandAgainstTown(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, true, true);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnSettler()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptSettlerMission(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, false, false);
                    }
                }
            }
        }

        [DebugAction("Rim War", null, actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void SpawnSettlerIgnoreProximity()
        {
            int tile = GenWorld.MouseTile();
            if (tile < 0 || Find.World.Impassable(tile))
            {
                Messages.Message("Impassable", MessageTypeDefOf.RejectInput, historical: false);
            }
            else
            {
                RimWorld.Planet.Settlement s = Find.WorldObjects.SettlementAt(tile);
                if (s != null)
                {
                    RimWarSettlementComp rwsc = WorldUtility.GetRimWarSettlementAtTile(tile);
                    if (rwsc != null)
                    {
                        WorldUtility.Get_WCPT().AttemptSettlerMission(WorldUtility.GetRimWarDataForFaction(s.Faction), s, rwsc, false, true);
                    }
                }
            }
        }

        [DebugAction("Rim War - Debug Log", "War Object Report", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void LogWarObjectData()
        {
            int rwoCount = 0;
            int rwoPts = 0;
            StringBuilder str = new StringBuilder();
            List<RimWarData> rwdList = WorldUtility.GetRimWarData();
            if (rwdList != null)
            {
                if (rwdList.Count > 0)
                {
                    List<WorldObject> woList = Find.WorldObjects.AllWorldObjects;
                    if (woList != null && woList.Count > 0)
                    {
                        for (int i = 0; i < woList.Count; i++)
                        {
                            str.Clear();
                            if (woList[i] is WarObject)
                            {
                                rwoCount++;
                                WarObject rwo = woList[i] as WarObject;
                                str.Append(rwo.Name + " ID: " + rwo.ID);
                                if (rwo.rimwarData == null || rwo.rimwarData.WorldSettlements == null || rwo.rimwarData.WorldSettlements.Count <= 0)
                                {
                                    Log.Warning("Invalid Rim War Data!");
                                }
                                if (rwo.ParentSettlement != null)
                                {
                                    rwoPts += rwo.RimWarPoints;
                                    if (rwo.ParentSettlement != null)
                                    {
                                        str.Append(" Parent: " + rwo.ParentSettlement.Label + " ID " + rwo.ParentSettlement.ID);
                                        if (rwo.ParentSettlement.Destroyed)
                                        {
                                            Log.Warning("Parent Settlement is Destroyed!");
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("Parent Settlement has Null World Object!");
                                    }
                                }
                                else
                                {
                                    Log.Warning("No Parent Settlement!");
                                }
                                if (rwo.UseDestinationTile)
                                {
                                    if (Find.WorldObjects.AnySettlementAt(rwo.DestinationTile))
                                    {
                                        Log.Warning("Settlement detected at destination!");
                                    }
                                }
                                else if (rwo.DestinationTarget != null)
                                {
                                    str.Append(" Destination " + rwo.DestinationTarget.Label + " ID " + rwo.DestinationTarget.ID);
                                    if (!rwo.DestinationTarget.Destroyed)
                                    {
                                        if (!rwo.canReachDestination)
                                        {
                                            Log.Warning("Pather unable to reach destination!");
                                        }
                                        int distance = Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(rwo.Tile, rwo.DestinationTarget.Tile));
                                        if (distance > 100)
                                        {
                                            Log.Warning("Object travel distance is " + distance);
                                        }
                                    }
                                    else
                                    {
                                        Log.Warning("Destination destroyed!");
                                    }
                                }
                                else
                                {
                                    Log.Warning("No Destination or Destination is Null!");
                                }
                                Log.Message("" + str);
                            }
                        }
                    }
                    else
                    {
                        Log.Warning("Debug: no world objects found");
                    }
                }
                else
                {
                    Log.Warning("Debug: rwd count = 0");
                }
                Log.Message("Total objects: " + rwoCount);
                Log.Message("Total points: " + rwoPts);
            }
            else
            {
                Log.Warning("Debug: Rim War Data was null.");
            }
        }

        [DebugAction("Rim War - Debug Log", "Settlements in Range", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void LogRimWarSettlementsInRange()
        {
            List<Settlement> settlementsInRange = new List<Settlement>();
            int tile = GenWorld.MouseTile();
            WorldObject wo = Find.WorldObjects.SettlementAt(tile);
            RimWarSettlementComp rwsc = wo.GetComponent<RimWarSettlementComp>();
            Options.SettingsRef settingsRef = new Options.SettingsRef();
            int targetRange = Mathf.RoundToInt(rwsc.RimWarPoints / settingsRef.settlementScanRangeDivider);
            List<WorldObject> woList = WorldUtility.GetWorldObjectsInRange(tile, targetRange);
            for (int i = 0; i < woList.Count; i++)
            {
                if (woList[i] is Settlement)
                {
                    Settlement s = woList[i] as Settlement;
                    Log.Message(s.Label + " in range of " + rwsc.parent.Label);
                }
            }
        }

        [DebugAction("Rim War - Debug Log", "Settlement Summary Log", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void LogRimWarSettlementData()
        {
            Debug_FixRimWarSettlements(true, false);
        }

        [DebugAction("Rim War - Debug", "Debug Settlements", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void DebugRimWarSettlementData()
        {
            Debug_FixRimWarSettlements(true, true);
        }

        [DebugAction("Rim War - Debug", "Reset Capitols", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void DebugRimWarLegacyCapitols()
        {
            foreach (RimWarData rwd in WorldUtility.GetRimWarData())
            {
                rwd.ClearCapitol();
            }
            List<WorldObject> remList = new List<WorldObject>();
            for (int i = 0; i < Find.WorldObjects.AllWorldObjects.Count; i++)
            {
                if (Find.WorldObjects.AllWorldObjects[i] is CapitolBuilding)
                {
                    remList.Add(Find.WorldObjects.AllWorldObjects[i]);
                }
            }
            if (remList.Count > 0)
            {
                for (int i = 0; i < remList.Count; i++)
                {
                    remList[i].Destroy();
                }
            }
        }

        public static void Debug_FixRimWarSettlements(bool generateReport = false, bool cleanupErrors = false)
        {
            int rwsCount = 0;
            int wosCount = 0;
            int rws_no_wosCount = 0;
            int wos_no_rwsCount = 0;
            int factionMismatchCount = 0;

            List<WorldObject> woList = Find.WorldObjects.AllWorldObjects;
            List<WorldObject> wosList = new List<WorldObject>();
            wosList.Clear();
            for (int i = 0; i < woList.Count; i++)
            {
                RimWorld.Planet.Settlement wos = woList[i] as RimWorld.Planet.Settlement;
                if (wos != null)
                {
                    wosList.Add(wos);
                    wosCount++;
                    RimWarSettlementComp rws = WorldUtility.GetRimWarSettlementAtTile(wos.Tile);
                    if (rws != null)
                    {
                        if (wos.Destroyed)
                        {
                            if (generateReport) { Log.Warning(wos.Label + " destroyed but has RWSC"); }
                            if (cleanupErrors)
                            {
                                if (generateReport) { Log.Message("Cleaning RWS..."); }
                                //WorldUtility.GetRimWarDataForFaction(wos.Faction)?.WorldSettlements?.Remove(rws);
                            }
                        }
                        else if (wos.Faction != rws.parent.Faction)
                        {
                            factionMismatchCount++;
                            if (generateReport) { Log.Warning(wos.Label + " of Faction " + wos.Faction + " different from RWS Faction " + rws.parent.Faction); }
                            if (cleanupErrors)
                            {
                                if (generateReport) { Log.Message("Removing RWS from " + rws.parent.Faction + "..."); }
                                //WorldUtility.GetRimWarDataForFaction(rws.parent.Faction)?.WorldSettlements?.Remove(rws);
                                if (generateReport) { Log.Message("Adding RWS to " + wos.Faction + "..."); }
                                //rws.Faction = wos.Faction;
                                //WorldUtility.GetRimWarDataForFaction(wos.Faction)?.WorldSettlements?.Add(rws);
                            }
                        }
                    }
                    else
                    {
                        wos_no_rwsCount++;
                        if (generateReport) { Log.Warning("" + wos.Label + " has no RWS"); }
                        if (cleanupErrors)
                        {
                            if (generateReport) { Log.Message("Generating RWS for " + wos.Label + "..."); }
                            WorldUtility.CreateRimWarSettlement(WorldUtility.GetRimWarDataForFaction(wos.Faction), wos);
                        }
                    }
                }
            }

            List<RimWarData> rwdList = WorldUtility.Get_WCPT().RimWarData;
            for (int i = 0; i < rwdList.Count; i++)
            {
                RimWarData rwd = rwdList[i];
                if (rwd.WorldSettlements != null)
                {
                    for (int j = 0; j < rwd.WorldSettlements.Count; j++)
                    {
                        RimWarSettlementComp rws = rwd.WorldSettlements[j].GetComponent<RimWarSettlementComp>();
                        rwsCount++;
                        int wosHere = 0;
                        List<RimWorld.Planet.Settlement> wosHereList = new List<RimWorld.Planet.Settlement>();
                        wosHereList.Clear();
                        for (int k = 0; k < wosList.Count; k++)
                        {
                            if (wosList[k].Tile != rws.parent.Tile)
                            {
                                continue;
                            }
                            wosHere++;
                            wosHereList.Add(wosList[k] as RimWorld.Planet.Settlement);
                            if (wosList[k].Faction != rws.parent.Faction)
                            {
                                factionMismatchCount++;
                                if (generateReport) { Log.Warning(wosList[k].Label + " of Faction " + wosList[k].Faction + " different from RWS Faction " + rws.parent.Faction); }
                                if (cleanupErrors)
                                {
                                    if (generateReport) { Log.Message("Removing RWS from " + rws.parent.Faction + "..."); }
                                    //WorldUtility.GetRimWarDataForFaction(rws.parent.Faction)?.WorldSettlements?.Remove(rws);
                                    if (generateReport) { Log.Message("Adding RWS to " + wosList[k].Faction + "..."); }
                                    //rws.Faction = wosList[k].Faction;
                                    //WorldUtility.GetRimWarDataForFaction(wosList[k].Faction)?.WorldSettlements?.Add(rws);
                                }
                            }
                        }
                        if (wosHere == 0)
                        {
                            rws_no_wosCount++;
                            if (generateReport) { Log.Warning("No settlement found at " + Find.WorldGrid.LongLatOf(rws.parent.Tile)); }
                            if (cleanupErrors)
                            {
                                if (generateReport) { Log.Warning("Removing RWS..."); }
                                //rwd.FactionSettlements.Remove(rws);
                            }
                        }
                        if (wosHere > 1)
                        {
                            if (generateReport) { Log.Warning("Stacked settlements (" + wosHere + ") found at " + Find.WorldGrid.LongLatOf(rws.parent.Tile)); }
                            if (cleanupErrors)
                            {
                                while (wosHereList.Count > 1)
                                {
                                    if (generateReport) { Log.Message("Destroying settlement..."); }
                                    RimWorld.Planet.Settlement wosHereDes = wosHereList[0];
                                    wosHereList.Remove(wosHereDes);
                                    if (!wosHereDes.Destroyed)
                                    {
                                        wosHereDes.Destroy();
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (generateReport) { Log.Warning("Found null rwd"); }
                    if (cleanupErrors)
                    {
                        if (generateReport) { Log.Message("Removing rwd..."); }
                        rwdList.Remove(rwd);
                    }
                }
            }

            if (generateReport)
            {
                bool errors = wos_no_rwsCount != 0 || rws_no_wosCount != 0 || factionMismatchCount != 0;
                Log.Message("Rim War Settlement Count: " + rwsCount);
                Log.Message("World Settlement Count: " + wosCount);
                if (!errors) { Log.Message("No errors found."); }
                if (wos_no_rwsCount > 0) { Log.Warning("Settlements without RWS component: " + wos_no_rwsCount); }
                if (rws_no_wosCount > 0) { Log.Warning("Rim War components without Settlement: " + rws_no_wosCount); }
                if (factionMismatchCount > 0) { Log.Warning("Faction mismatches: " + factionMismatchCount); }
            }
        }

        [DebugAction("Rim War - Debug", "Reset Units", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void DebugResetAllMobileUnits()
        {
            int resetCount = 0;
            List<WorldObject> woList = Find.WorldObjects.AllWorldObjects;
            if (woList != null && woList.Count > 0)
            {
                for (int i = 0; i < woList.Count; i++)
                {
                    WarObject rwo = woList[i] as WarObject;
                    if (rwo != null)
                    {
                        RimWarData rwd = WorldUtility.GetRimWarDataForFaction(rwo.Faction);
                        if (rwd != null && rwd.WorldSettlements != null && rwd.WorldSettlements.Count > 0)
                        {
                            resetCount++;
                            RimWorld.Planet.Settlement settlement = rwd.WorldSettlements.RandomElement();
                            if (settlement != null)
                            {
                                if (settlement.Destroyed)
                                {
                                    Log.Warning("Detected destroyed settlement in Rim War data for " + rwd.RimWarFaction.Name);
                                }
                                else
                                {
                                    RimWarSettlementComp rwsc = settlement.GetComponent<RimWarSettlementComp>();
                                    if (rwsc != null)
                                    {
                                        rwsc.RimWarPoints += rwo.RimWarPoints;
                                    }
                                    else
                                    {
                                        Log.Warning("Found no Rim War component for settlement " + settlement.Label);
                                        Log.Warning("Settlement in faction " + settlement.Faction);
                                        Log.Warning("Settlement defname " + settlement.def.defName);
                                    }
                                }
                            }
                            else
                            {
                                Log.Warning("Detected null settlement in Rim War data for " + rwd.RimWarFaction.Name);
                            }
                            if (!rwo.Destroyed)
                            {
                                rwo.Destroy();
                            }
                        }
                        else
                        {
                            Log.Warning("Tried to reset unit but no Faction data exists - cleaning up object.");
                            if (!rwo.Destroyed)
                            {
                                rwo.Destroy();
                            }
                        }
                    }
                }
                Log.Message("Reset " + resetCount + " Rim War units.");
            }
        }

        public static void ValidateAndResetSettlements()
        {
            int sCount = 0;
            int sPoints = GetPointsFromAllSettlements(out sCount);
            if (sCount > 0)
            {
                if (Mathf.RoundToInt(sPoints / sCount) < 110)
                {
                    DebugResetAllSettlements();
                }
            }
        }

        [DebugAction("Rim War - Debug", "Reset Settlements", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void DebugResetAllSettlements()
        {
            Log.Message("Reseting Rim War Settlement Points...");
            int totalSettlements = 0;
            int initialPoints = GetPointsFromAllSettlements(out totalSettlements);
            float yearMultiplier = 1f + (GenDate.YearsPassed * .1f);
            List<RimWarData> rwdList = WorldUtility.Get_WCPT().RimWarData;
            if (rwdList != null && rwdList.Count > 0)
            {
                for (int i = 0; i < rwdList.Count; i++)
                {
                    List<RimWarSettlementComp> rwscList = rwdList[i].WarSettlementComps;
                    if (rwscList != null && rwscList.Count > 0 && rwdList[i].behavior != RimWarBehavior.Player)
                    {
                        for (int j = 0; j < rwscList.Count; j++)
                        {
                            rwscList[j].RimWarPoints = Mathf.RoundToInt(WorldUtility.CalculateSettlementPoints(rwscList[j].parent, rwscList[j].parent.Faction) * Rand.Range(.5f, 1.5f) * yearMultiplier);
                        }
                    }
                }
            }
            int adjustedPoints = GetPointsFromAllSettlements(out totalSettlements);
            Log.Message(totalSettlements + " settlements initially with " + initialPoints + "; " + Mathf.RoundToInt(initialPoints / totalSettlements) + " per settlement");
            Log.Message(totalSettlements + " settlements adjusted to " + adjustedPoints + "; " + Mathf.RoundToInt(adjustedPoints / totalSettlements) + " per settlement");
        }

        private static int GetPointsFromAllSettlements(out int totalSettlements)
        {
            int totalPoints = 0;
            totalSettlements = 0;
            List<RimWarData> rwdList = WorldUtility.Get_WCPT().RimWarData;
            if (rwdList != null && rwdList.Count > 0)
            {
                for (int i = 0; i < rwdList.Count; i++)
                {
                    List<RimWarSettlementComp> rwscList = rwdList[i].WarSettlementComps;
                    if (rwscList != null && rwscList.Count > 0 && rwdList[i].behavior != RimWarBehavior.Player)
                    {
                        for (int j = 0; j < rwscList.Count; j++)
                        {
                            totalPoints += rwscList[j].RimWarPoints;
                            totalSettlements++;
                        }
                    }
                }
            }
            return totalPoints;
        }

        //-- reset rimwar -- //

        [DebugAction("Rim War - Debug", "Reset Factions", actionType = DebugActionType.ToolWorld, allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        private static void DebugResetRimWarFactions()
        {
            ResetFactions(true, true);
        }

        public static void ResetRWD()
        {
        }

        public static void ResetFactions(bool clearExisting = true, bool displayDebugMessages = true)
        {
            List<RimWarData> rwdList = new List<RimWarData>();
            rwdList.Clear();
            foreach (RimWarData rwd in WorldUtility.Get_WCPT().RimWarData)
            {
                rwdList.Add(rwd);
            }
            if (displayDebugMessages) Log.Message("Found " + Find.World.factionManager.AllFactionsVisible.ToList().Count + " visible factions and " + rwdList.Count + " existing factions.");
            if (clearExisting)
            {
                if (displayDebugMessages) Log.Message("Clearing factions...");
                rwdList.Clear();
            }

            List<Faction> factionList = Find.FactionManager.AllFactions.ToList();
            List<Faction> rimwarFactions = new List<Faction>();
            rimwarFactions.Clear();
            if (rwdList != null && rwdList.Count > 0)
            {
                for (int i = 0; i < rwdList.Count; i++)
                {
                    rimwarFactions.Add(rwdList[i].RimWarFaction);
                }
            }

            List<Faction> allFactionsVisible = Find.World.factionManager.AllFactionsVisible.ToList();
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
                        if (displayDebugMessages) Log.Message("Adding new faction " + allFactionsVisible[i].Name);
                        RimWarData rwd = AddRimWarFaction(allFactionsVisible[i], rwdList);
                        if (rwd != null)
                        {
                            rwdList.Add(rwd);
                        }
                    }
                }
            }
            WorldUtility.Get_WCPT().RimWarData = rwdList;
            WorldUtility.GetRimWarFactions(true);
        }

        public static RimWarData AddRimWarFaction(Faction faction, List<RimWarData> rwdList)
        {
            if (CheckForRimWarFaction(faction, rwdList))
            {
                return null;
            }
            //Log.Message("adding rimwar faction " + faction.Name);
            RimWarData newRimWarFaction = new RimWarData(faction);
            if (faction != null)
            {
                GenerateFactionBehavior(newRimWarFaction);
                AssignFactionSettlements(newRimWarFaction);
            }
            Settlement s = newRimWarFaction.GetCapitol;
            return newRimWarFaction;
        }

        public static bool CheckForRimWarFaction(Faction faction, List<RimWarData> rwdList)
        {
            if (rwdList == null)
            {
                return false;
            }
            for (int i = 0; i < rwdList.Count; i++)
            {
                //Log.Message("checking faction " + faction + " against rwd faction: " + this.RimWarData[i].RimWarFaction);
                if (rwdList[i].RandomKey == faction.randomKey)
                {
                    return true;
                }
                else if (rwdList[i].RimWarFaction.HasName && rwdList[i].RimWarFactionKey == (faction.Name + faction.randomKey).ToString())
                {
                    //Log.Message("faction names same, factiond different");
                    rwdList[i].RimWarFaction = faction;
                    return true;
                }
            }
            return false;
        }

        public static void GenerateFactionBehavior(RimWarData rimwarObject)
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

        public static void RandomizeFactionBehavior(RimWarData rimwarObject)
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

        public static void AssignFactionSettlements(RimWarData rimwarObject)
        {
            //Log.Message("assigning settlements to " + rimwarObject.RimWarFaction.Name);
            List<WorldObject> wos = Find.WorldObjects.AllWorldObjects.ToList();
            if (wos != null && wos.Count > 0)
            {
                for (int i = 0; i < wos.Count; i++)
                {
                    //Log.Message("faction for " + worldObjects[i] + " is " + rimwarObject);
                    if (wos[i].Faction != null && rimwarObject != null && rimwarObject.RimWarFaction != null && wos[i].Faction.randomKey == rimwarObject.RimWarFaction.randomKey)
                    {
                        WorldUtility.CreateRimWarSettlement(rimwarObject, wos[i]);
                    }
                }
            }
        }

        public static void UpdateFactionSettlements(RimWarData rwd)
        {
            List<WorldObject> worldObjects = Find.WorldObjects.AllWorldObjects.ToList();

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
    }
}
