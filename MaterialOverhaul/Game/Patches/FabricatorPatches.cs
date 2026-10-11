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
    ///   ComplexFabricator.RefreshAndStartNextOrder  private                  180855
    ///   ComplexFabricator.SetQueueDirty             public                   180894
    ///   ComplexFabricator.StartWorkingOrder         private，唯一开始点       180909
    ///   ComplexFabricator.CompleteWorkingOrder      public virtual           180935
    ///   ComplexFabricator.AdvanceNextOrder          private，唯一推进点       180989
    ///   ComplexFabricator.IncrementRecipeQueueCount public                   181402
    ///   ComplexFabricator.SetRecipeQueueCount       public                   181391
    ///   ComplexFabricator.DecrementRecipeQueueCount public                   181419
    ///   ComplexFabricatorWorkable.OnWorkTick        protected override       126157
    ///   Workable.GetEfficiencyMultiplier            public virtual           157412
    ///   Overheatable.ResetTemperature               public                   142560
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
                    fab.TickDamage();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("状态刷新补丁异常：" + e);
            }
        }
    }

    // ---- 订单流程（V0.0.2，建筑细节文档 §2.3）----
    //
    // 原版队列是「每配方一个计数」的字典，没有顺序概念，轮换按 recipe_list 下标推进。
    // 本 Mod 用有序订单表接管「下一个执行哪一笔订单」，同时把订单表的数量折算回原版计数字典，
    // 使原版界面、预取与可用性判断继续按原版口径工作。
    //
    // 三个原版界面入口（在 ComplexFabricatorSideScreen 里核实）：
    //   Increment  「+」        181402
    //   Decrement  「−」        181419（respectInfinite:false 用于面板按钮）
    //   SetCount   「∞」与数量输入框  181391

    /// <summary>「+」：新增一笔 1 批的有限订单；原版计数到达上限后会变成无限，这里同步为无限订单。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "IncrementRecipeQueueCount")]
    internal static class Patch_ComplexFabricator_IncrementRecipeQueueCount
    {
        private static void Prefix(ComplexFabricator __instance, ComplexRecipe recipe, out int __state)
        {
            __state = 0;
            try
            {
                if (recipe == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                // 改原版计数之前先对齐订单表，避免把「尚未纳入订单表的原版数量」当成不存在。
                fab.EnsureOrdersImported();
                __state = __instance.GetRecipeQueueCount(recipe);
            }
            catch (Exception e)
            {
                ModLog.Error("订单顺序记录补丁异常：" + e);
            }
        }

        private static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe, int __state)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.OnVanillaIncrement(recipe, __state);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("订单顺序记录补丁异常：" + e);
            }
        }
    }

    /// <summary>「−」：按订单逐笔回退，数量减到 0 时整笔移除。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "DecrementRecipeQueueCount")]
    internal static class Patch_ComplexFabricator_DecrementRecipeQueueCount
    {
        private static void Prefix(ComplexFabricator __instance, ComplexRecipe recipe, out int __state)
        {
            __state = 0;
            try
            {
                if (recipe == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                fab.EnsureOrdersImported();
                __state = __instance.GetRecipeQueueCount(recipe);
            }
            catch (Exception e)
            {
                ModLog.Error("订单数量回退补丁异常：" + e);
            }
        }

        private static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe, int __state)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.OnVanillaDecrement(recipe, __state);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("订单数量回退补丁异常：" + e);
            }
        }
    }

    /// <summary>侧屏直接设置数量：0 取消该配方订单，-1 记为无限订单，正数改写为一笔对应批次的订单。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "SetRecipeQueueCount")]
    internal static class Patch_ComplexFabricator_SetRecipeQueueCount
    {
        private static void Prefix(ComplexFabricator __instance, ComplexRecipe recipe)
        {
            try
            {
                if (recipe == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.EnsureOrdersImported();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("订单数量设置补丁异常：" + e);
            }
        }

        private static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe, int count)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.OnVanillaSetCount(recipe, count);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("订单数量设置补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 接管「下一个执行哪一笔订单」：跳过原版按 <c>recipe_list</c> 下标的扫描，
    /// 改由本 Mod 订单表按玩家添加顺序决定（V0.0.2 开发计划 §3.2）。
    ///
    /// 判断仍然沿用原版口径：剩余队列数与原料齐备都由原版方法给出，不复制一份逻辑；
    /// 温度不足不参与选择——温度只决定已锁定批次是加热还是生产（建筑细节文档 §2.3.3）。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "AdvanceNextOrder")]
    internal static class Patch_ComplexFabricator_AdvanceNextOrder
    {
        private static bool Prefix(ComplexFabricator __instance)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null || !fab.HasOrderData)
                {
                    // 没有本 Mod 订单数据（旧存档或其它码路直接改原版计数）：保持原版轮换。
                    return true;
                }

                fab.RefreshNextOrder();
                return false;
            }
            catch (Exception e)
            {
                ModLog.Error("订单轮换补丁异常：" + e);
                return true;
            }
        }
    }

    /// <summary>
    /// 刷新并在需要时开始下一笔订单。本 Mod 在这里先把「下一笔」重新算一遍，
    /// 因此批次完成（<c>CompleteWorkingOrder</c> 末尾会调用本方法）之后立刻按新状态选择订单，
    /// 不会先把刚结束的那笔订单又启动一次。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "RefreshAndStartNextOrder")]
    internal static class Patch_ComplexFabricator_RefreshAndStartNextOrder
    {
        private static void Prefix(ComplexFabricator __instance)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.RefreshNextOrder();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("订单轮换补丁异常：" + e);
            }
        }
    }

    /// <summary>开始生产时锁定对应的那一笔订单：重复配方必须靠轮换位置确定是哪一笔。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "StartWorkingOrder")]
    internal static class Patch_ComplexFabricator_StartWorkingOrder
    {
        private static void Prefix(ComplexFabricator __instance, int index)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                ComplexRecipe[] recipes = __instance.GetRecipes();
                if (recipes == null || index < 0 || index >= recipes.Length)
                {
                    return;
                }

                fab.LockOrderForStart(recipes[index].id);
            }
            catch (Exception e)
            {
                ModLog.Error("批次锁定补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 批次完成的订单记账。
    ///
    /// 记账放在**前缀**：原版正文在结算后会立刻调用 <c>RefreshAndStartNextOrder</c>，
    /// 如果等到后置才扣减，无限订单的「本轮额度」就会晚一步生效（额度 1 时表现为连做两批）。
    /// 原版计数字典的同步放在**后置**：原版正文内部会自己扣一次计数（走私有的
    /// <c>DecrementRecipeQueueCountInternal</c>，不经过公开 API），后置最后以订单表为准写回。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "CompleteWorkingOrder")]
    internal static class Patch_ComplexFabricator_CompleteWorkingOrder
    {
        private static void Prefix(ComplexFabricator __instance, out ComplexRecipe __state)
        {
            __state = __instance.CurrentWorkingOrder;

            try
            {
                if (__state == null)
                {
                    // 原版自身会记录一次警告并直接返回，这里不记账。
                    return;
                }

                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.OnBatchCompleting(__state.id);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("批次完成记账补丁异常：" + e);
            }
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
                if (fab != null)
                {
                    fab.OnBatchCompleted(__state.id);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("批次完成同步补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 维修时的温度重置。原版 <c>Overheatable</c> 订阅 <c>BuildingPartiallyRepaired</c> 后
    /// **无条件**把本体温度设为 293.15 K；设计要求「只有机械损坏时维修不重置温度」，
    /// 因此这里按当前损坏原因决定是否放行：含过热才重置。
    ///
    /// 白名单外的建筑取不到组件，直接放行原版行为。
    /// </summary>
    [HarmonyPatch(typeof(Overheatable), "ResetTemperature")]
    internal static class Patch_Overheatable_ResetTemperature
    {
        private static bool Prefix(Overheatable __instance)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return true;
                }

                bool reset = fab.RepairResetsTemperature;
                if (!reset)
                {
                    ModLog.Info(fab.PrefabId + "：只有机械损坏，维修不重置温度。");
                }

                return reset;
            }
            catch (Exception e)
            {
                ModLog.Error("维修温度重置补丁异常：" + e);
                return true;
            }
        }
    }
}
