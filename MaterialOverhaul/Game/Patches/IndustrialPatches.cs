using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(BuildingLoader),"CreateBuildingComplete")]
    internal static class Patch_IndustrialPrefab
    {
        private static void Postfix(GameObject __result, BuildingDef def)
        {
            if (__result == null || def == null || !BuildingWhitelist.Contains(def.PrefabID)) return;
            __result.AddOrGet<MaterialOverhaulFab>();
            if (AutomationControlScope.IsEnabled(def.PrefabID)) __result.AddOrGet<LogicOperationalController>();
        }
    }

    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(ElementConverter),"GetSpeedMultiplier")]
    internal static class Patch_IndustrialConverterSpeed
    {
        private static void Postfix(ElementConverter __instance,ref float __result)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab != null && fab.Spawned && fab.IsNonBatch) __result *= fab.IndustrialEfficiency;
        }
    }

    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(ElementConverter),"ConvertMass")]
    internal static class Patch_IndustrialConversionGate
    {
        private static bool Prefix(ElementConverter __instance)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab == null || !fab.Spawned || !fab.IsNonBatch) return true;
            fab.PrepareIndustrialStep();
            Operational op=__instance.GetComponent<Operational>();
            return fab.IndustrialTemperatureAllowed && !fab.IsBrokenDown && !fab.IsManuallyDisabled
                && fab.AutomationAllowsProduction && fab.IsPowered
                && (op == null || op.MeetsRequirements(__instance.OperationalRequirement));
        }
    }

    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(EnergyGenerator),"EnergySim200ms")]
    internal static class Patch_IndustrialGeneration
    {
        private static void Prefix(EnergyGenerator __instance,ref float dt,out float __state)
        {
            __state=dt;
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab == null || !fab.Spawned || !fab.IsFuelGenerator) return;
            fab.PrepareIndustrialStep();
            // 原版同一dt用于燃料、全部排放和GenerateJoules，保留原调校倍率，温度只应用一次。
            fab.GeneratorAdvancing=true;
            dt *= fab.IndustrialEfficiency;
        }
        private static void Postfix(EnergyGenerator __instance,float __state)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab == null || !fab.Spawned || !fab.IsFuelGenerator) return;
            fab.GeneratorAdvancing=false;
            fab.NoteGeneratorProduction(__state);
        }
        private static Exception Finalizer(EnergyGenerator __instance,Exception __exception)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab != null) fab.GeneratorAdvancing=false;
            return __exception;
        }
    }

    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(Generator),"get_WattageRating")]
    internal static class Patch_IndustrialWattage
    {
        private static void Postfix(Generator __instance,ref float __result)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            // 实际发电已经通过dt缩放，属性读取在该调用期间必须保留原值。
            if (fab != null && fab.Spawned && fab.IsFuelGenerator && !fab.GeneratorAdvancing)
                __result *= fab.IndustrialEfficiency;
        }
    }

    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(StructureTemperaturePayload),"get_OperatingKilowatts")]
    internal static class Patch_IndustrialOperatingHeat
    {
        internal static void Postfix(StructureTemperaturePayload __instance,ref float __result)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance.primaryElement);
            if (fab != null && fab.Spawned && fab.IsFuelGenerator) __result *= fab.IndustrialEfficiency;
        }
    }
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(StructureTemperaturePayload),"get_ExhaustKilowatts")]
    internal static class Patch_IndustrialExhaustHeat
    {
        private static void Postfix(StructureTemperaturePayload __instance,ref float __result)
        { Patch_IndustrialOperatingHeat.Postfix(__instance,ref __result); }
    }

    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(OilWellCap.States),"IsAbleToPump")]
    internal static class Patch_OilWellStart
    {
        private static void Postfix(OilWellCap.StatesInstance smi,ref bool __result)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(smi.master);
            if (fab == null || !fab.Spawned || !fab.IsOilWell) return;
            fab.PrepareIndustrialStep();
            __result &= fab.IndustrialTemperatureAllowed;
        }
    }
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(OilWellCap.States),"MustStopPumping")]
    internal static class Patch_OilWellStop
    {
        private static void Postfix(OilWellCap.StatesInstance smi,ref bool __result)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(smi.master);
            if (fab == null || !fab.Spawned || !fab.IsOilWell) return;
            fab.PrepareIndustrialStep();
            __result |= !fab.IndustrialTemperatureAllowed;
        }
    }
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(OilWellCap),"AddGasPressure")]
    internal static class Patch_OilWellPressure
    {
        private static bool Prefix(OilWellCap __instance,ref float dt)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab == null || !fab.Spawned || !fab.IsOilWell) return true;
            fab.PrepareIndustrialStep();
            if (!fab.IndustrialProductionActive) return false;
            dt *= fab.IndustrialEfficiency;
            return true;
        }
    }

    // 原排放器异步运行于SIM。肥料的副产物已按实际耗料由转换回调结算，必须关闭原SIM源。
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(BuildingElementEmitter),"UpdateSimState")]
    internal static class Patch_IndustrialEmitter
    {
        private static readonly AccessTools.FieldRef<BuildingElementEmitter,bool> Active=
            AccessTools.FieldRefAccess<BuildingElementEmitter,bool>("simActive");
        private static readonly AccessTools.FieldRef<BuildingElementEmitter,bool> Dirty=
            AccessTools.FieldRefAccess<BuildingElementEmitter,bool>("dirty");
        private static void Prefix(BuildingElementEmitter __instance)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab == null || !fab.Spawned || fab.PrefabId != "FertilizerMaker") return;
            if (Active(__instance)) { Active(__instance)=false; Dirty(__instance)=true; }
        }
    }
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(BuildingElementEmitter),"get_EmitRate")]
    internal static class Patch_IndustrialEmitterDescriptor
    {
        private static void Postfix(BuildingElementEmitter __instance,ref float __result)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab != null && fab.Spawned && fab.PrefabId == "FertilizerMaker") __result *= fab.IndustrialEfficiency;
        }
    }

    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(WorkableReactable),"InternalCanBegin")]
    internal static class Patch_ScrubberBypass
    {
        private static readonly AccessTools.FieldRef<WorkableReactable,Workable> Target=
            AccessTools.FieldRefAccess<WorkableReactable,Workable>("workable");
        private static bool Prefix(WorkableReactable __instance,ref bool __result)
        {
            var work=Target(__instance) as OreScrubber.Work;
            MaterialOverhaulFab fab=work != null ? PatchHelpers.FabOf(work) : null;
            if (fab == null || !fab.Spawned) return true;
            fab.PrepareIndustrialStep();
            if (fab.IndustrialTemperatureAllowed) return true;
            __result=false;
            return false; // 原Reactable不接管搬运者，继续原移动过程。
        }
    }
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(Workable),"WorkTick")]
    internal static class Patch_ScrubberWorkGate
    {
        private static bool Prefix(Workable __instance,WorkerBase worker,ref bool __result)
        {
            if (!(__instance is OreScrubber.Work)) return true;
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab == null || !fab.Spawned) return true;
            fab.PrepareIndustrialStep();
            if (fab.IndustrialTemperatureAllowed && !fab.IsBrokenDown && !fab.IsManuallyDisabled && fab.AutomationAllowsProduction) return true;
            worker.StopWork();
            __result=false;
            return false;
        }
    }
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(OreScrubber.Work),"OnWorkTick")]
    internal static class Patch_ScrubberWear
    {
        private static void Postfix(OreScrubber.Work __instance,float dt)
        {
            MaterialOverhaulFab fab=PatchHelpers.FabOf(__instance);
            if (fab != null && fab.Spawned) fab.OnDuplicantWorkTick(dt);
        }
    }
}
