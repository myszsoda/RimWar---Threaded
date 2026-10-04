using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimWar
{
    [StaticConstructorOnStartup]
    public static class RimWarMatPool
    {
        public static readonly Texture2D Icon_Trader = ContentFinder<Texture2D>.Get("World/TraderExpanded", true);
        public static readonly Texture2D Icon_Settler = ContentFinder<Texture2D>.Get("World/SettlerExpanded", true);
        public static readonly Texture2D Icon_Scout = ContentFinder<Texture2D>.Get("World/ScoutExpanded", true);
        public static readonly Texture2D Icon_Warband = ContentFinder<Texture2D>.Get("World/WarbandExpanded", true);
        public static readonly Texture2D Icon_LaunchWarband = ContentFinder<Texture2D>.Get("World/LaunchWarbandExpanded", true);
        public static readonly Texture2D Icon_ReinforceSite = ContentFinder<Texture2D>.Get("World/ReinforceSite", true);
        public static readonly Texture2D Material_Exclamation_Red = ContentFinder<Texture2D>.Get("World/Exclamation_Red", true);

        public static readonly Texture2D Marker_ShowAggression = ContentFinder<Texture2D>.Get("World/WarbandExpanded");

        public static readonly Material Material_Exclamation_Green = MaterialPool.MatFrom("World/Exclamation_Green");
        public static readonly Material Material_CapitolStar_se = MaterialPool.MatFrom("World/capitol_star_se");
        public static readonly Material Material_FieldBattle = MaterialPool.MatFrom("World/FieldBattle");
        public static readonly Material Material_BattleSite = MaterialPool.MatFrom("World/BattleSite");
        public static readonly Material Material_BattleSiteExpanded = MaterialPool.MatFrom("World/BattleSiteExpanded");
    }
}
