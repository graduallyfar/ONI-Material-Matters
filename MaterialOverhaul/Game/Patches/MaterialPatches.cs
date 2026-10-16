using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    // 元素加载后、矿石预制件生成前加入标签，让材料选择、取料和旧档实例共用原版分类。
    [HarmonyPatch(typeof(ElementLoader), "Load")]
    internal static class Patch_ElementLoader_Load
    {
        private static void Postfix()
        {
            try { MaterialRegistration.AddAbyssaliteBuildingTag(); }
            catch (Exception e) { ModLog.Error("深渊晶石建材登记失败：" + e); }
        }
    }

    // 原版在这里建立元素过热加成；Db.Initialize 时修饰项尚不存在。
    [HarmonyPatch(typeof(LegacyModMain), "ConfigElements")]
    internal static class Patch_LegacyModMain_ConfigElements
    {
        private static void Postfix()
        {
            try { MaterialRegistration.ConvertOverheatBonuses(); }
            catch (Exception e) { ModLog.Error("材料过热百分比登记失败：" + e); }
        }
    }
}
