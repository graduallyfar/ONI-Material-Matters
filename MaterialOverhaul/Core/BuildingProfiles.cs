using System.Collections.Generic;

namespace MaterialOverhaul.Core
{
    public enum ProductionKind { Batch, Continuous, Generator, OilWell, OreScrubber }
    /// <summary>
    /// 已接入 29 栋建筑的集中数值；原六栋参数保持不变，第三批为授权工作初值。运行许可仍由温控、供电和原版条件判断。
    /// 要求硬度按建筑分档（设计评审 2026-10-23）：机械冲击与钻探 50，低负荷流体或化学处理 10，其余 25。
    /// </summary>
    public sealed class BuildingProfile
    {
        public readonly float WorkMinCelsius;
        public readonly float ProductionWatts;
        public readonly float LowEfficiency;
        public readonly float HighEfficiency;
        public readonly float DamagePerWearUnit;
        public readonly int RequiredHardness;
        public readonly bool BuiltInHeating;
        public readonly bool PreserveOverheatImmunity;
        public readonly ProductionKind Kind;
        public readonly float BestMinCelsius;
        public readonly float BestMaxCelsius;

        internal BuildingProfile(float floor, float watts, float low, float high, float damage, bool heating,
            bool preserveOverheatImmunity = false, ProductionKind kind = ProductionKind.Batch,
            float bestMin = 0f, float bestMax = 0f, int hardness = 25)
        {
            WorkMinCelsius = floor;
            ProductionWatts = watts;
            LowEfficiency = low;
            HighEfficiency = high;
            DamagePerWearUnit = damage;
            RequiredHardness = hardness;
            BuiltInHeating = heating;
            PreserveOverheatImmunity = preserveOverheatImmunity;
            Kind = kind;
            BestMinCelsius = bestMin;
            BestMaxCelsius = bestMax;
        }
    }

    public static class BuildingProfiles
    {
        private static readonly Dictionary<string, BuildingProfile> Entries =
            new Dictionary<string, BuildingProfile>
            {
                { "MetalRefinery", new BuildingProfile(80f, 1200f, 0.30f, 0.80f, 0.05f, true) },
                { "RockCrusher", new BuildingProfile(-20f, 240f, 0.80f, 0.90f, 0.02f, false, hardness: 50) },
                { "GlassForge", new BuildingProfile(80f, 1200f, 0.25f, 0.75f, 0.025f, true) },
                // 第二批工作初值，平衡待验收；窑炉保留用户确认的无电／无内置加热／过热豁免。
                { "SupermaterialRefinery", new BuildingProfile(20f, 1600f, 0.50f, 0.80f, 0.03f, true) },
                { "ChemicalRefinery", new BuildingProfile(0f, 480f, 0.70f, 0.90f, 0.01f, true) },
                { "Kiln", new BuildingProfile(-20f, 0f, 0.50f, 0.80f, 0.02f, false, true) },
                // 新增23栋的用户授权工作初值；保留原六栋全部参数，游戏平衡待验收。
                { "Chlorinator", new BuildingProfile(0f,480f,.75f,.90f,.01f,false) },
                { "DiamondPress", new BuildingProfile(20f,240f,.50f,.80f,.03f,true, hardness: 50) },
                { "UraniumCentrifuge", new BuildingProfile(0f,480f,.70f,.90f,.02f,false,true) },
                { "RubberMaker", new BuildingProfile(0f,480f,.60f,.85f,.02f,true) },
                { "FabricatedWoodMaker", new BuildingProfile(-20f,480f,.80f,.90f,.02f,false) },
                { "SludgePress", new BuildingProfile(-20f,120f,.85f,.95f,.02f,false) },
                { "MissileFabricator", new BuildingProfile(-20f,960f,.85f,.95f,.02f,false) },
                { "OilRefinery", new BuildingProfile(0f,480f,.60f,.85f,.02f,true,false,ProductionKind.Continuous,80f,140f) },
                { "Polymerizer", new BuildingProfile(0f,240f,.60f,.85f,.02f,true,false,ProductionKind.Continuous,70f,130f) },
                { "EthanolDistillery", new BuildingProfile(0f,240f,.65f,.85f,.01f,true,true,ProductionKind.Continuous,60f,100f) },
                { "FertilizerMaker", new BuildingProfile(-10f,120f,.80f,.90f,.01f,false,false,ProductionKind.Continuous,20f,50f, hardness: 10) },
                { "AlgaeDistillery", new BuildingProfile(-10f,120f,.85f,.90f,.01f,false,true,ProductionKind.Continuous,20f,40f, hardness: 10) },
                { "OxyliteRefinery", new BuildingProfile(-20f,1200f,.85f,.90f,.01f,false,true,ProductionKind.Continuous,10f,40f, hardness: 10) },
                { "WaterPurifier", new BuildingProfile(-20f,120f,.90f,.95f,.01f,false,false,ProductionKind.Continuous,10f,50f, hardness: 10) },
                { "Desalinator", new BuildingProfile(-20f,480f,.85f,.95f,.02f,false,false,ProductionKind.Continuous,10f,50f, hardness: 10) },
                // ProductionWatts仅表示耗电；发电额定值仍由原版Generator保存，不能把发电记成消费。
                { "Generator", new BuildingProfile(-20f,0f,.80f,.90f,.02f,false,false,ProductionKind.Generator,60f,180f) },
                { "PeatGenerator", new BuildingProfile(-20f,0f,.85f,.90f,.02f,false,false,ProductionKind.Generator,40f,140f) },
                { "HydrogenGenerator", new BuildingProfile(-20f,0f,.85f,.90f,.01f,false,false,ProductionKind.Generator,40f,120f) },
                { "MethaneGenerator", new BuildingProfile(-20f,0f,.80f,.90f,.02f,false,false,ProductionKind.Generator,60f,160f) },
                { "PetroleumGenerator", new BuildingProfile(-20f,0f,.80f,.90f,.02f,false,false,ProductionKind.Generator,80f,180f) },
                { "WoodGasGenerator", new BuildingProfile(-20f,0f,.85f,.90f,.02f,false,false,ProductionKind.Generator,40f,120f) },
                { "OilWellCap", new BuildingProfile(0f,240f,.80f,.90f,.02f,false,false,ProductionKind.OilWell,60f,120f, hardness: 50) },
                { "OreScrubber", new BuildingProfile(-20f,0f,.90f,.95f,.005f,false,false,ProductionKind.OreScrubber,10f,40f, hardness: 10) }
            };

        public static IReadOnlyDictionary<string, BuildingProfile> All { get { return Entries; } }

        public static bool TryGet(string prefabId, out BuildingProfile profile)
        {
            profile = null;
            return !string.IsNullOrEmpty(prefabId) && Entries.TryGetValue(prefabId, out profile);
        }
    }
}
