using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 温区输出端口的说明文本（端口描述／激活说明／非激活说明）与可定位日志。
    ///
    /// **为什么要在这里再写一遍（游戏内日志已确认的问题）**：原版
    /// <c>BuildingLoader.InitializeLogicPorts</c> 在创建**建筑预制件**时就把 Def 上的端口描述
    /// 复制给 <c>LogicPorts</c>（`outputPortInfo = def.LogicOutputPorts.ToArray()`）。
    /// 那一刻本 Mod 的本地化键是否已经建好不由我们保证：一旦当时取到空串，
    /// 这个端口在**整个游戏进程内**都没有说明文本，重新读档也不会变。
    ///
    /// 用**后置**补丁（v0.0.4 由前置改为后置）有两个原因：
    ///   1. 原版悬停卡（<c>SelectToolHoverTextCard</c>）要读 <c>port.activeDescription</c> 与
    ///      <c>inactiveDescription</c>，后置时物理端口已经建好，文本与端口状态一定一致；
    ///   2. 可以在同一处用 <c>GetPortCell</c> 打印端口的实际格子，便于在游戏内核对端口位置
    ///      （v0.0.4 开发计划 §2.4、验收 G04）。
    /// 文本正确时仍然只是重写一遍，属于幂等操作。
    /// </summary>
    [HarmonyPatch(typeof(LogicPorts), "OnSpawn")]
    internal static class Patch_LogicPorts_OnSpawn
    {
        private const string KeyDescription = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.DESCRIPTION";
        private const string KeyActive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.ACTIVE";
        private const string KeyInactive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.INACTIVE";

        private const string KeyControlDescription = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.DESCRIPTION";
        private const string KeyControlActive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.ACTIVE";
        private const string KeyControlInactive = "STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.INACTIVE";

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

                string description = Strings.Get(KeyDescription);
                string active = Strings.Get(KeyActive);
                string inactive = Strings.Get(KeyInactive);

                bool found = Rewrite(__instance.outputPortInfo, TemperatureZoneSignal.PortId, description, active, inactive);
                found |= Rewrite(__instance.inputPortInfo, TemperatureZoneSignal.PortId, description, active, inactive);

                // 「允许／暂停生产」输入端口：端口本体来自原版逻辑控制端口，
                // 说明文本改成设计要求的措辞（默认放行、红色只暂停生产）。
                bool hasControl = false;
                if (AutomationControlScope.IsEnabled(prefabId.PrefabTag.ToString()))
                {
                    hasControl = Rewrite(
                        __instance.inputPortInfo,
                        AutomationControlScope.PortId,
                        Strings.Get(KeyControlDescription),
                        Strings.Get(KeyControlActive),
                        Strings.Get(KeyControlInactive));
                    found |= hasControl;
                }

                if (found)
                {
                    ModLog.Info(prefabId.PrefabTag + "：逻辑端口说明已写入 —— 温区输出「" + description
                        + "」@格子 " + __instance.GetPortCell(TemperatureZoneSignal.PortId)
                        + (hasControl
                            ? "，生产控制输入「" + Strings.Get(KeyControlDescription) + "」@格子 "
                                + __instance.GetPortCell(AutomationControlScope.PortId)
                            : string.Empty)
                        + "。");
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
                found = true;
            }

            return found;
        }
    }
}
