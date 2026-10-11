using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    // ---- 订单流程（V0.0.2 建立、V0.0.3 补齐身份与逐笔编辑）----
    //
    // 原版队列是「每配方一个计数」的字典，没有顺序概念，轮换按 recipe_list 下标推进。
    // 本 Mod 用有序订单表接管「下一个执行哪一笔订单」，同时把订单表的数量折算回原版计数字典，
    // 使原版界面、预取与可用性判断继续按原版口径工作。
    //
    // 三个原版界面入口（在 ComplexFabricatorSideScreen 里核实）：
    //   Increment  「+」        181402
    //   Decrement  「−」        181419（respectInfinite:false 用于面板按钮）
    //   SetCount   「∞」与数量输入框  181391
    //
    // 逐笔编辑走本 Mod 的侧屏（Game/OrderListSideScreen.cs），不再依赖这三个有歧义的合计入口。

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

    /// <summary>
    /// 侧屏直接设置数量：原版界面沿用合计口径（0 取消该配方全部订单、-1 记为无限、正数改写为一批）。
    /// 逐笔编辑走本 Mod 的订单列表侧屏，两处不互相覆盖：都以订单表为准写回原版计数。
    /// </summary>
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
    /// 改由本 Mod 订单表按玩家添加顺序决定。
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

    /// <summary>
    /// 开始生产时锁定对应的那一笔订单：重复配方必须靠订单身份（而不是配方标识）确定是哪一笔。
    /// 这是**批次准备**发生的时刻，锁定必须在这里完成，并且不受温度与自动化影响。
    /// </summary>
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

                fab.LockOrderForStart(recipes[index].id, index);
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
}
