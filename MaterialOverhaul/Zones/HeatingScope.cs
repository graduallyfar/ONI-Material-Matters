using System.Collections.Generic;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 内置加热（预热／补热／强制持续加热）的适用范围（V0.0.3 开发计划 §1.1）。
    ///
    /// 本表只决定内置加热能力：金属、玻璃、分子和化学精炼有；粉碎机与窑炉无。
    /// 温区、效率、生产电力与硬度由首发名单及建筑独立参数决定，不受本表限制。
    ///
    /// 过热阈值与要求硬度属于**建筑级**规则，不走这张表。
    /// </summary>
    public static class HeatingScope
    {
        private static readonly HashSet<string> Fabricators = new HashSet<string>
        {
            "MetalRefinery",
            "GlassForge",
            "SupermaterialRefinery",
            "ChemicalRefinery",
            "DiamondPress", "RubberMaker", "OilRefinery", "Polymerizer", "EthanolDistillery"
        };

        /// <summary>该建筑是否启用本 Mod 的内置加热与加热用电。</summary>
        public static bool IsEnabled(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && Fabricators.Contains(prefabId);
        }

        public static IEnumerable<string> All
        {
            get { return Fabricators; }
        }
    }
}
