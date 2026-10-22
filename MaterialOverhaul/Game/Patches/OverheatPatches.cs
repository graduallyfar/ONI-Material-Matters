using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    // PrimaryElement.OnSpawn 会把材料倍率加入建筑属性；无论组件生成顺序如何，随后再校准一次。
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(PrimaryElement), "OnSpawn")]
    internal static class Patch_PrimaryElement_OnSpawn
    {
        private static void Postfix(PrimaryElement __instance)
        {
            try { __instance.GetComponent<MaterialOverhaulFab>()?.ApplyOverheatRule(); }
            catch (Exception e) { ModLog.Error("材料属性就绪后校准过热阈值失败：" + e); }
        }
    }

    /// <summary>
    /// 过热阈值接入：原版 <c>Overheatable.OnSpawn</c> 完成属性初始化与结构温度注册之后，
    /// 由本 Mod 组件把阈值改成「最高工作温度 = 过热阈值 = 主建造材料熔点 × 75% × 主材料过热倍率，按 K 计算」
    /// （建筑细节文档 §2.1.5、V0.1.2 开发计划 §2.1）。
    ///
    /// 组件自身的 OnSpawn 也会调用一次：两处调用都是幂等的，谁先谁后都能得到同一结果
    /// （先调用时属性修饰项尚未建立，规则值直接落在基础值上；后调用时再补偿一次差值）。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(Overheatable), "OnSpawn")]
    internal static class Patch_Overheatable_OnSpawn
    {
        private static void Postfix(Overheatable __instance)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.ApplyOverheatRule();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("过热阈值接入异常：" + e);
            }
        }
    }
}
