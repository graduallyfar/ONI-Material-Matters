using System.Collections.Generic;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 「允许／暂停生产」自动化输入的范围与端口位置（建筑细节文档 §2.2.3、V0.0.3 开发计划 §10.4）。
    ///
    /// 本版给首发三栋添加这个端口；粉碎机只有生产许可，没有内置加热。
    /// 三栋以外不添加端口。
    ///
    /// 端口标识复用原版 <see cref="LogicOperationalController.PORT_ID"/> 与
    /// <see cref="LogicOperationalController"/> 组件，因此「未接线 → 允许生产」的默认语义
    /// 与原版逻辑控制建筑完全一致，不额外发明一套未接线规则。
    /// </summary>
    internal static class AutomationControlScope
    {
        private static readonly Dictionary<string, CellOffset> Offsets = new Dictionary<string, CellOffset>
        {
            // 金属精炼器 3×4，输入保留顶排中间；与液体接口和温区输出 (1,3) 不重叠。
            { "MetalRefinery", new CellOffset(0, 3) },
            { "RockCrusher", new CellOffset(0, 3) },
            { "GlassForge", new CellOffset(-1, 3) }
        };

        /// <summary>该建筑是否启用「允许／暂停生产」自动化输入端口。</summary>
        public static bool IsEnabled(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && Offsets.ContainsKey(prefabId);
        }

        /// <summary>端口标识，与原版逻辑控制端口一致。</summary>
        public static HashedString PortId
        {
            get { return LogicOperationalController.PORT_ID; }
        }

        public static bool TryGetOffset(string prefabId, out CellOffset offset)
        {
            offset = default(CellOffset);
            return !string.IsNullOrEmpty(prefabId) && Offsets.TryGetValue(prefabId, out offset);
        }

        public static IEnumerable<string> All
        {
            get { return Offsets.Keys; }
        }
    }
}
