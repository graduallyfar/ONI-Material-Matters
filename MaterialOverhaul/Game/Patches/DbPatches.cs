using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 在 <c>Db.Initialize</c> 之后登记本 Mod 的状态项。
    /// Db.Initialize 内部会重建 BuildingStatusItems 集合，早于它登记会被覆盖，所以用后置补丁。
    /// </summary>
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class Patch_Db_Initialize
    {
        private static void Postfix()
        {
            try
            {
                DamageStatusItems.Register();
                DebugValueApplier.Apply();
            }
            catch (Exception e)
            {
                ModLog.Error("Db 初始化后置处理失败：" + e);
            }
        }
    }

    /// <summary>
    /// 给白名单建筑补一个自动化输出端口：原版这些配方建筑没有任何逻辑端口（开发计划 §3.9），
    /// 而设计要求温区信号。
    ///
    /// 在 <c>BuildingLoader.CreateBuildingComplete</c> 之前改 Def，确保 LogicPorts 按修改后的
    /// 端口列表挂载；同一端口重复调用只添加一次。
    ///
    /// 参数用 <c>__args</c> 取，不依赖原方法参数名——参数名一旦对不上，Harmony 注入会直接失败。
    /// </summary>
    [HarmonyPatch(typeof(BuildingLoader), "CreateBuildingComplete")]
    internal static class Patch_BuildingLoader_CreateBuildingComplete
    {
        private static void Prefix(object[] __args)
        {
            try
            {
                BuildingDef def = null;
                if (__args != null)
                {
                    foreach (object arg in __args)
                    {
                        def = arg as BuildingDef;
                        if (def != null)
                        {
                            break;
                        }
                    }
                }

                if (def == null || !BuildingWhitelist.Contains(def.PrefabID))
                {
                    return;
                }

                if (def.LogicOutputPorts == null)
                {
                    def.LogicOutputPorts = new System.Collections.Generic.List<LogicPorts.Port>();
                }

                foreach (LogicPorts.Port existing in def.LogicOutputPorts)
                {
                    if (existing.id == TemperatureZoneSignal.PortId)
                    {
                        return;
                    }
                }

                def.LogicOutputPorts.Add(LogicPorts.Port.OutputPort(
                    TemperatureZoneSignal.PortId,
                    new CellOffset(0, 1),
                    Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.DESCRIPTION"),
                    Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.ACTIVE"),
                    Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.INACTIVE")));

                ModLog.Info("已为 " + def.PrefabID + " 添加温区输出端口。");
            }
            catch (Exception e)
            {
                ModLog.Error("添加温区输出端口失败：" + e);
            }
        }
    }
}
