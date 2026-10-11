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

                // 先登记已确认的设计数值，调试样例值只填补仍未配置的条目。
                ContentRegistration.Apply();
                DebugValueApplier.Apply();
            }
            catch (Exception e)
            {
                ModLog.Error("Db 初始化后置处理失败：" + e);
            }
        }
    }

    /// <summary>
    /// 在 <c>ComplexRecipeManager.PostProcess</c> 之后登记配方温区。
    ///
    /// **这是必须的时机修正（游戏内日志已确认）**：`Db.Initialize` 跑在建筑预制件与配方注册之前，
    /// 那时 `ComplexRecipeManager.recipes` 是空的，温区登记会全部落空（表现为「配方温区条目 0」、
    /// 建筑在常温下照样生产）。原版在 `Assets.CreatePrefabs` 末尾调用 `PostProcess()` 把
    /// `preProcessRecipes` 转入 `recipes`，因此在这里补一次登记。
    /// </summary>
    [HarmonyPatch(typeof(ComplexRecipeManager), "PostProcess")]
    internal static class Patch_ComplexRecipeManager_PostProcess
    {
        private static void Postfix()
        {
            try
            {
                ContentRegistration.Apply();

                // 调试样例温区同样要在配方表可用之后写入（未被真实数值覆盖的配方才会用到它）。
                DebugValueApplier.Apply();
            }
            catch (Exception e)
            {
                ModLog.Error("配方注册后登记内容失败：" + e);
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
        /// <summary>
        /// 温区输出端口位置，按建筑逐个确定，**不是统一偏移**。
        ///
        /// 坐标语义（已核实）：`CellOffset` 相对建筑**原点格**（左下角），
        /// 由 `LogicPorts.GetActualCell` 经 `Grid.OffsetCell(Grid.PosToCell(建筑位置), offset)` 换算；
        /// 建筑可旋转时偏移会跟着旋转（这五个配方建筑不可旋转）。
        ///
        /// 位置取自各建筑的**右上角**，依据是目标版本里这些建筑已有的端口分布：
        ///   - MetalRefinery 3×4：液体输入 (-1,1)、液体输出 (1,0)，都在左下侧
        ///   - GlassForge    5×4：液体输出 (1,3)，在顶排中段
        ///   - Kiln          2×2：自动化输入 (0,1)，在左上角
        ///   - RockCrusher   4×4：无其它端口
        ///   - RubberMaker   3×3：自动化输入 (0,1)、液体输入 (1,0)
        /// 右上角与上述位置均不重叠，也避开建筑底部的接口与操作位。
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, CellOffset> OutputPortOffsets =
            new System.Collections.Generic.Dictionary<string, CellOffset>
            {
                { "MetalRefinery", new CellOffset(2, 3) },
                { "GlassForge", new CellOffset(4, 3) },
                { "Kiln", new CellOffset(1, 1) },
                { "RockCrusher", new CellOffset(3, 3) },
                { "RubberMaker", new CellOffset(2, 2) }
            };

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

                if (!OutputPortOffsets.TryGetValue(def.PrefabID, out CellOffset offset))
                {
                    // 名单内但还没有确定端口位置的建筑：不加端口，并留下可定位记录（不静默）。
                    ModLog.WarnOnce("port|missing|" + def.PrefabID,
                        def.PrefabID + " 已列入白名单但还没有配置温区输出端口位置，本次不添加端口。");
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
                    offset,
                    Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.DESCRIPTION"),
                    Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.ACTIVE"),
                    Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.INACTIVE")));

                ModLog.Info("已为 " + def.PrefabID + " 添加温区输出端口，位置 " + offset.x + "," + offset.y + "。");
            }
            catch (Exception e)
            {
                ModLog.Error("添加温区输出端口失败：" + e);
            }
        }
    }
}
