using System;
using HarmonyLib;
using KMod;
using MaterialOverhaul.Core;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul
{
    /// <summary>
    /// Mod 入口。原版 DLLLoader 会实例化程序集中的 <see cref="UserMod2"/> 派生类并调用 OnLoad，
    /// 基类默认执行 <c>harmony.PatchAll(assembly)</c>。
    /// </summary>
    public class MaterialOverhaulMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            try
            {
                base.OnLoad(harmony);
            }
            catch (Exception e)
            {
                // 补丁目标未匹配时必须留下可定位记录，不能静默声称生效（§9、AGENTS 实现约定）。
                ModLog.Error("Harmony 补丁装配失败，Mod 未能完整生效：" + e);
                throw;
            }

            Core.DebugValues.Initialize(path);

            // 注册本 Mod 的本地化键树。原版 OverloadStrings 只能覆盖已存在的 LocString 字段、
            // 无法新增键，所以必须在这里显式建键，否则界面会显示 MISSING.STRINGS.*。
            RegisterStrings();
            LoadModStrings();

            // 注意：UserMod2 继承链上存在同名成员 ModInfo，此处必须用限定名指到本 Mod 的常量类。
            ModLog.Info("v" + Core.ModInfo.Version + " 已加载。白名单建筑 " + BuildingWhitelist.Count
                + " 项，配方温区条目 " + RecipeZoneTable.Count
                + "，要求硬度条目 " + HardnessRequirementTable.Count
                + "，内置加热范围 " + JoinScope() + "。");
            LogBalanceSummary();
            ModLog.Info("v0.2.2 材料修订：正向过热加成统一+60%，深渊晶石无加成；矿物原料可用深渊晶石，金属矿石建材槽位也接受精炼金属；伤害速率不变。");
            ModLog.Info("加载路径：" + path + "；程序集：" + typeof(MaterialOverhaulMod).Assembly.Location);
            ModLog.Info("v0.2.0 首发三建筑已接入：全量配方温区、独立效率与磨损、交集回差、注热倍率100；粉碎机无内置加热。");
            ModLog.Info("v0.2.1 界面修订：风险／损坏文本使用完整键读取；模式与运行项仅显示状态栏，不绘制无制作队列的世界图标。");
        }

        /// <summary>启动时把本版关键数值打一条日志，便于核对实际生效的是哪一套（§2.1）。</summary>
        private static void LogBalanceSummary()
        {
            foreach (var entry in Core.BuildingProfiles.All)
                ModLog.Info(entry.Key + "：下限 " + entry.Value.WorkMinCelsius + " ℃，生产 "
                    + entry.Value.ProductionWatts + " W，效率底值 " + entry.Value.LowEfficiency + "/"
                    + entry.Value.HighEfficiency + "，磨损转换 " + entry.Value.DamagePerWearUnit
                    + " HP/单位，内置加热 " + entry.Value.BuiltInHeating + "。");
            ModLog.Info("金属精炼器基准数值：炼铝配方最佳温区 450/650 ℃，建筑下限 "
                + Core.Balance.MetalRefineryWorkMinCelsius.ToString("F0")
                + " ℃（金属精炼器，建筑级），最高工作温度 = 过热阈值 = 主建造材料熔点 × "
                + Heating.OverheatRule.MeltingPointFraction.ToString("F2")
                + " × 主材料过热倍率（按 K 计算）；共用加热功率滑条 " + Core.Balance.MinHeatingWatts.ToString("F0")
                + "–" + Core.Balance.MaxHeatingWatts.ToString("F0") + " W（默认 " + Core.Balance.DefaultHeatingWatts.ToString("F0") + " W）"
                + "，注热倍率 " + Heating.HeatingPower.TestHeatMultiplier.ToString("F0")
                + "；要求硬度 " + Core.Balance.MetalRefineryRequiredHardness
                + "，磨损 " + Hardness.DamageConversion.WearPerProductionSecond.ToString("F2")
                + " 单位/生产秒，伤害 " + Hardness.DamageConversion.DamagePerWearUnit.ToString("F3")
                + " HP/磨损单位；无限订单默认额度 " + Core.Balance.DefaultInfiniteBatchQuota
                + "（上限 " + Core.Balance.MaxInfiniteBatchQuota + "）。");
        }

        private static string JoinScope()
        {
            System.Collections.Generic.List<string> ids = new System.Collections.Generic.List<string>();
            foreach (string id in Zones.HeatingScope.All)
            {
                ids.Add(id);
            }

            return ids.Count == 0 ? "（空）" : string.Join("、", ids.ToArray());
        }

        /// <summary>
        /// 把 <see cref="Game.ModStrings"/> 的各分支注册成键。
        /// 键名 = "STRINGS." + 嵌套类名 + "." + 字段名，所以这里按三个顶层分支分别注册，
        /// 避免把 ModStrings 自身的类名也拼进键里。
        /// </summary>
        private static void RegisterStrings()
        {
            LocString.CreateLocStringKeys(typeof(Game.ModStrings.UI), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(Game.ModStrings.BUILDINGS), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(Game.ModStrings.BUILDING), "STRINGS.");
        }

        /// <summary>
        /// 注册之后再读 mod 的 strings/*.po 做覆盖。
        ///
        /// 顺序很关键：原版自己的 `Mod.LoadStrings` 发生在本 Mod 的 OnLoad **之前**，
        /// 那时键还没建，它覆盖不到任何自定义键；只有这里手动加载才真正生效。
        /// 当前 .po 的内容与字段默认值一致，保留它主要是给后续加其它语言留入口。
        /// </summary>
        private void LoadModStrings()
        {
            try
            {
                string dir = System.IO.Path.Combine(path ?? string.Empty, "strings");
                if (string.IsNullOrEmpty(path) || !System.IO.Directory.Exists(dir))
                {
                    return;
                }

                foreach (string file in System.IO.Directory.GetFiles(dir, "*.po"))
                {
                    Localization.OverloadStrings(Localization.LoadStringsFile(file, isTemplate: false));
                    ModLog.Info("已加载本地化文件 " + System.IO.Path.GetFileName(file));
                }
            }
            catch (Exception e)
            {
                // 本地化失败不影响功能：字段默认值本身就可显示。
                ModLog.Warn("加载本地化文件失败（界面将使用内置默认文本）：" + e.Message);
            }
        }
    }
}
