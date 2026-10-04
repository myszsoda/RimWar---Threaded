using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWar.Planet;
using RimWorld;
using Verse;

namespace RimWar.ModCheck
{
    public class Empire
    {

        public static bool EmpireFaction_ColonyCheck(int tile)
        {
            if (!Validate.EmpireIsActive)
            {
                return false;
            }
            try
            {
                foreach (RimWorld.Planet.WorldObject wo in Find.WorldObjects.AllWorldObjects)
                {
                    if (wo.def.defName == "Colony" && wo.Tile == tile)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        //public static bool FactionFC_ComponentCheck(int tile)
        //{
        //    FactionFC component = Find.World.GetComponent<FactionFC>();
        //    if (component != null)
        //    {
        //        SettlementFC sfc = component.returnSettlementByLocation(tile, Find.World.info.name);
        //        if (sfc != null)
        //        {
        //            return true;                        
        //        }
        //    }
        //    return false;            
        //}

        //public static int FactionFC_SettlementLevel(int tile)
        //{
        //    FactionFC component = Find.World.GetComponent<FactionFC>();
        //    if (component != null)
        //    {
        //        SettlementFC sfc = component.returnSettlementByLocation(tile, Find.World.info.name);
        //        if (sfc != null)
        //        {
        //            return sfc.settlementLevel * 500;
        //        }
        //    }
        //    return 0;
        //}
    }
}
