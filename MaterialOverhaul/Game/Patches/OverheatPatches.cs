using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 过热阈值接入：原版 <c>Overheatable.OnSpawn</c> 完成属性初始化与结构温度注册之后，
    /// 由本 Mod 组件把阈值改成「最高工作温度 = 过热阈值 = 主建造材料熔点 × 75%，按 K 计算」
    /// （建筑细节文档 §2.1.5、V0.1.2 开发计划 §2.1）。
    ///
    /// 组件自身的 OnSpawn 也会调用一次：两处调用都是幂等的，谁先谁后都能得到同一结果
    /// （先调用时属性修饰项尚未建立，规则值直接落在基础值上；后调用时再补偿一次差值）。
    /// </summary>
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
