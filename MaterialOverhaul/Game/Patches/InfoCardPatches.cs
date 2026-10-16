using System;
using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 信息卡与配方详情的温度行（细节文档 §2.3.5、§2.4.5 的第一步）。
    ///
    /// **按原版分工排布**（已核实原版机制）：建筑描述只讲用途（<c>BuildingDef.Desc</c> 读
    /// <c>STRINGS.BUILDINGS.PREFABS.&lt;ID&gt;.DESC</c>，原版金属精炼器只有一句材料用途），
    /// 机制与数值放在效果列表里（<c>Building.EffectDescriptors</c> 收集
    /// <c>def.EffectDescription</c> 与各组件 <c>IGameObjectEffectDescriptor.GetDescriptors</c>），
    /// 效果行的悬停是「一句结论 + 来源明细」。因此本文件不再改写原版描述与过热行，
    /// 只补一条本 Mod 自己的效果行。
    ///
    /// 1. 配方详情：<c>ComplexFabricator.AdditionalEffectsForRecipe</c> 追加所选配方的最佳温区。
    ///    这是原版自己的接口调用点，配方名与其它效果保持原版。
    ///
    /// 2. 建筑信息卡效果列表：在 <c>Overheatable.GetDescriptors</c> 之后追加一条
    ///    「工作温区：下限 ~ 上限」。借这个注入点是因为它已经在本 Mod 的建筑上生效，
    ///    追加后温区行紧跟原版「过热温度」行，语义相邻。
    ///
    /// **V0.1.2 及以前的两处覆盖已按原版分工撤销：**
    ///   - 不再重建过热行：原版那一行本来就只显示标签与数值，标签带热量百科链接；
    ///     悬停由「一句说明 + 属性明细」组成，而本 Mod 的规则来源已经作为属性修正项写在
    ///     那份明细里（见 <c>MaterialOverhaulFab.Damage.cs</c> 的
    ///     <c>STRINGS.BUILDING.ATTRIBUTES.MATERIALOVERHAUL.OVERHEAT.DESCRIPTION</c>），
    ///     重建只会丢掉百科链接与明细。
    ///   - 不再往 <c>BuildingDef.Desc</c> 追加机制说明：机制改由下面的效果行承担，
    ///     描述恢复原版用途句。
    ///
    /// 温区行只对**已登记最低工作温度的建筑**（<see cref="BuildingZoneTable"/>）生效，
    /// 没有数值的建筑保持原版显示，不向玩家暗示一个还不存在的约束。
    /// 行里的数值取建筑级静态数据（建筑下限 + 当前过热阈值），不随配方与订单变化，
    /// 避免效果行在切配方时跳动；配方最佳温区仍在配方详情里显示。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "AdditionalEffectsForRecipe")]
    internal static class Patch_ComplexFabricator_AdditionalEffectsForRecipe
    {
        private const string KeyRecipeTempRangeDetail = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.RECIPE_TEMP_RANGE_DETAIL";

        private static void Postfix(ComplexRecipe recipe, ref List<Descriptor> __result)
        {
            try
            {
                if (recipe == null)
                {
                    return;
                }

                TempZone zone;
                if (!RecipeZoneTable.TryGet(recipe.id, out zone) || !zone.Configured)
                {
                    return;
                }

                string template = Strings.Get(KeyRecipeTempRangeDetail);
                if (string.IsNullOrEmpty(template))
                {
                    return;
                }

                string text = string.Format(
                    template,
                    GameUtil.GetFormattedTemperature(zone.BestMin),
                    GameUtil.GetFormattedTemperature(zone.BestMax));

                if (__result == null)
                {
                    __result = new List<Descriptor>();
                }

                string tooltip = MaterialOverhaulFab.LocalizedOr(
                    "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.RECIPE_TEMP_RANGE_TOOLTIP",
                    "建筑温度处于这个范围时，配方的温度效率为100%。\n实际生产速度还受复制人的操作能力影响。");
                __result.Add(new Descriptor(text, tooltip, Descriptor.DescriptorType.Effect));
            }
            catch (Exception e)
            {
                ModLog.Error("配方温度行补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 建筑信息卡效果列表里的**工作温区**行。
    ///
    /// 原版把机制写在效果区、把用途写在描述区；这条效果行就是本 Mod 温控机制的常驻入口：
    /// 常驻只给「工作温区：80 ℃ ~ 240 ℃」这样的标签与数值，规则与模式选择放在悬停里，
    /// 配方最佳温区引导到配方详情。
    ///
    /// **只在真正建成的实例上生效**（<c>MaterialOverhaulFab.Spawned</c>），因此建造菜单里的
    /// 预制件预览保持原版不变。文本全部走本 Mod 的键，不依赖原版 LocString 的类路径。
    /// </summary>
    [HarmonyPatch(typeof(Overheatable), "GetDescriptors")]
    internal static class Patch_Overheatable_GetDescriptors
    {
        private const string KeyTempZoneLine = "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.TEMPZONE_LINE";
        private const string KeyTempZoneTooltip = "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.TEMPZONE_TOOLTIP";

        private static void Postfix(Overheatable __instance, ref List<Descriptor> __result)
        {
            try
            {
                if (__instance == null || __result == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = __instance.GetComponent<MaterialOverhaulFab>();
                if (fab == null || !fab.Spawned)
                {
                    return;
                }

                AppendWorkTemperature(fab.PrefabId, __instance.OverheatTemperature, __result, false);
            }
            catch (Exception e)
            {
                ModLog.Error("信息卡工作温区行补丁异常：" + e);
            }
        }

        internal static void AppendWorkTemperature(string prefabId, float ceilingKelvin, List<Descriptor> descriptors, bool immune)
        {
            // 只对已登记最低工作温度的建筑显示；其它建筑完全保持原版效果列表。
            float floorKelvin;
            if (!BuildingZoneTable.TryGet(prefabId, out floorKelvin))
            {
                return;
            }

            // 普通建筑取过热属性，原版免疫过热的窑炉取独立工作上限。
            if (!(ceilingKelvin > floorKelvin + 0.01f))
            {
                ModLog.WarnOnce("zone|display-invalid|" + prefabId,
                    prefabId + "：下限 " + floorKelvin.ToString("F1") + " K 不低于上限 "
                    + ceilingKelvin.ToString("F1") + " K，信息卡不显示工作温区行。");
                return;
            }

            string lineTemplate = Strings.Get(KeyTempZoneLine);
            if (string.IsNullOrEmpty(lineTemplate))
            {
                return;
            }

            string line = string.Format(
                lineTemplate,
                GameUtil.GetFormattedTemperature(floorKelvin),
                GameUtil.GetFormattedTemperature(ceilingKelvin));

            string tooltipTemplate = Strings.Get(immune
                ? "STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.TEMPZONE_IMMUNE_TOOLTIP" : KeyTempZoneTooltip);
            if (immune && BuildingProfiles.TryGet(prefabId,out BuildingProfile profile) && profile.BuiltInHeating)
                tooltipTemplate = MaterialOverhaulFab.LocalizedOr("STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.IMMUNE_HEATING_TOOLTIP",
                    "主建造材料决定最高工作温度；保留原版过热豁免，不产生过热伤害。内置加热仍受工作温区、供电和运行条件限制。材料仍可发生相变。");
            string tooltip = string.IsNullOrEmpty(tooltipTemplate) ? line : tooltipTemplate;

            descriptors.Add(new Descriptor(line, tooltip, Descriptor.DescriptorType.Effect));
        }
    }
}
