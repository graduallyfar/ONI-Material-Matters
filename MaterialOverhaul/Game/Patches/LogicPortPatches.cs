using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 温区输出端口的说明文本（端口描述／激活说明／非激活说明）与可定位日志。
    ///
    /// 原版
    /// <c>BuildingLoader.InitializeLogicPorts</c> 在创建**建筑预制件**时就把 Def 上的端口描述
    /// 复制给 <c>LogicPorts</c>（`outputPortInfo = def.LogicOutputPorts.ToArray()`）。
    /// 那一刻本 Mod 的本地化键是否已经建好不由我们保证：一旦当时取到空串，
    /// 描述会一直沿用。因此在实例初始化后刷新文本，并核对物理端口、格子命中与选择范围。
    /// V0.1.0 静态定位的坐标错误在 Def 注册处修正；本处不是以文本重写代替悬停验收。
    ///
    /// 用**后置**补丁（v0.0.4 由前置改为后置）有两个原因：
    ///   1. 原版悬停卡（<c>SelectToolHoverTextCard</c>）要读 <c>port.activeDescription</c> 与
    ///      <c>inactiveDescription</c>，后置时物理端口已经建好，文本与端口状态一定一致；
    ///   2. 可以在同一处用 <c>GetPortCell</c> 打印端口的实际格子，便于在游戏内核对端口位置
    ///      （v0.0.4 开发计划 §2.4、验收 G04）。
    /// 文本正确时仍然只是重写一遍，属于幂等操作。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Industrial)]
    [HarmonyPatch(typeof(LogicPorts), "OnSpawn")]
    internal static class Patch_LogicPorts_OnSpawn
    {
        private const string KeyDescription = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.DESCRIPTION";
        private const string KeyActive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.ACTIVE";
        private const string KeyInactive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.INACTIVE";

        private const string KeyControlDescription = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.DESCRIPTION";
        private const string KeyControlActive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.ACTIVE";
        private const string KeyControlInactive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.INACTIVE";

        private static void Prefix(LogicPorts __instance)
        {
            KPrefabID id = __instance.GetComponent<KPrefabID>();
            if (id == null || !BuildingWhitelist.Contains(id.PrefabTag.ToString())) return;
            // 原版 TryGetPortAtCell / GetPortCell 无条件遍历两侧数组。
            // 只补本 Mod 建筑的空数组，不制造额外端口。
            if (__instance.inputPortInfo == null) __instance.inputPortInfo = new LogicPorts.Port[0];
            if (__instance.outputPortInfo == null) __instance.outputPortInfo = new LogicPorts.Port[0];
        }

        private static void Postfix(LogicPorts __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                KPrefabID prefabId = __instance.GetComponent<KPrefabID>();
                if (prefabId == null || !BuildingWhitelist.Contains(prefabId.PrefabTag.ToString()))
                {
                    return;
                }

                string description = PortText(KeyDescription, "最佳温度信号");
                string active = PortText(KeyActive, "建筑温度处于当前配方的最佳温区时，输出<b><style=\"logic_on\">绿色信号</style></b>。\n连接后建筑使用控制模式。");
                string inactive = PortText(KeyInactive, "温度不在最佳温区，或配方没有温度要求时，输出<b><style=\"logic_off\">红色信号</style></b>。\n未连接时建筑使用默认模式。");
                if (BuildingProfiles.TryGet(prefabId.PrefabTag.ToString(),out BuildingProfile profile) && profile.Kind != ProductionKind.Batch)
                {
                    active=PortText("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.INDUSTRIAL_ACTIVE",active);
                    inactive=PortText("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.INDUSTRIAL_INACTIVE",inactive);
                }

                bool found = Rewrite(__instance.outputPortInfo, TemperatureZoneSignal.PortId, description, active, inactive);

                // 「允许／暂停生产」输入端口：端口本体来自原版逻辑控制端口，
                // 说明文本改成设计要求的措辞（默认放行、红色只暂停生产）。
                bool hasControl = false;
                if (AutomationControlScope.IsEnabled(prefabId.PrefabTag.ToString()))
                {
                    hasControl = Rewrite(
                        __instance.inputPortInfo,
                        AutomationControlScope.PortId,
                        PortText(KeyControlDescription, "生产控制"),
                        PortText(KeyControlActive, "<b><style=\"logic_on\">绿色信号</style></b>：允许生产。未连接时允许生产。"),
                        PortText(KeyControlInactive, "<b><style=\"logic_off\">红色信号</style></b>：暂停生产，预热和维持温度不受影响。"));
                }

                if (found)
                {
                    CheckOutput(__instance, prefabId.PrefabTag.ToString());
                    ModLog.Info(prefabId.PrefabTag + "：逻辑端口说明已写入 —— 温区输出「" + description
                        + "」@格子 " + __instance.GetPortCell(TemperatureZoneSignal.PortId)
                        + (hasControl
                            ? "，生产控制输入「" + Strings.Get(KeyControlDescription) + "」@格子 "
                                + __instance.GetPortCell(AutomationControlScope.PortId)
                            : string.Empty)
                        + "。");
                }
                else if (AutomationControlScope.IsWithoutOutput(prefabId.PrefabTag.ToString()))
                {
                    ModLog.Info(prefabId.PrefabTag + "：按设计没有温区输出"
                        + (hasControl
                            ? "；生产控制输入「" + Strings.Get(KeyControlDescription) + "」@格子 "
                                + __instance.GetPortCell(AutomationControlScope.PortId)
                            : string.Empty)
                        + "。");
                }
                else
                {
                    ModLog.WarnOnce("port|spawn-missing|" + prefabId.PrefabTag,
                        prefabId.PrefabTag + "：实例没有温区输出，端口注册未完整生效。");
                }
            }
            catch (Exception e)
            {
                ModLog.Error("逻辑端口说明补丁异常：" + e);
            }
        }

        /// <summary>按端口标识重写说明文本。Port 是结构体，必须用下标写回数组元素。</summary>
        private static bool Rewrite(LogicPorts.Port[] ports, HashedString portId, string description, string active, string inactive)
        {
            if (ports == null)
            {
                return false;
            }

            bool found = false;
            for (int i = 0; i < ports.Length; i++)
            {
                if (ports[i].id != portId)
                {
                    continue;
                }

                ports[i].description = description;
                ports[i].activeDescription = active;
                ports[i].inactiveDescription = inactive;
                ports[i].displayCustomName = true;
                found = true;
            }

            return found;
        }

        private static string PortText(string key, string fallback)
        {
            string text = Strings.Get(key);
            if (string.IsNullOrEmpty(text) || text == key || text.StartsWith("MISSING."))
                ModLog.WarnOnce("port|text|" + key, "端口本地化键缺失：" + key + "，使用内置回退说明。");
            return MaterialOverhaulFab.LocalizedOr(key, fallback);
        }

        /// <summary>每个已建成实例仅在生成时核对一次，不在悬停或每帧扫描建筑。</summary>
        private static void CheckOutput(LogicPorts ports, string prefabId)
        {
            if (ports.GetComponent<BuildingComplete>() == null) return;
            int cell = ports.GetPortCell(TemperatureZoneSignal.PortId);
            LogicPorts.Port hit;
            bool input;
            bool hitOutput = Grid.IsValidCell(cell) && ports.TryGetPortAtCell(cell, out hit, out input)
                && !input && hit.id == TemperatureZoneSignal.PortId;
            int count = 0;
            foreach (LogicPorts.Port port in ports.outputPortInfo)
                if (port.id == TemperatureZoneSignal.PortId) count++;
            bool physicalReady = ports.outputPorts != null
                && ports.outputPorts.Count == ports.outputPortInfo.Length;
            KCollider2D collider = ports.GetComponent<KCollider2D>();
            bool selectableAtPort = false;
            if (Grid.IsValidCell(cell) && collider != null)
            {
                UnityEngine.Vector3 center = Grid.CellToPosCCC(cell, Grid.SceneLayer.Building);
                selectableAtPort = collider.Intersects(new UnityEngine.Vector2(center.x, center.y));
            }
            if (!hitOutput || count != 1 || !physicalReady || !selectableAtPort)
                ModLog.Warn("端口自检 " + prefabId + " @" + cell + "：输出命中 " + hitOutput
                    + "，同 ID 数量 " + count + "，物理列表就绪 " + physicalReady
                    + "，端口中心在选择范围内 " + selectableAtPort + "。需核查悬停链路。");
            else
                ModLog.Info("端口自检 " + prefabId + " @" + cell + "：输出命中、列表与选择范围一致（悬停绘制仍待游戏验收）。");
        }
    }
}
