using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 房间配置目录（房间系统设计 §4.1）。数值与条件集中在这里，游戏接入层只负责
    /// 把定义翻译成原版 <c>RoomType</c>／<c>RoomConstraints.Constraint</c>。
    ///
    /// 新增房间的 id 稳定为 <c>MO_*</c>；改造项使用原版房间 id，不新建类型。
    /// 本轮只登记构成与环境条件；效果（士气、压力、效率、医疗、科研、航天）在对应
    /// 模块实现后才写入 <see cref="RoomDefinition.EffectIds"/>，未实装的效果不显示。
    /// </summary>
    public static class RoomCatalogue
    {
        public const string DlcExpansion1 = "EXPANSION1_ID";
        public const string Dlc3 = "DLC3_ID";

        public const string IndustrialAreaId = "MO_IndustrialArea";
        public const string DataCenterId = "MO_DataCenter";

        private const string Prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.";

        private static readonly RoomDefinition[] NewRoomsValue =
        {
            new RoomDefinition
            {
                Id = "MO_ShowerRoom",
                Tier = RoomTier.Improved,
                CategoryKey = "Bathroom",
                NameKey = Prefix + "SHOWER_ROOM.NAME",
                DescriptionKey = Prefix + "SHOWER_ROOM.DESC",
                TooltipKey = Prefix + "SHOWER_ROOM.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.Shower },
                Additional = new[]
                {
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(64)
                },
                // 同时存在厕所时保留原版卫生间／公共厕所，淋浴间只用于没有厕所的独立淋浴区。
                Beats = null,
                EffectIds = new[] { RoomEffectIds.ShowerRelief },
                Priority = 0,
                SortKey = 100
            },
            new RoomDefinition
            {
                Id = "MO_ArtificialPark",
                Tier = RoomTier.Improved,
                CategoryKey = "Park",
                NameKey = Prefix + "ARTIFICIAL_PARK.NAME",
                DescriptionKey = Prefix + "ARTIFICIAL_PARK.DESC",
                TooltipKey = Prefix + "ARTIFICIAL_PARK.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.ParkSign },
                Additional = new[]
                {
                    RoomRequirement.MinimumPlants(4),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(64)
                },
                Beats = new[] { "Park", "NatureReserve" },
                EffectIds = new[] { RoomEffectIds.ArtificialParkMorale },
                Priority = 1,
                SortKey = 101
            },
            new RoomDefinition
            {
                Id = IndustrialAreaId,
                Tier = RoomTier.Advanced,
                CategoryKey = "Industrial",
                NameKey = Prefix + "INDUSTRIAL_AREA.NAME",
                DescriptionKey = Prefix + "INDUSTRIAL_AREA.DESC",
                TooltipKey = Prefix + "INDUSTRIAL_AREA.TOOLTIP",
                PrimaryPrefabIds = new[]
                {
                    "MetalRefinery", "GlassForge", "RockCrusher", "SupermaterialRefinery",
                    "ChemicalRefinery", "Kiln", "Chlorinator", "DiamondPress",
                    "UraniumCentrifuge", "RubberMaker", "FabricatedWoodMaker", "SludgePress",
                    "MissileFabricator", "OilRefinery", "Polymerizer", "EthanolDistillery",
                    "FertilizerMaker", "AlgaeDistillery", "OxyliteRefinery", "WaterPurifier",
                    "Desalinator", "OilWellCap", "OreScrubber"
                },
                Additional = new[]
                {
                    RoomRequirement.RequireIndustrialBuilding(),
                    RoomRequirement.RequireStorage(),
                    RoomRequirement.RequireLight(),
                    RoomRequirement.MinimumDecor(60f),
                    RoomRequirement.MinimumSize(32),
                    RoomRequirement.MaximumSize(1000)
                },
                Priority = 1,
                SortKey = 102
            },
            new RoomDefinition
            {
                Id = "MO_MedicalCenter",
                Tier = RoomTier.Advanced,
                CategoryKey = "Hospital",
                NameKey = Prefix + "MEDICAL_CENTER.NAME",
                DescriptionKey = Prefix + "MEDICAL_CENTER.DESC",
                TooltipKey = Prefix + "MEDICAL_CENTER.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.MedicalCot },
                Additional = new[]
                {
                    RoomRequirement.RequireAllPrefabs(
                        RoomBuildingCatalogue.MedicalCot,
                        RoomBuildingCatalogue.DoctorStation,
                        RoomBuildingCatalogue.Apothecary,
                        RoomBuildingCatalogue.AdvancedDoctorStation),
                    RoomRequirement.RequireToilet(),
                    RoomRequirement.RequireDiningTable(),
                    RoomRequirement.RequireLight(),
                    RoomRequirement.MinimumDecor(80f),
                    RoomRequirement.TemperatureRange(20f, 26f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(24),
                    RoomRequirement.MaximumSize(108)
                },
                Beats = new[] { "Hospital" },
                Priority = 2,
                SortKey = 103
            },
            new RoomDefinition
            {
                Id = "MO_DanceHall",
                Tier = RoomTier.Advanced,
                CategoryKey = "Recreation",
                NameKey = Prefix + "DANCE_HALL.NAME",
                DescriptionKey = Prefix + "DANCE_HALL.DESC",
                TooltipKey = Prefix + "DANCE_HALL.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.Phonobox },
                Additional = new[]
                {
                    RoomRequirement.NoOtherAdvancedRecreation(),
                    RoomRequirement.RequireDecoration(),
                    RoomRequirement.MinimumDecor(100f),
                    RoomRequirement.TemperatureRange(20f, 26f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(96)
                },
                Beats = new[] { "RecRoom" },
                EffectIds = new[] { RoomEffectIds.DanceHallMorale },
                Priority = 1,
                SortKey = 104
            },
            new RoomDefinition
            {
                Id = "MO_ArcadeHall",
                Tier = RoomTier.Advanced,
                CategoryKey = "Recreation",
                NameKey = Prefix + "ARCADE_HALL.NAME",
                DescriptionKey = Prefix + "ARCADE_HALL.DESC",
                TooltipKey = Prefix + "ARCADE_HALL.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.ArcadeMachine },
                Additional = new[]
                {
                    RoomRequirement.NoOtherAdvancedRecreation(),
                    RoomRequirement.RequireDecoration(),
                    RoomRequirement.MinimumDecor(100f),
                    RoomRequirement.TemperatureRange(20f, 26f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(96)
                },
                Beats = new[] { "RecRoom" },
                EffectIds = new[] { RoomEffectIds.ArcadeHallMorale },
                Priority = 1,
                SortKey = 105
            },
            new RoomDefinition
            {
                Id = "MO_BeachParadise",
                Tier = RoomTier.Advanced,
                CategoryKey = "Recreation",
                NameKey = Prefix + "BEACH_PARADISE.NAME",
                DescriptionKey = Prefix + "BEACH_PARADISE.DESC",
                TooltipKey = Prefix + "BEACH_PARADISE.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.BeachChair },
                Additional = new[]
                {
                    RoomRequirement.NoOtherAdvancedRecreation(),
                    RoomRequirement.RequireDecoration(),
                    RoomRequirement.RequireLight(),
                    RoomRequirement.RequirePrefab(RoomBuildingCatalogue.MechanicalSurfboard),
                    RoomRequirement.MinimumDecor(80f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(96)
                },
                Beats = new[] { "RecRoom" },
                EffectIds = new[] { RoomEffectIds.BeachParadiseMorale },
                Priority = 1,
                SortKey = 106
            },
            new RoomDefinition
            {
                Id = "MO_Gallery",
                Tier = RoomTier.Advanced,
                CategoryKey = "Recreation",
                NameKey = Prefix + "GALLERY.NAME",
                DescriptionKey = Prefix + "GALLERY.DESC",
                TooltipKey = Prefix + "GALLERY.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.ItemPedestal },
                Additional = new[]
                {
                    RoomRequirement.RequireOrnament(),
                    RoomRequirement.RequireMasterpiece(),
                    RoomRequirement.NoOtherAdvancedRecreation(),
                    RoomRequirement.RequireLight(),
                    RoomRequirement.MinimumDecor(120f),
                    RoomRequirement.TemperatureRange(20f, 26f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(18),
                    RoomRequirement.MaximumSize(96)
                },
                Beats = new[] { "RecRoom" },
                EffectIds = new[] { RoomEffectIds.GalleryMorale },
                Priority = 2,
                SortKey = 107
            },
            new RoomDefinition
            {
                Id = "MO_WindTunnel",
                Tier = RoomTier.Advanced,
                CategoryKey = "Recreation",
                NameKey = Prefix + "WIND_TUNNEL.NAME",
                DescriptionKey = Prefix + "WIND_TUNNEL.DESC",
                TooltipKey = Prefix + "WIND_TUNNEL.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.VerticalWindTunnel },
                Additional = new[]
                {
                    RoomRequirement.NoOtherAdvancedRecreation(),
                    RoomRequirement.RequireDecoration(),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumDecor(80f),
                    RoomRequirement.TemperatureRange(20f, 26f),
                    RoomRequirement.MinimumSize(36),
                    RoomRequirement.MaximumSize(96)
                },
                Beats = new[] { "RecRoom" },
                EffectIds = new[] { RoomEffectIds.WindTunnelMorale },
                Priority = 1,
                SortKey = 108
            },
            new RoomDefinition
            {
                Id = "MO_Spa",
                Tier = RoomTier.Advanced,
                CategoryKey = "Recreation",
                NameKey = Prefix + "SPA.NAME",
                DescriptionKey = Prefix + "SPA.DESC",
                TooltipKey = Prefix + "SPA.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.Sauna, RoomBuildingCatalogue.HotTub },
                Additional = new[]
                {
                    RoomRequirement.NoOtherAdvancedRecreation(),
                    RoomRequirement.RequireDecoration(),
                    RoomRequirement.MinimumDecor(80f),
                    RoomRequirement.TemperatureRange(26f, 50f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(16),
                    RoomRequirement.MaximumSize(64)
                },
                Beats = new[] { "RecRoom" },
                EffectIds = new[] { RoomEffectIds.SpaMorale },
                Priority = 1,
                SortKey = 109
            },
            new RoomDefinition
            {
                Id = "MO_TeaRoom",
                Tier = RoomTier.Advanced,
                CategoryKey = "Recreation",
                NameKey = Prefix + "TEA_ROOM.NAME",
                DescriptionKey = Prefix + "TEA_ROOM.DESC",
                TooltipKey = Prefix + "TEA_ROOM.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.EspressoMachine, RoomBuildingCatalogue.SodaFountain },
                Additional = new[]
                {
                    RoomRequirement.RequirePrefab(RoomBuildingCatalogue.WaterCooler),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumDecor(100f),
                    RoomRequirement.TemperatureRange(20f, 26f),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(64)
                },
                Beats = new[] { "RecRoom" },
                EffectIds = new[] { RoomEffectIds.TeaRoomMorale },
                Priority = 1,
                SortKey = 110
            },
            new RoomDefinition
            {
                Id = "MO_MissionControl",
                Tier = RoomTier.Advanced,
                CategoryKey = "Science",
                NameKey = Prefix + "MISSION_CONTROL.NAME",
                DescriptionKey = Prefix + "MISSION_CONTROL.DESC",
                TooltipKey = Prefix + "MISSION_CONTROL.TOOLTIP",
                PrimaryPrefabIds = new[]
                {
                    RoomBuildingCatalogue.MissionControl,
                    RoomBuildingCatalogue.MissionControlCluster
                },
                Additional = new[]
                {
                    RoomRequirement.NoOtherScienceBuilding(),
                    RoomRequirement.RequireLight(),
                    RoomRequirement.MinimumDecor(60f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(64)
                },
                Beats = new[] { "Laboratory" },
                Priority = 1,
                SortKey = 111
            },
            new RoomDefinition
            {
                Id = "MO_GeoTunerLab",
                Tier = RoomTier.Advanced,
                CategoryKey = "Science",
                NameKey = Prefix + "GEO_TUNER_LAB.NAME",
                DescriptionKey = Prefix + "GEO_TUNER_LAB.DESC",
                TooltipKey = Prefix + "GEO_TUNER_LAB.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.GeoTuner },
                Additional = new[]
                {
                    RoomRequirement.NoOtherScienceBuilding(),
                    RoomRequirement.RequireLight(),
                    RoomRequirement.MinimumDecor(60f),
                    RoomRequirement.NoIndustrialMachinery(),
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(64)
                },
                Beats = new[] { "Laboratory" },
                Priority = 1,
                SortKey = 112
            },
            new RoomDefinition
            {
                Id = "MO_Spaceport",
                Tier = RoomTier.Advanced,
                CategoryKey = "Industrial",
                NameKey = Prefix + "SPACEPORT.NAME",
                DescriptionKey = Prefix + "SPACEPORT.DESC",
                TooltipKey = Prefix + "SPACEPORT.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.LaunchPad },
                Additional = new[]
                {
                    RoomRequirement.MinimumSize(64),
                    RoomRequirement.MaximumSize(2400),
                    RoomRequirement.NoLifeBuilding()
                },
                RequiredDlcIds = new[] { DlcExpansion1 },
                Priority = 1,
                SortKey = 113
            },
            new RoomDefinition
            {
                Id = "MO_CargoStation",
                Tier = RoomTier.Advanced,
                CategoryKey = "Industrial",
                NameKey = Prefix + "CARGO_STATION.NAME",
                DescriptionKey = Prefix + "CARGO_STATION.DESC",
                TooltipKey = Prefix + "CARGO_STATION.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.RailGun },
                Additional = new[]
                {
                    RoomRequirement.RequireAllPrefabs(
                        RoomBuildingCatalogue.RailGunPayloadOpener,
                        RoomBuildingCatalogue.LogicInterasteroidSender,
                        RoomBuildingCatalogue.LogicInterasteroidReceiver),
                    RoomRequirement.MinimumSize(32),
                    RoomRequirement.MaximumSize(360)
                },
                RequiredDlcIds = new[] { DlcExpansion1 },
                Priority = 1,
                SortKey = 114
            },
            new RoomDefinition
            {
                Id = DataCenterId,
                Tier = RoomTier.Advanced,
                CategoryKey = "Science",
                NameKey = Prefix + "DATA_CENTER.NAME",
                DescriptionKey = Prefix + "DATA_CENTER.DESC",
                TooltipKey = Prefix + "DATA_CENTER.TOOLTIP",
                PrimaryPrefabIds = new[] { RoomBuildingCatalogue.DataMiner },
                Additional = new[]
                {
                    RoomRequirement.MinimumSize(12),
                    RoomRequirement.MaximumSize(96),
                    RoomRequirement.TemperatureBelow(20f)
                },
                RequiredDlcIds = new[] { Dlc3 },
                Beats = new[] { "Laboratory" },
                Priority = 1,
                SortKey = 115
            }
        };

        private static readonly RoomDefinition[] ModificationsValue =
        {
            new RoomDefinition
            {
                Id = "MO_Mod_PrivateBedroom",
                Tier = RoomTier.Advanced,
                ExistingRoomId = "Private Bedroom",
                Additional = new[]
                {
                    RoomRequirement.MinimumDecor(80f),
                    RoomRequirement.TemperatureRange(10f, 40f)
                },
                FallbackRoomIds = new[] { "Bedroom" },
                SortKey = 0
            },
            new RoomDefinition
            {
                Id = "MO_Mod_BanquetHall",
                Tier = RoomTier.Advanced,
                ExistingRoomId = "BanquetHall",
                Additional = new[]
                {
                    RoomRequirement.RequireLight(),
                    RoomRequirement.TemperatureRange(20f, 26f),
                    RoomRequirement.MinimumDecor(120f)
                },
                EffectIds = new[] { RoomEffectIds.BanquetRelief },
                FallbackRoomIds = new[] { "GreatHall", "MessHall" },
                SortKey = 0
            }
        };

        /// <summary>新增房间（不含改造项）。</summary>
        public static RoomDefinition[] NewRooms
        {
            get { return NewRoomsValue; }
        }

        /// <summary>对原版房间的附加条件改造。</summary>
        public static RoomDefinition[] Modifications
        {
            get { return ModificationsValue; }
        }

        /// <summary>按原版房间 id 找到改造项；没有则返回 null。</summary>
        public static RoomDefinition ModificationFor(string existingRoomId)
        {
            if (string.IsNullOrEmpty(existingRoomId))
            {
                return null;
            }

            for (int i = 0; i < ModificationsValue.Length; i++)
            {
                if (string.Equals(ModificationsValue[i].ExistingRoomId, existingRoomId, StringComparison.Ordinal))
                {
                    return ModificationsValue[i];
                }
            }

            return null;
        }

        public static int NewRoomCount
        {
            get { return NewRoomsValue.Length; }
        }

        public static int ModificationCount
        {
            get { return ModificationsValue.Length; }
        }
    }
}
