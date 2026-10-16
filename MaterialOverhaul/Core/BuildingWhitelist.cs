using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 生效建筑白名单：只对明确列出的建筑启用，其余建筑行为等同原版
    /// （V0.0.1 开发计划 §1.3 原则 5、§9）。
    ///
    /// 索引使用原版建筑 prefab id（<c>KPrefabID.PrefabTag</c>），不按配置类名匹配：
    /// 同一建筑可能在不同配置类中重复实现，类名匹配不可靠（《建筑分类与温区适用范围》§1.2）。
    ///
    /// 已接入共 29 栋：首发三栋、第二批分子熔炉／化学精炼厂／窑炉，以及用户授权的第三批 23 栋工业建筑。
    /// 食品、纺织、制药、堆肥与工业制造三栋（工作台、焊接台、太空服锻造台）仍不接入。
    /// </summary>
    public static class BuildingWhitelist
    {
        private static readonly HashSet<string> Ids = new HashSet<string>(StringComparer.Ordinal)
        {
            "MetalRefinery",
            "GlassForge",
            "RockCrusher",
            "SupermaterialRefinery",
            "ChemicalRefinery",
            "Kiln",
            "Chlorinator", "DiamondPress", "UraniumCentrifuge", "RubberMaker", "FabricatedWoodMaker", "SludgePress",
            "MissileFabricator", "OilRefinery", "Polymerizer", "EthanolDistillery", "FertilizerMaker", "AlgaeDistillery",
            "OxyliteRefinery", "WaterPurifier", "Desalinator", "Generator", "PeatGenerator", "HydrogenGenerator",
            "MethaneGenerator", "PetroleumGenerator", "WoodGasGenerator", "OilWellCap", "OreScrubber"
        };

        public static int Count
        {
            get { return Ids.Count; }
        }

        public static bool Contains(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && Ids.Contains(prefabId);
        }

        /// <summary>补充名单条目；返回 false 表示已存在或参数无效。</summary>
        public static bool Register(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && Ids.Add(prefabId);
        }

        public static IEnumerable<string> All
        {
            get { return Ids; }
        }
    }
}
