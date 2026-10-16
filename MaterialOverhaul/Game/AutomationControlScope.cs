using System.Collections.Generic;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 「允许／暂停生产」自动化输入的范围与端口位置（建筑细节文档 §2.2.3、V0.0.3 开发计划 §10.4）。
    ///
    /// 本版覆盖全部 29 栋接入建筑；已有原版逻辑控制输入的建筑直接复用其位置（见 §3 表格说明）。
    /// 粉碎机、窑炉、发电机与洗涤器等没有内置加热；名单以外不添加端口。
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
            { "GlassForge", new CellOffset(-1, 3) },
            { "SupermaterialRefinery", new CellOffset(0, 4) },
            // 复用这两栋原版已存在的输入位置与逻辑控制组件。
            { "ChemicalRefinery", new CellOffset(0, 0) },
            { "Kiln", new CellOffset(0, 1) },
            { "Chlorinator",new CellOffset(0,1) }, { "DiamondPress",new CellOffset(0,4) },
            { "UraniumCentrifuge",new CellOffset(0,0) }, { "RubberMaker",new CellOffset(0,1) },
            { "FabricatedWoodMaker",new CellOffset(1,0) }, { "SludgePress",new CellOffset(0,2) },
            { "MissileFabricator",new CellOffset(0,3) }, { "OilRefinery",new CellOffset(0,3) },
            { "Polymerizer",new CellOffset(0,1) }, { "EthanolDistillery",new CellOffset(0,2) },
            { "FertilizerMaker",new CellOffset(-1,0) }, { "AlgaeDistillery",new CellOffset(0,1) },
            { "OxyliteRefinery",new CellOffset(0,1) }, { "WaterPurifier",new CellOffset(-1,0) },
            { "Desalinator",new CellOffset(0,2) }, { "Generator",new CellOffset(0,0) },
            { "PeatGenerator",new CellOffset(0,0) }, { "HydrogenGenerator",new CellOffset(-1,0) },
            { "MethaneGenerator",new CellOffset(0,0) }, { "PetroleumGenerator",new CellOffset(0,0) },
            { "WoodGasGenerator",new CellOffset(1,1) }, { "OilWellCap",new CellOffset(0,0) },
            { "OreScrubber",new CellOffset(0,2) }
        };

        private static readonly Dictionary<string, CellOffset> OutputOffsets = new Dictionary<string, CellOffset>
        {
            { "MetalRefinery", new CellOffset(1, 3) },
            { "RockCrusher", new CellOffset(2, 3) },
            { "GlassForge", new CellOffset(2, 3) },
            { "SupermaterialRefinery", new CellOffset(2, 4) },
            { "ChemicalRefinery", new CellOffset(2, 2) },
            { "Kiln", new CellOffset(1, 1) },
            { "Chlorinator",new CellOffset(1,2) }, { "DiamondPress",new CellOffset(1,4) },
            { "UraniumCentrifuge",new CellOffset(0,3) }, { "RubberMaker",new CellOffset(1,2) },
            { "FabricatedWoodMaker",new CellOffset(2,2) }, { "SludgePress",new CellOffset(2,2) },
            { "MissileFabricator",new CellOffset(2,3) }, { "OilRefinery",new CellOffset(2,3) },
            { "Polymerizer",new CellOffset(1,2) }, { "EthanolDistillery",new CellOffset(2,2) },
            { "FertilizerMaker",new CellOffset(2,2) }, { "AlgaeDistillery",new CellOffset(1,3) },
            { "OxyliteRefinery",new CellOffset(1,3) }, { "WaterPurifier",new CellOffset(1,2) },
            { "Desalinator",new CellOffset(2,2) }, { "Generator",new CellOffset(1,2) },
            { "PeatGenerator",new CellOffset(1,1) }, { "HydrogenGenerator",new CellOffset(2,2) },
            { "MethaneGenerator",new CellOffset(1,2) }, { "PetroleumGenerator",new CellOffset(1,3) },
            { "WoodGasGenerator",new CellOffset(0,1) }, { "OilWellCap",new CellOffset(2,3) },
            { "OreScrubber",new CellOffset(1,2) }
        };

        public static bool TryGetOutputOffset(string prefabId, out CellOffset offset)
        {
            offset = default(CellOffset);
            return !string.IsNullOrEmpty(prefabId) && OutputOffsets.TryGetValue(prefabId, out offset);
        }

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
