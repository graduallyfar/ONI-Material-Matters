using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Hardness;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 维修时的温度重置。原版 <c>Overheatable</c> 订阅 <c>BuildingPartiallyRepaired</c> 后
    /// **无条件**把本体温度设为 293.15 K；设计要求「只有机械损坏时维修不重置温度」，
    /// 因此这里按当前尚未修复的原因决定是否放行：含过热（或来源未知的旧损伤）才重置。
    ///
    /// 时序（已核实）：<c>BuildingHP.Repair</c> 先改生命值、再触发
    /// <c>BuildingPartiallyRepaired</c>；本 Mod 的原因只在完全修复后的下一次模拟步清除，
    /// 因此本次维修的原因一定还在，不会被提前清掉。原版过热维修时机保持不变，
    /// 不在刚超过阈值时主动降温。
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

    /// <summary>
    /// 过热伤害来源记录（V0.0.3 开发计划 §6.2）。
    ///
    /// 原版 <c>Overheatable</c> 每 7.5 s 通过 <c>DoBuildingDamage</c> 施加 1 点过热伤害。
    /// 这里在扣血事件之后识别来源：不是本 Mod 刚发出的机械磨损、且本体温度确实高于过热阈值时，
    /// 才锁存过热损坏原因。这样「刚超过阈值但还没扣血」不会被写成「已过热受损」，
    /// 而损坏停机后原版不再发伤害的情形由 <c>MaterialOverhaulFab.TickDamage</c> 的阈值兜底处理。
    /// </summary>
    [HarmonyPatch(typeof(BuildingHP), "OnDoBuildingDamage")]
    internal static class Patch_BuildingHP_OnDoBuildingDamage
    {
        private static void Prefix(BuildingHP __instance, out int __state)
        {
            __state = __instance.HitPoints;
        }

        private static void Postfix(BuildingHP __instance, int __state)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                if (fab.ConsumeMechanicalDamageMark())
                {
                    // 本 Mod 的机械磨损：原因已在扣除余额时记录。
                    return;
                }

                // invincible 或未实际扣血的事件不能触发过热加热保护。
                if (__instance.HitPoints < __state && fab.IsAboveOverheatThreshold)
                {
                    fab.LatchOverheatDamage();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("过热伤害来源补丁异常：" + e);
            }
        }
    }
}
