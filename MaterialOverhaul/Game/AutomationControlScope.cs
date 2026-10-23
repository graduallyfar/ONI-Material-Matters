using System.Collections.Generic;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 「允许／暂停生产」自动化输入的范围（建筑细节文档 §2.2.3、V1.0.3 开发计划 #23）。
    ///
    /// 只用原版已有的逻辑控制输入，端口本体、位置都是原版的，本 Mod 不移动、不新增；
    /// 下表只记录原版位置，供注册时核对。原版输入有两种来源：
    ///   - 18 栋在 `*Config` 里用 <c>LogicOperationalController.CreateSingleInputPortList</c> 写明；
    ///   - 10 栋需要电力输入，由 <c>BuildingLoader</c> 按 <c>RequiresPowerInput &amp;&amp; AddLogicPowerPort</c>
    ///     调用 <c>GeneratedBuildings.RegisterSingleLogicInputPort</c> 统一加在 (0,0)。
    ///     这类端口不在 <c>def.LogicInputPorts</c> 里；Def 上一旦写了输入列表，原版
    ///     <c>InitializeLogicPorts</c> 会用它覆盖 (0,0) 端口，所以本 Mod 不能往 Def 写输入。
    /// 矿石洗涤器不用电，原版没有输入，不加。
    ///
    /// 端口标识即原版 <see cref="LogicOperationalController.PORT_ID"/>，「未接线 → 允许生产」
    /// 沿用原版 <see cref="LogicOperationalController"/> 的语义。
    /// </summary>
    internal static class AutomationControlScope
    {
        private static readonly Dictionary<string, CellOffset> Offsets = new Dictionary<string, CellOffset>
        {
            // 原版因需要电力而自带的 (0,0) 输入。
            { "MetalRefinery", new CellOffset(0, 0) }, { "RockCrusher", new CellOffset(0, 0) },
            { "GlassForge", new CellOffset(0, 0) }, { "SupermaterialRefinery", new CellOffset(0, 0) },
            { "DiamondPress", new CellOffset(0, 0) }, { "SludgePress", new CellOffset(0, 0) },
            { "MissileFabricator", new CellOffset(0, 0) }, { "OilRefinery", new CellOffset(0, 0) },
            { "EthanolDistillery", new CellOffset(0, 0) }, { "Desalinator", new CellOffset(0, 0) },
            // 原版 Config 写明的输入。
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

        /// <summary>
        /// 温区输出端口位置。金属精炼器、岩石粉碎机、玻璃熔炉、分子熔炉、聚合物压塑器、泥浆分离器、
        /// 氧石精炼炉、原油精炼器、油井按用户截图标注放在贴图上（V1.0.3 开发计划 #23b），其余保持原位；
        /// 与原版管道或电力接口同格时不冲突（不同图层），但不与原版自动化端口同格。
        /// </summary>
        private static readonly Dictionary<string, CellOffset> OutputOffsets = new Dictionary<string, CellOffset>
        {
            { "MetalRefinery", new CellOffset(-1, 1) },
            { "RockCrusher", new CellOffset(-1, 1) },
            { "GlassForge", new CellOffset(1, 1) },
            { "SupermaterialRefinery", new CellOffset(2, 1) },
            { "ChemicalRefinery", new CellOffset(2, 2) },
            { "Kiln", new CellOffset(1, 1) },
            { "Chlorinator",new CellOffset(1,2) }, { "DiamondPress",new CellOffset(1,4) },
            { "UraniumCentrifuge",new CellOffset(0,3) }, { "RubberMaker",new CellOffset(1,2) },
            { "FabricatedWoodMaker",new CellOffset(2,2) }, { "SludgePress",new CellOffset(1,0) },
            { "MissileFabricator",new CellOffset(2,3) }, { "OilRefinery",new CellOffset(2,0) },
            { "Polymerizer",new CellOffset(1,0) }, { "EthanolDistillery",new CellOffset(2,2) },
            { "FertilizerMaker",new CellOffset(2,2) }, { "AlgaeDistillery",new CellOffset(1,3) },
            { "OxyliteRefinery",new CellOffset(1,0) }, { "WaterPurifier",new CellOffset(1,2) },
            { "Desalinator",new CellOffset(2,2) }, { "OilWellCap",new CellOffset(1,1) },
            { "OreScrubber",new CellOffset(1,2) }
        };

        /// <summary>
        /// 燃料发电机不加温区输出（用户 2026-10-23 指示），只保留原版启停输入，始终按默认模式运行。
        /// </summary>
        private static readonly HashSet<string> WithoutOutput = new HashSet<string>
        {
            "Generator", "PeatGenerator", "HydrogenGenerator", "MethaneGenerator", "PetroleumGenerator", "WoodGasGenerator"
        };

        public static bool TryGetOutputOffset(string prefabId, out CellOffset offset)
        {
            offset = default(CellOffset);
            return !string.IsNullOrEmpty(prefabId) && OutputOffsets.TryGetValue(prefabId, out offset);
        }

        /// <summary>该建筑按设计没有温区输出（不是漏配位置）。</summary>
        public static bool IsWithoutOutput(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && WithoutOutput.Contains(prefabId);
        }

        public static IEnumerable<string> AllWithoutOutput
        {
            get { return WithoutOutput; }
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
