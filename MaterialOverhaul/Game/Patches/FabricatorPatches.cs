using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 补丁接入点均取自目标版本程序集核实结果（Assembly-CSharp.decompiled.cs 行号只作核对记录）：
    ///   ComplexFabricator.OnPrefabInit              protected override       180727
    ///   ComplexFabricator.ComputeWorkProgress       protected virtual        180832
    ///   ComplexFabricator.Sim200ms                  public，无子类覆写        180837
    ///   ComplexFabricator.IncrementRecipeQueueCount public                   181402
    ///   ComplexFabricator.SetRecipeQueueCount       public                   181391
    ///   ComplexFabricatorWorkable.OnWorkTick        protected override       126157
    ///   Workable.GetEfficiencyMultiplier            public virtual           157412
    ///
    /// 补丁只作用于白名单建筑：组件只在白名单建筑的 prefab 上挂载，其余建筑取不到组件即原样放行。
    /// </summary>
    internal static class PatchHelpers
    {
        public static string PrefabIdOf(GameObject go)
        {
            if (go == null)
            {
                return string.Empty;
            }

            KPrefabID prefabId = go.GetComponent<KPrefabID>();
            return prefabId != null ? prefabId.PrefabTag.ToString() : go.name;
        }

        public static MaterialOverhaulFab FabOf(Component component)
        {
            return component != null ? component.GetComponent<MaterialOverhaulFab>() : null;
        }
    }

    /// <summary>在建筑 prefab 阶段挂载本 Mod 组件，使组件随存档序列化（而非运行时临时添加）。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "OnPrefabInit")]
    internal static class Patch_ComplexFabricator_OnPrefabInit
    {
        private static void Postfix(ComplexFabricator __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                string prefabId = PatchHelpers.PrefabIdOf(__instance.gameObject);
                if (!BuildingWhitelist.Contains(prefabId))
                {
                    return;
                }

                if (__instance.GetComponent<MaterialOverhaulFab>() == null)
                {
                    __instance.gameObject.AddComponent<MaterialOverhaulFab>();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("挂载 MaterialOverhaulFab 失败：" + e);
            }
        }
    }

    /// <summary>
    /// 非复制人路径：把温度效率乘进生产进度，越界时停止推进（返回值 0）。
    /// 只在允许生产时累计实际生产时间对应的机械磨损。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "ComputeWorkProgress")]
    internal static class Patch_ComplexFabricator_ComputeWorkProgress
    {
        private static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe, float dt, ref float __result)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                ProductionGate gate = fab.EvaluateFor(recipe);
                if (!gate.AllowsProduction)
                {
                    __result = 0f;
                    return;
                }

                __result *= gate.Efficiency;
                fab.AccumulateProduction(dt);
            }
            catch (Exception e)
            {
                ModLog.Error("温度效率补丁异常：" + e);
            }
        }
    }

    /// <summary>复制人路径：操作效率乘数，与 ComputingWorkProgress 共用同一份温区评估。</summary>
    [HarmonyPatch(typeof(Workable), "GetEfficiencyMultiplier")]
    internal static class Patch_Workable_GetEfficiencyMultiplier
    {
        private static void Postfix(Workable __instance, ref float __result)
        {
            try
            {
                ComplexFabricatorWorkable workable = __instance as ComplexFabricatorWorkable;
                if (workable == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = PatchHelpers.FabOf(workable);
                if (fab == null)
                {
                    return;
                }

                ComplexRecipe recipe = fab.CurrentWorkingRecipe;
                if (recipe == null)
                {
                    return;
                }

                ProductionGate gate = fab.EvaluateFor(recipe);
                __result *= gate.AllowsProduction ? gate.Efficiency : 0f;
            }
            catch (Exception e)
            {
                ModLog.Error("复制人操作效率补丁异常：" + e);
            }
        }
    }

    /// <summary>复制人路径的生产时间：只有实际推进工作才累计磨损。</summary>
    [HarmonyPatch(typeof(ComplexFabricatorWorkable), "OnWorkTick")]
    internal static class Patch_ComplexFabricatorWorkable_OnWorkTick
    {
        private static void Postfix(ComplexFabricatorWorkable __instance, float dt)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.AccumulateProduction(dt);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("生产时间累计补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 状态刷新。挂在 Sim200ms 而不是 Sim1000ms：Sim200ms 非 virtual 且无子类覆写，
    /// 不会因为子类 override 而漏掉（DataMiner 覆写了 Sim1000ms）。
    /// 只在状态变化时记录日志，不在每次模拟更新刷屏。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "Sim200ms")]
    internal static class Patch_ComplexFabricator_Sim200ms
    {
        private static void Postfix(ComplexFabricator __instance)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.RefreshStatus();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("状态刷新补丁异常：" + e);
            }
        }
    }

    /// <summary>订单添加顺序：原版队列是"每配方计数器"，这里额外记录玩家添加顺序。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "IncrementRecipeQueueCount")]
    internal static class Patch_ComplexFabricator_IncrementRecipeQueueCount
    {
        private static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null && recipe != null)
                {
                    fab.NoteRecipeQueued(recipe.id);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("订单顺序记录补丁异常：" + e);
            }
        }
    }

    /// <summary>侧屏直接设置数量时同样记录顺序；数量归零则移出顺序表。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "SetRecipeQueueCount")]
    internal static class Patch_ComplexFabricator_SetRecipeQueueCount
    {
        private static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe, int count)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null || recipe == null)
                {
                    return;
                }

                if (count > 0)
                {
                    fab.NoteRecipeQueued(recipe.id);
                }
                else
                {
                    fab.NoteRecipeCleared(recipe.id);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("订单顺序记录补丁异常：" + e);
            }
        }
    }
}
