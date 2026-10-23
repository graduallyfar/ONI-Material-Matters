using System.Collections.Generic;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 「允许／暂停生产」自动化输入的范围（建筑细节文档 §2.2.3、V1.0.2 开发计划 §9 #23）。
    ///
    /// 只用原版已有的逻辑控制输入：29 栋接入建筑中原版带
    /// <c>LogicOperationalController.CreateSingleInputPortList</c> 的 18 栋。端口本体、位置都是原版的，
    /// 本 Mod 不移动、不新增；下表只记录原版位置，供注册时核对。原版不能用自动化控制的
    /// 11 栋（金属精炼器、粉碎机、玻璃熔炉、分子熔炉、钻石压机、淤泥压榨机、导弹制造机、
    /// 原油精炼器、乙醇蒸馏器、海水淡化器、矿石洗涤器）不加输入端口。乙醇蒸馏器原版虽挂了
    /// 逻辑控制组件，但没有定义端口，同样归入不加。
    ///
    /// 端口标识即原版 <see cref="LogicOperationalController.PORT_ID"/>，「未接线 → 允许生产」
    /// 沿用原版 <see cref="LogicOperationalController"/> 的语义。
    /// </summary>
    internal static class AutomationControlScope
    {
        private static readonly Dictionary<string, CellOffset> Offsets = new Dictionary<string, CellOffset>
        {
            { "ChemicalRefinery", new CellOffset(0, 0) },
            { "Kiln", new CellOffset(0, 1) },
            { "Chlorinator",new CellOffset(0,1) },
            { "UraniumCentrifuge",new CellOffset(0,0) }, { "RubberMaker",new CellOffset(0,1) },
            { "FabricatedWoodMaker",new CellOffset(1,0) },
            { "Polymerizer",new CellOffset(0,1) },
            { "FertilizerMaker",new CellOffset(-1,0) }, { "AlgaeDistillery",new CellOffset(0,1) },
            { "OxyliteRefinery",new CellOffset(0,1) }, { "WaterPurifier",new CellOffset(-1,0) },
            { "Generator",new CellOffset(0,0) },
            { "PeatGenerator",new CellOffset(0,0) }, { "HydrogenGenerator",new CellOffset(-1,0) },
            { "MethaneGenerator",new CellOffset(0,0) }, { "PetroleumGenerator",new CellOffset(0,0) },
            { "WoodGasGenerator",new CellOffset(1,1) }, { "OilWellCap",new CellOffset(0,0) }
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

        /// <summary>原版逻辑控制输入的位置（只用于核对，不据此新增端口）。</summary>
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
