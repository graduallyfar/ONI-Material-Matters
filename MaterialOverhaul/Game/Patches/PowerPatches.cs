using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 电网接入（V0.0.3 开发计划 §4.2）。
    ///
    /// 已核实原版口径：<c>EnergyConsumer.WattsUsed</c> 决定电路实际扣走的焦耳，
    /// <c>WattsNeededWhenActive</c> 决定需求排序与潜在功率显示；两者都是**非虚属性**，
    /// 无法用子类覆写，只能做后置补丁。因此这里按消费者反查本 Mod 组件
    /// （见 <see cref="PowerRegistry"/>），只改写明确纳入加热范围的建筑。
    ///
    /// 关键口径：
    ///   - <c>WattsUsed</c> 返回**需求**而不是「上次是否真的供上了电」：
    ///     原版也是这么做的（它返回额定值，由电路决定供不供得上），
    ///     否则需求会随供电状态来回翻转，形成自锁或免费注热；
    ///   - 未获供电时需求仍然保留，但本 Mod 不注入任何热量（<c>AppliedHeating</c> 为 None）；
    ///   - 不新增第二个消费者，也不新增可见接线口，只复用建筑原有电力连接。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(EnergyConsumer), "get_WattsUsed")]
    internal static class Patch_EnergyConsumer_WattsUsed
    {
        private static void Postfix(EnergyConsumer __instance, ref float __result)
        {
            try
            {
                MaterialOverhaulFab fab;
                if (!PowerRegistry.TryGet(__instance, out fab) || !fab.HasPowerOverride)
                {
                    return;
                }

                __result = fab.CurrentRequestedWatts;
            }
            catch (Exception e)
            {
                ModLog.Error("电网实际用电补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 潜在功率：需求排序与「潜在耗电」显示用。取原版额定值与本 Mod 需求中的较大者，
    /// 保证加热需求不会因为建筑基础额定更低而被低估。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(EnergyConsumer), "get_WattsNeededWhenActive")]
    internal static class Patch_EnergyConsumer_WattsNeededWhenActive
    {
        private static void Postfix(EnergyConsumer __instance, ref float __result)
        {
            try
            {
                MaterialOverhaulFab fab;
                if (!PowerRegistry.TryGet(__instance, out fab) || !fab.HasPowerOverride)
                {
                    return;
                }

                if (fab.CurrentRequestedWatts > __result)
                {
                    __result = fab.CurrentRequestedWatts;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("潜在功率补丁异常：" + e);
            }
        }
    }
}
