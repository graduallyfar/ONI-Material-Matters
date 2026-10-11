using System.Collections.Generic;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 内置加热（预热／补热／强制持续加热）的适用范围（V0.0.3 开发计划 §1.1）。
    ///
    /// 温区、温度效率与内置加热**只对明确列出的建筑生效**：本版仅有金属精炼器。
    /// 即使某个建筑因为调试样例温区而拿到了温区数值，也不会因此获得加热与电网需求——
    /// 新增游戏接入不能仅凭「挂上了本 Mod 的组件」就扩散到五个白名单建筑。
    ///
    /// 过热阈值与要求硬度属于**建筑级**规则，不走这张表。
    /// </summary>
    public static class HeatingScope
    {
        private static readonly HashSet<string> Fabricators = new HashSet<string>
        {
            "MetalRefinery"
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
