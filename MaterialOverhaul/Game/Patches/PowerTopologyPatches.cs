using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    [Core.SystemPart(Core.ModSystem.Power)]
    [HarmonyPatch(typeof(global::Game), "OnPrefabInit")]
    internal static class Patch_PowerTopology_Initialize
    {
        private static void Postfix(global::Game __instance) { PowerTopologyMonitor.Attach(__instance); }
    }

    [Core.SystemPart(Core.ModSystem.Power)]
    [HarmonyPatch(typeof(global::Game), "OnCleanUp")]
    internal static class Patch_PowerTopology_CleanUp
    {
        private static void Prefix() { PowerSettlement.Reset(); PowerTopologyMonitor.Reset(); }
    }

    [Core.SystemPart(Core.ModSystem.Power)]
    [HarmonyPatch(typeof(CircuitManager), "Rebuild")]
    internal static class Patch_PowerTopology_Rebuild
    {
        private static void Postfix(HashSet<Generator> ___generators, HashSet<IEnergyConsumer> ___consumers,
            HashSet<WireUtilityNetworkLink> ___bridges)
        {
            PowerTopologyMonitor.AfterCircuitRebuild(___generators, ___consumers, ___bridges);
            PowerSettlement.Rebuild(___generators, ___consumers);
        }
    }
}
