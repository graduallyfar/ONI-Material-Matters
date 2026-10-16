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

                // 运行状态改到信息卡状态栏显示（V0.0.4 开发计划 §2.3）；同样是 Db 初始化之后登记。
                RunStatusItem.Register();
                RunModeStatusItem.Register();
                MaterialRiskStatusItems.Register();

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
        /// 坐标语义：`CellOffset` 相对建筑原点，横向不是左下角。
        /// BuildingDef.GenerateOffsets 的左界为 width / 2 - width + 1，右界为 width / 2。
        /// 由 `LogicPorts.GetActualCell` 经 `Grid.OffsetCell(Grid.PosToCell(建筑位置), offset)` 换算；
        /// 建筑可旋转时偏移会跟着旋转（首发三栋配方建筑不可旋转）。
        ///
        /// 位置取自各建筑的**右上角**，依据是目标版本里这些建筑已有的端口分布：
        ///   - MetalRefinery 3×4：液体输入 (-1,1)、液体输出 (1,0)，都在左下侧
        ///   - GlassForge    5×4：液体输出 (1,3)，在顶排中段
        ///   - RockCrusher   4×4：无其它端口
        ///   - SupermaterialRefinery 4×5：输出 (2,4)，新增输入 (0,4)
        ///   - ChemicalRefinery 4×3：输出 (2,2)，避开液体输出 (2,1)，复用输入 (0,0)
        ///   - Kiln 2×2：输出 (1,1)，复用原版输入 (0,1)
        /// 位置集中在 AutomationControlScope，与其它接口不重叠，且均在建筑占格内。
        /// </summary>

        /// <summary>
        /// 「允许／暂停生产」自动化**输入**端口的位置来自 <see cref="AutomationControlScope"/>：
        /// 本版覆盖接入的六栋，已有原版输入直接复用。
        /// </summary>
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

                AddProductionControlPort(def);
                AddTemperatureZonePort(def);
            }
            catch (Exception e)
            {
                ModLog.Error("添加逻辑端口失败：" + e);
            }
        }

        /// <summary>
        /// 添加「允许／暂停生产」输入端口。
        ///
        /// 复用原版 <see cref="LogicOperationalController"/> 的端口标识与组件：
        /// 它的「未接线按 <c>unNetworkedValue = 1</c> 处理」语义正是本设计要的默认放行，
        /// 并且会顺带给出原版的「逻辑控制：已启用／已暂停」状态项。
        /// 端口说明文本由本 Mod 在 <c>LogicPorts.OnSpawn</c> 时改成「允许／暂停生产」。
        /// </summary>
        private static void AddProductionControlPort(BuildingDef def)
        {
            CellOffset offset;
            if (!AutomationControlScope.TryGetOffset(def.PrefabID, out offset))
            {
                return;
            }

            if (def.LogicInputPorts == null)
            {
                def.LogicInputPorts = new System.Collections.Generic.List<LogicPorts.Port>();
            }

            foreach (LogicPorts.Port existing in def.LogicInputPorts)
            {
                if (existing.id == AutomationControlScope.PortId)
                {
                    return;
                }
            }

            def.LogicInputPorts.Add(LogicPorts.Port.InputPort(
                AutomationControlScope.PortId,
                offset,
                Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.DESCRIPTION"),
                Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.ACTIVE"),
                Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.PRODUCTION_CONTROL.INACTIVE"),
                show_wire_missing_icon: false,
                display_custom_name: true));

            ModLog.Info("已为 " + def.PrefabID + " 添加「允许／暂停生产」自动化输入端口，位置 "
                + offset.x + "," + offset.y + "。");
        }

        private static void AddTemperatureZonePort(BuildingDef def)
        {
            if (!AutomationControlScope.TryGetOutputOffset(def.PrefabID, out CellOffset offset))
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
                Strings.Get("STRINGS.BUILDING.LOGICPORTS.MATERIALOVERHAUL.TEMPERATURE_ZONE.INACTIVE"),
                show_wire_missing_icon: false,
                display_custom_name: true));

            ModLog.Info("已为 " + def.PrefabID + " 添加温区输出端口，位置 " + offset.x + "," + offset.y + "。");
        }
    }
}
