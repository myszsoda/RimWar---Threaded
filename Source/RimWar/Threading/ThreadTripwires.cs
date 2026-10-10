#if RIMWAR_TRIPWIRES
using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimWar.Threading
{
    /// <summary>
    /// Verification-only Harmony prefixes that make RimWorld's own non-thread-safe entry points throw when reached from
    /// the RimWar worker, catching transitive calls that the RimWar-level MainThread.Verify() gates cannot see.
    ///
    /// Compiled only with the RIMWAR_TRIPWIRES symbol (not plain DEBUG: the Debug configuration is the one that builds
    /// into v1.6/Assemblies, and a prefix on Verse.Rand would cost every tick). Build for a verification run with:
    ///   dotnet msbuild Source\RimWar\RimWar.csproj /p:Configuration=Debug /p:DefineConstants=DEBUG%3BTRACE%3BRIMWAR_TRIPWIRES
    /// Small getters such as Rand.Value may be inlined by the JIT, so this is best-effort.
    /// </summary>
    public static class ThreadTripwires
    {
        public static void Install(HarmonyLib.Harmony harmony)
        {
            HarmonyMethod prefix = new HarmonyMethod(typeof(ThreadTripwires), nameof(WorkerTripwire));
            Patch(harmony, prefix, AccessTools.PropertyGetter(typeof(Rand), nameof(Rand.Value)));
            Patch(harmony, prefix, AccessTools.PropertyGetter(typeof(Rand), nameof(Rand.Int)));
            Patch(harmony, prefix, AccessTools.Method(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add)));
            Patch(harmony, prefix, AccessTools.Method(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Remove)));
            Patch(harmony, prefix, AccessTools.Method(typeof(WorldObject), nameof(WorldObject.Destroy)));
            Patch(harmony, prefix, AccessTools.Method(typeof(Faction), nameof(Faction.TryAffectGoodwillWith)));
            Patch(harmony, prefix, AccessTools.Method(typeof(WorldGrid), nameof(WorldGrid.TraversalDistanceBetween)));
            foreach (MethodInfo method in typeof(LetterStack).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (method.Name == nameof(LetterStack.ReceiveLetter))
                {
                    Patch(harmony, prefix, method);
                }
            }
            Log.Message("[RimWar] Thread tripwires installed.");
        }

        private static void Patch(HarmonyLib.Harmony harmony, HarmonyMethod prefix, MethodInfo target)
        {
            if (target == null)
            {
                Log.Warning("[RimWar] Thread tripwire target not found.");
                return;
            }
            try
            {
                harmony.Patch(target, prefix: prefix);
            }
            catch (Exception e)
            {
                Log.Warning("[RimWar] Could not install thread tripwire on " + target + ": " + e.Message);
            }
        }

        private static void WorkerTripwire(MethodBase __originalMethod)
        {
            MainThread.Verify(__originalMethod.DeclaringType?.Name + "." + __originalMethod.Name);
        }
    }
}
#endif
