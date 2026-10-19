using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    [HarmonyPatch(typeof(global::Game), "OnPrefabInit")]
    internal static class Patch_PowerTopology_Initialize
    {
        private static void Postfix(global::Game __instance) { PowerCircuitCount.Reset(); PowerTopologyMonitor.Attach(__instance); }
    }

    [HarmonyPatch(typeof(global::Game), "OnCleanUp")]
    internal static class Patch_PowerTopology_CleanUp
    {
        private static void Prefix() { PowerCircuitCount.Reset(); PowerTopologyMonitor.Reset(); }
    }

    [HarmonyPatch(typeof(CircuitManager), "Rebuild")]
    internal static class Patch_PowerTopology_Rebuild
    {
        private static void Postfix(CircuitManager __instance, HashSet<Generator> ___generators, HashSet<IEnergyConsumer> ___consumers,
            HashSet<WireUtilityNetworkLink> ___bridges)
        {
            PowerCircuitCount.Refresh(__instance, ___generators, ___consumers);
            PowerTopologyMonitor.AfterCircuitRebuild(___generators, ___consumers, ___bridges);
        }
    }
}
