using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using RimWar.History;
using UnityEngine;

namespace RimWar
{
    [DefOf]
    public static class RimWarDefOf
    {
        public static WorldObjectDef RW_WarObject;
        public static WorldObjectDef RW_LaunchedWarObject;

        public static WorldObjectDef RW_Warband;
        public static WorldObjectDef RW_LaunchedWarband;
        public static WorldObjectDef RW_Scout;
        public static WorldObjectDef RW_Settler;
        public static WorldObjectDef RW_Diplomat;
        public static WorldObjectDef RW_Trader;
        public static WorldObjectDef RW_CapitolBuilding;
        public static WorldObjectDef RW_BattleSite;
        public static WorldObjectDef PeaceTalks;

        public static RW_LetterDef RimWar_HostileEvent;
        public static RW_LetterDef RimWar_NeutralEvent;
        public static RW_LetterDef RimWar_TradeEvent;
        public static RW_LetterDef RimWar_SettlementEvent;
        public static RW_LetterDef RimWar_FriendlyEvent;
        public static RW_LetterDef RimWar_WarningEvent;

        public static DamageDef RW_CombatInjury;
        public static HediffDef RW_CombatInjuryHD;

        public static HistoryEventDef RW_RandomizeRelations;
        public static HistoryEventDef RW_UnitRequest;
        public static HistoryEventDef RW_DiplomacyAction;
        public static HistoryEventDef RW_ReinforcedSettlement;

        public static PawnsArrivalModeDef RandomDrop;
    }
}
