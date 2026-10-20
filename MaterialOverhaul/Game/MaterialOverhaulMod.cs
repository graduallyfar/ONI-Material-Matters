using System;
using HarmonyLib;
using KMod;
using MaterialOverhaul.Core;
#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Zones;
#endif

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

#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY
            Core.DebugValues.Initialize(path);
#endif

            // 注册本 Mod 的本地化键树。原版 OverloadStrings 只能覆盖已存在的 LocString 字段、
            // 无法新增键，所以必须在这里显式建键，否则界面会显示 MISSING.STRINGS.*。
            RegisterStrings();
            LoadModStrings();

            ModLog.Info("v" + Core.ModInfo.Version + "；构建模式：" + BuildProfile.DisplayName
                + " [" + BuildProfile.Name + "]；工业=" + BuildProfile.IndustrialEnabled + "；电力=" + BuildProfile.PowerEnabled
                + "；房间=" + BuildProfile.RoomEnabled + "。");
#if !MATERIAL_OVERHAUL_INDUSTRIAL_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY
            ModLog.Info("电力0.0.2：按最短物理路径实际支付损耗；设备、电池充放电与变压器分账；信息卡显示电线数量、输送和损耗。系数与电导率为首轮测试初值，尚未通过游戏平衡验收。");
#endif

#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY
            // 注意：UserMod2 继承链上存在同名成员 ModInfo，此处必须用限定名指到本 Mod 的常量类。
            ModLog.Info("v" + Core.ModInfo.Version + " 已加载。白名单建筑 " + BuildingWhitelist.Count
                + " 项，配方温区条目 " + RecipeZoneTable.Count
                + "，要求硬度条目 " + HardnessRequirementTable.Count
                + "，内置加热范围 " + JoinScope() + "。");
            LogBalanceSummary();
            ModLog.Info("第二批三建筑已接入：分子熔炉、化学精炼厂、窑炉；共6栋、51个配方候选。窑炉保留无电、无内置加热与原版过热豁免，版本保持0.2.2。");
            ModLog.Info("第三批23栋工业建筑已接入（批次7、连续转换8、燃料发电6、油井1、矿石洗涤器1）；名单共29栋、60个配方候选。"
                + "新增五栋（钻石压机、硫化器、原油精炼器、聚合物压塑器、乙醇蒸馏器）有内置加热；"
                + "铀素离心机、乙醇蒸馏器、藻类蒸馏器、氧石精炼炉保留原版过热豁免；油井原 2000 ℃ 基础阈值改由材料上限决定，需单独验收。"
                + "数值为工作初值，游戏平衡未验收。");
            ModLog.Info("v0.2.2 材料修订：正向过热加成统一+60%，深渊晶石无加成；矿物原料可用深渊晶石，金属矿石建材槽位也接受精炼金属；伤害速率不变。");
            ModLog.Info("加载路径：" + path + "；程序集：" + typeof(MaterialOverhaulMod).Assembly.Location);
            ModLog.Info("v0.2.0 首发三建筑已接入：全量配方温区、独立效率与磨损、交集回差、注热倍率100；粉碎机无内置加热。");
            ModLog.Info("v0.2.1 界面修订：风险／损坏文本使用完整键读取；模式与运行项仅显示状态栏，不绘制无制作队列的世界图标。");
#else
            ModLog.Info("工业温控、预热补热、订单、磨损、材料分类及过热改造未编入此验证版；工业建筑沿用原版机制。");
            ModLog.Info("加载路径：" + path + "；程序集：" + typeof(MaterialOverhaulMod).Assembly.Location);
#endif
#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_INDUSTRIAL_ONLY
            ModLog.Info("房间系统：16 个新增房间、3 项原版改造、平均装饰度／温度条件（含保持回差）与独立模式已编入；"
                + "士气、压力、医疗（含优先入院与辐射拯救者）、特质、生产效率、科研、航天发射场与星际货运站效果已接入；数值为工作初值，游戏验收待执行。");
#endif
        }

#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY
        /// <summary>启动时把本版关键数值打一条日志，便于核对实际生效的是哪一套（§2.1）。</summary>
        private static void LogBalanceSummary()
        {
            foreach (var entry in Core.BuildingProfiles.All)
                ModLog.Info(entry.Key + "：下限 " + entry.Value.WorkMinCelsius + " ℃，生产 "
                    + entry.Value.ProductionWatts + " W，效率底值 " + entry.Value.LowEfficiency + "/"
                    + entry.Value.HighEfficiency + "，磨损转换 " + entry.Value.DamagePerWearUnit
                    + " HP/单位，内置加热 " + entry.Value.BuiltInHeating + "，原版过热豁免 " + entry.Value.PreserveOverheatImmunity + "。");
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
#endif

        /// <summary>
        /// 把 <see cref="Game.ModStrings"/> 的各分支注册成键。
        /// 键名 = "STRINGS." + 嵌套类名 + "." + 字段名，所以这里按三个顶层分支分别注册，
        /// 避免把 ModStrings 自身的类名也拼进键里。
        /// </summary>
        private static void RegisterStrings()
        {
#if !MATERIAL_OVERHAUL_ROOM_ONLY
            LocString.CreateLocStringKeys(typeof(Game.ModStrings.UI), "STRINGS.");
#endif
#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_ROOM_ONLY
            LocString.CreateLocStringKeys(typeof(Game.ModStrings.BUILDINGS), "STRINGS.");
            LocString.CreateLocStringKeys(typeof(Game.ModStrings.BUILDING), "STRINGS.");
#endif
#if !MATERIAL_OVERHAUL_POWER_ONLY && !MATERIAL_OVERHAUL_INDUSTRIAL_ONLY
            LocString.CreateLocStringKeys(typeof(Game.RoomStrings.ROOMS), "STRINGS.");
#endif
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
