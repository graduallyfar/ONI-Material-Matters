using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Zones;
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
    ///   ComplexFabricator.AdvanceNextOrder          private，唯一推进点       180989
    ///   ComplexFabricator.CompleteWorkingOrder      public virtual           180935
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

                ProductionDecision gate = fab.EvaluateFor(recipe);
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

                ProductionDecision gate = fab.EvaluateFor(recipe);
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
        private static void Postfix(ComplexFabricator __instance, float dt)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.RefreshStatus();
                    fab.TickHeating(dt);
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

    /// <summary>
    /// 接管“下一个执行哪个订单”：把原版指针预置到本 Mod 顺序表的候选之前，
    /// 让原版 <c>AdvanceNextOrder</c> 的扫描循环从玩家添加顺序的候选开始。
    ///
    /// 这样只改变**顺序**：有剩余队列、是否有料、缺料向后跳过，仍全部由原版判断
    /// （`GetRemainingQueueCount` 与 `HasIngredients`），不重复实现也不会越权放行。
    /// 温度不足不参与选择——温度只决定已锁定批次是加热还是生产（建筑细节文档 §2.3.3）。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "AdvanceNextOrder")]
    internal static class Patch_ComplexFabricator_AdvanceNextOrder
    {
        private static readonly AccessTools.FieldRef<ComplexFabricator, int> NextOrderIdxRef =
            AccessTools.FieldRefAccess<ComplexFabricator, int>("nextOrderIdx");

        private static void Prefix(ComplexFabricator __instance)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                string candidate = fab.PeekNextOrderRecipeId();
                if (string.IsNullOrEmpty(candidate))
                {
                    return;
                }

                ComplexRecipe[] recipes = __instance.GetRecipes();
                if (recipes == null || recipes.Length == 0)
                {
                    return;
                }

                int index = -1;
                for (int i = 0; i < recipes.Length; i++)
                {
                    if (recipes[i].id == candidate)
                    {
                        index = i;
                        break;
                    }
                }

                if (index < 0)
                {
                    // 候选不在该建筑的配方列表里（配方被移除或 DLC 未启用）时保持原版行为。
                    return;
                }

                // 原版循环第一步就是 nextOrderIdx = (nextOrderIdx + 1) % Length，因此预置到候选的前一位。
                NextOrderIdxRef(__instance) = (index - 1 + recipes.Length) % recipes.Length;
            }
            catch (Exception e)
            {
                ModLog.Error("订单轮换补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 批次完成后同步本 Mod 顺序表：原版把队列计数扣到 0 即该订单已耗尽，从顺序表移除。
    /// 无限订单（计数 -1）不在顺序表中被移除，符合“无限订单持续保留”。
    ///
    /// 原版 <c>CompleteWorkingOrder</c> 内部走的是私有的 <c>DecrementRecipeQueueCountInternal</c>，
    /// 不经过 <c>SetRecipeQueueCount</c>，所以这里必须单独同步。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "CompleteWorkingOrder")]
    internal static class Patch_ComplexFabricator_CompleteWorkingOrder
    {
        private static void Prefix(ComplexFabricator __instance, out ComplexRecipe __state)
        {
            __state = __instance.CurrentWorkingOrder;
        }

        private static void Postfix(ComplexFabricator __instance, ComplexRecipe __state)
        {
            try
            {
                if (__state == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                if (__instance.GetRecipeQueueCount(__state) <= 0)
                {
                    fab.NoteRecipeCleared(__state.id);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("批次完成同步补丁异常：" + e);
            }
        }
    }
}
