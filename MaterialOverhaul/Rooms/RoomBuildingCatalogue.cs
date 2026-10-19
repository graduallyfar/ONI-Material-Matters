using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 房间相关建筑分类的显式名单。
    ///
    /// 只列明确写出的 prefab id，不按共享组件或「看起来像」自动扩类；
    /// 设计文档里尚未给出最终名单的类别，这里先按当前工作名单实现，并在
    /// <c>docs/开发计划/V3.0.0开发计划.md</c> 记录待确认项（D06、D09）。
    ///
    /// 注意原版「工业机械」（<c>ConstraintTags.IndustrialMachinery</c>）与本 Mod 的
    /// 「工业建筑」是两种用途：前者沿用原版标签，后者由下面的名单决定，二者不互相替代。
    /// </summary>
    public static class RoomBuildingCatalogue
    {
        public const string Shower = "Shower";
        public const string ParkSign = "ParkSign";
        public const string Jukebot = "Jukebot";
        public const string ArcadeMachine = "ArcadeMachine";
        public const string BeachChair = "BeachChair";
        public const string MechanicalSurfboard = "MechanicalSurfboard";
        public const string VerticalWindTunnel = "VerticalWindTunnel";
        public const string Sauna = "Sauna";
        public const string HotTub = "HotTub";
        public const string EspressoMachine = "EspressoMachine";
        public const string SodaFountain = "SodaFountain";
        public const string WaterCooler = "WaterCooler";
        public const string Juicer = "Juicer";
        public const string Pedestal = "Pedestal";
        public const string MissionControl = "MissionControl";
        public const string MissionControlCluster = "MissionControlCluster";
        public const string GeoTuner = "GeoTuner";
        public const string LaunchPad = "LaunchPad";
        public const string RailGun = "RailGun";
        public const string RailGunPayloadOpener = "RailGunPayloadOpener";
        public const string LogicInterasteroidSender = "LogicInterasteroidSender";
        public const string LogicInterasteroidReceiver = "LogicInterasteroidReceiver";
        public const string DataMiner = "DataMiner";
        public const string MedicalCot = "MedicalCot";
        public const string DoctorStation = "DoctorStation";
        public const string Apothecary = "Apothecary";
        public const string AdvancedDoctorStation = "AdvancedDoctorStation";

        /// <summary>
        /// 工业建筑工作名单：以生产／加工建筑为主，不含发电建筑（发电厂是独立房间类型）。
        /// 设计尚未给出最终名单，本轮按工业温控项目的生产建筑范围收敛，D09 确认后更新。
        /// </summary>
        private static readonly HashSet<string> Industrial = new HashSet<string>(StringComparer.Ordinal)
        {
            "MetalRefinery",
            "GlassForge",
            "RockCrusher",
            "SupermaterialRefinery",
            "ChemicalRefinery",
            "Kiln",
            "Chlorinator",
            "DiamondPress",
            "UraniumCentrifuge",
            "RubberMaker",
            "FabricatedWoodMaker",
            "SludgePress",
            "MissileFabricator",
            "OilRefinery",
            "Polymerizer",
            "EthanolDistillery",
            "FertilizerMaker",
            "AlgaeDistillery",
            "OxyliteRefinery",
            "WaterPurifier",
            "Desalinator",
            "OilWellCap",
            "OreScrubber"
        };

        /// <summary>
        /// 高级娱乐建筑工作名单：用于「除专属设施外，无其他高级娱乐建筑」。
        /// </summary>
        private static readonly HashSet<string> AdvancedRecreation = new HashSet<string>(StringComparer.Ordinal)
        {
            Jukebot,
            ArcadeMachine,
            BeachChair,
            MechanicalSurfboard,
            VerticalWindTunnel,
            Sauna,
            HotTub,
            EspressoMachine,
            SodaFountain,
            WaterCooler,
            Juicer
        };

        /// <summary>
        /// 生活建筑工作名单：床、厕所、餐桌与娱乐设施。只用于航天发射场的「无生活建筑」，
        /// 不代表完整分类。D06 确认后更新。
        /// </summary>
        private static readonly HashSet<string> Life = new HashSet<string>(StringComparer.Ordinal)
        {
            "Bed",
            "LuxuryBed",
            "Clinic",
            MedicalCot,
            "DiningTable",
            "MultiMinionDiningTable",
            "Outhouse",
            "FlushToilet",
            "WallToilet",
            Jukebot,
            ArcadeMachine,
            BeachChair,
            MechanicalSurfboard,
            VerticalWindTunnel,
            Sauna,
            HotTub,
            EspressoMachine,
            SodaFountain,
            WaterCooler,
            Juicer
        };

        public static bool IsIndustrialBuilding(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && Industrial.Contains(prefabId);
        }

        public static bool IsAdvancedRecreation(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && AdvancedRecreation.Contains(prefabId);
        }

        public static bool IsLifeBuilding(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && Life.Contains(prefabId);
        }

        public static IEnumerable<string> IndustrialBuildings
        {
            get { return Industrial; }
        }

        public static IEnumerable<string> AdvancedRecreationBuildings
        {
            get { return AdvancedRecreation; }
        }

        public static IEnumerable<string> LifeBuildings
        {
            get { return Life; }
        }
    }
}
