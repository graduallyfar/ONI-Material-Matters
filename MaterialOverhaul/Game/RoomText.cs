using System;
using System.Text;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 房间显示文本。把纯判定层的条件定义翻译成原版 <c>Constraint.name</c>／
    /// <c>RoomDetails.Detail</c> 可用的字符串；数值格式与分设计一致。
    /// </summary>
    public static class RoomText
    {
        private const string CriteriaPrefix = "STRINGS.ROOMS.MATERIALOVERHAUL.CRITERIA.";

        public static string RoomName(RoomDefinition definition)
        {
            return definition == null ? string.Empty : Strings.Get(definition.NameKey);
        }

        public static string RoomDescription(RoomDefinition definition)
        {
            return definition == null ? string.Empty : Strings.Get(definition.DescriptionKey);
        }

        public static string RoomTooltip(RoomDefinition definition)
        {
            return definition == null ? string.Empty : Strings.Get(definition.TooltipKey);
        }

        /// <summary>房间概览图例与悬停卡第一行的简述（原版 <c>RoomType.effect</c>，如「- 士气加成」）。</summary>
        public static string RoomEffect(RoomDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.NameKey) || !definition.NameKey.EndsWith(".NAME", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string key = definition.NameKey.Substring(0, definition.NameKey.Length - ".NAME".Length) + ".EFFECT";
            StringEntry entry;
            return Strings.TryGet(new StringKey(key), out entry) && entry != null ? entry.ToString() : string.Empty;
        }

        /// <summary>主条件显示专属设施的建筑名（同名的本体／群星版本只写一次），而不是房间名。</summary>
        public static string PrimaryName(RoomDefinition definition)
        {
            string[] ids = definition == null ? null : definition.PrimaryPrefabIds;
            StringBuilder names = new StringBuilder();
            System.Collections.Generic.HashSet<string> seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; ids != null && i < ids.Length; i++)
            {
                string name = PrefabName(ids[i]);
                if (!seen.Add(STRINGS.UI.StripLinkFormatting(name)))
                {
                    continue;
                }

                if (names.Length > 0)
                {
                    names.Append(Criteria("PRIMARY_OR"));
                }

                names.Append(name);
            }

            return string.Format(Criteria("PRIMARY"), names.Length > 0 ? names.ToString() : RoomName(definition));
        }

        /// <summary>条件显示文本。未识别的种类直接显示内部标识，便于定位而不是静默留空。</summary>
        public static string RequirementName(RoomDefinition definition, RoomRequirement requirement)
        {
            if (requirement == null)
            {
                return string.Empty;
            }

            switch (requirement.Kind)
            {
                case RoomRequirementKind.MinimumSize:
                    return string.Format(Criteria("MIN_SIZE"), requirement.IntValue);
                case RoomRequirementKind.MaximumSize:
                    return string.Format(Criteria("MAX_SIZE"), requirement.IntValue);
                case RoomRequirementKind.MinimumPlants:
                    return string.Format(Criteria("MIN_PLANTS"), requirement.IntValue);
                case RoomRequirementKind.NoIndustrialMachinery:
                    return Criteria("NO_INDUSTRIAL_MACHINERY");
                case RoomRequirementKind.RequireIndustrialBuilding:
                    return Criteria("INDUSTRIAL_BUILDING");
                case RoomRequirementKind.RequireStorage:
                    return Criteria("STORAGE");
                case RoomRequirementKind.RequireLight:
                    return Criteria("LIGHT");
                case RoomRequirementKind.RequireToilet:
                    return Criteria("TOILET");
                case RoomRequirementKind.RequireDiningTable:
                    return Criteria("DINING_TABLE");
                case RoomRequirementKind.RequireDecoration:
                    return Criteria("DECORATION");
                case RoomRequirementKind.RequireOrnament:
                    return Criteria("ORNAMENT");
                case RoomRequirementKind.RequireMasterpiece:
                    return Criteria("MASTERPIECE");
                case RoomRequirementKind.MinimumDecor:
                    return string.Format(Criteria("MIN_DECOR"), FormatDecor(requirement.FloatValue));
                case RoomRequirementKind.TemperatureRange:
                    return string.Format(
                        Criteria("TEMP_RANGE"),
                        FormatCelsius(requirement.MinValue),
                        FormatCelsius(requirement.MaxValue));
                case RoomRequirementKind.TemperatureBelow:
                    return string.Format(Criteria("TEMP_BELOW"), FormatCelsius(requirement.FloatValue));
                case RoomRequirementKind.RequirePrefab:
                    return string.Format(Criteria("PREFAB"), PrefabName(requirement.PrefabId));
                case RoomRequirementKind.RequireAllPrefabs:
                    return string.Format(Criteria("ALL_PREFABS"), JoinPrefabNames(requirement.PrefabIds));
                case RoomRequirementKind.NoOtherAdvancedRecreation:
                    return Criteria("NO_OTHER_ADVANCED_RECREATION");
                case RoomRequirementKind.NoOtherScienceBuilding:
                    return Criteria("NO_OTHER_SCIENCE");
                case RoomRequirementKind.NoLifeBuilding:
                    return Criteria("NO_LIFE_BUILDING");
                default:
                    return requirement.Kind.ToString();
            }
        }

        /// <summary>按 prefab id 取原版建筑名；键不存在时退回 id，便于定位。</summary>
        public static string PrefabName(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
            {
                return string.Empty;
            }

            StringEntry entry;
            string key = "STRINGS.BUILDINGS.PREFABS." + prefabId.ToUpperInvariant() + ".NAME";
            if (Strings.TryGet(new StringKey(key), out entry) && entry != null)
            {
                string text = entry.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return prefabId;
        }

        public static string AverageDecorLine(Room room)
        {
            RoomSnapshot snapshot = RoomSnapshotCache.Get(room);
            string value = float.IsNaN(snapshot.AverageDecor) ? "—" : FormatDecor(snapshot.AverageDecor);
            return string.Format(Strings.Get("STRINGS.ROOMS.MATERIALOVERHAUL.DETAILS.AVERAGE_DECOR"), value);
        }

        public static string AverageTemperatureLine(Room room)
        {
            RoomSnapshot snapshot = RoomSnapshotCache.Get(room);
            string value = float.IsNaN(snapshot.AverageTemperatureK)
                ? "—"
                : GameUtil.GetFormattedTemperature(snapshot.AverageTemperatureK);
            return string.Format(Strings.Get("STRINGS.ROOMS.MATERIALOVERHAUL.DETAILS.AVERAGE_TEMPERATURE"), value);
        }

        /// <summary>工业区／数据机房的生产加成显示行；其它房间返回空字符串。</summary>
        public static string ProductionLine(Room room)
        {
            if (room == null || room.roomType == null)
            {
                return string.Empty;
            }

            float multiplier = RoomProduction.MultiplierForRoom(room.roomType.Id);
            if (multiplier <= 1f)
            {
                return string.Empty;
            }

            return string.Format(
                Strings.Get("STRINGS.ROOMS.MATERIALOVERHAUL.DETAILS.PRODUCTION_BONUS"),
                ((multiplier - 1f) * 100f).ToString("0"));
        }

        /// <summary>对应偏好特质的显示行，例如「偏好特质：海滨达人、冒险家（…）」；没有对应特质的房间返回空字符串。</summary>
        public static string PreferenceTraitLine(string roomTypeId)
        {
            RoomTraitSpec[] specs = RoomTraitCatalogue.ForRoom(roomTypeId);
            if (specs.Length == 0)
            {
                return string.Empty;
            }

            var names = new string[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                names[i] = Strings.Get("STRINGS.ROOMS.MATERIALOVERHAUL.TRAITS." + specs[i].StringToken + ".NAME");
            }

            return string.Format(
                Strings.Get("STRINGS.ROOMS.MATERIALOVERHAUL.DETAILS.PREFERENCE_TRAITS"),
                string.Join("、", names));
        }

        /// <summary>具有专项效果的房间（科研、航天、货运、医疗中心）是否显示专项说明行。</summary>
        public static bool HasSpecialEffectLine(string roomTypeId)
        {
            return roomTypeId == RoomCatalogue.MissionControlId
                || roomTypeId == RoomCatalogue.GeoTunerLabId
                || roomTypeId == RoomCatalogue.SpaceportId
                || roomTypeId == RoomCatalogue.CargoStationId
                || roomTypeId == RoomCatalogue.MedicalCenterId;
        }

        /// <summary>专项效果说明行，数值取自 <see cref="RoomAdvancedEffects"/>；其它房间返回空字符串。</summary>
        public static string SpecialEffectLine(Room room)
        {
            string id = room == null || room.roomType == null ? null : room.roomType.Id;
            const string prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.DETAILS.";
            if (id == RoomCatalogue.MissionControlId)
            {
                string text = string.Format(
                    Strings.Get(prefix + "MISSION_CONTROL_BONUS"),
                    Percent(RoomAdvancedEffects.MissionControlSpeedBonus));
                return HasMissionControlRange()
                    ? text + string.Format(Strings.Get(prefix + "MISSION_CONTROL_RANGE"), RoomAdvancedEffects.MissionControlRangeBonus)
                    : text;
            }

            if (id == RoomCatalogue.GeoTunerLabId)
            {
                return string.Format(
                    Strings.Get(prefix + "GEOTUNER_LAB_BONUS"),
                    Factor(RoomAdvancedEffects.GeoTunerLabMassBonus),
                    RoomAdvancedEffects.GeoTunerLabExtraTuners);
            }

            if (id == RoomCatalogue.SpaceportId)
            {
                return string.Format(
                    Strings.Get(prefix + "SPACEPORT_BONUS"),
                    Percent(RoomAdvancedEffects.SpaceportSpeedBonus),
                    Percent(1f - RoomAdvancedEffects.SpaceportFuelFactor));
            }

            if (id == RoomCatalogue.CargoStationId)
            {
                return string.Format(
                    Strings.Get(prefix + "CARGO_STATION_BONUS"),
                    Percent(RoomAdvancedEffects.CargoPayloadSpeedFactor - 1f),
                    RoomAdvancedEffects.VanillaLaunchesPerCooldown + RoomAdvancedEffects.CargoExtraLaunches,
                    Percent(RoomAdvancedEffects.CargoCooldownSpeedFactor - 1f));
            }

            if (id == RoomCatalogue.MedicalCenterId)
            {
                return Strings.Get(prefix + "MEDICAL_CENTER_BONUS");
            }

            return string.Empty;
        }

        /// <summary>
        /// 位于该房间的建筑获得的公开加成，按显示顺序列出；没有加成返回空数组。
        /// 科学翻倍等隐藏机制不写入。矿石洗涤器能构成工业区，但清洗速度不受加成，不显示。
        /// </summary>
        public static string[] BuildingBonusLines(string roomTypeId, string prefabId)
        {
            const string prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.STATUS.";
            if (string.IsNullOrEmpty(roomTypeId) || string.IsNullOrEmpty(prefabId))
            {
                return new string[0];
            }

            float multiplier = RoomProduction.Multiplier(roomTypeId, prefabId);
            if (roomTypeId == RoomCatalogue.MissionControlId && multiplier > 1f)
            {
                string boost = string.Format(Strings.Get(prefix + "MISSION_CONTROL"), Percent(RoomAdvancedEffects.MissionControlSpeedBonus));
                return HasMissionControlRange()
                    ? new[]
                    {
                        string.Format(Strings.Get(prefix + "WORK"), Percent(multiplier - 1f)),
                        boost,
                        string.Format(Strings.Get(prefix + "MISSION_CONTROL_RANGE"), RoomAdvancedEffects.MissionControlRangeBonus)
                    }
                    : new[] { string.Format(Strings.Get(prefix + "WORK"), Percent(multiplier - 1f)), boost };
            }

            if (roomTypeId == RoomCatalogue.GeoTunerLabId && multiplier > 1f)
            {
                return new[]
                {
                    string.Format(Strings.Get(prefix + "WORK"), Percent(multiplier - 1f)),
                    string.Format(Strings.Get(prefix + "GEOTUNER"),
                        Factor(RoomAdvancedEffects.GeoTunerLabMassBonus), RoomAdvancedEffects.GeoTunerLabExtraTuners)
                };
            }

            if (multiplier > 1f && prefabId != "OreScrubber")
            {
                return new[] { string.Format(Strings.Get(prefix + "PRODUCTION"), Percent(multiplier - 1f)) };
            }

            if (RoomAdvancedEffects.IsSpaceport(roomTypeId) && prefabId == RoomBuildingCatalogue.LaunchPad)
            {
                return new[]
                {
                    string.Format(Strings.Get(prefix + "SPACEPORT"),
                        Percent(RoomAdvancedEffects.SpaceportSpeedBonus), Percent(1f - RoomAdvancedEffects.SpaceportFuelFactor))
                };
            }

            if (RoomAdvancedEffects.IsCargoStation(roomTypeId) && prefabId == RoomBuildingCatalogue.RailGun)
            {
                return new[]
                {
                    string.Format(Strings.Get(prefix + "CARGO"),
                        Percent(RoomAdvancedEffects.CargoPayloadSpeedFactor - 1f),
                        RoomAdvancedEffects.VanillaLaunchesPerCooldown + RoomAdvancedEffects.CargoExtraLaunches,
                        Percent(RoomAdvancedEffects.CargoCooldownSpeedFactor - 1f))
                };
            }

            string recovery = prefabId == RoomBuildingCatalogue.MedicalCot ? RoomRecoveryEffect.ForRoomType(roomTypeId) : null;
            RoomEffectSpec spec = recovery == null ? null : RoomEffectCatalogue.Get(recovery);
            if (spec != null && spec.HealthRecoveryPercent > 0f)
            {
                return new[] { string.Format(Strings.Get(prefix + "RECOVERY"), spec.HealthRecoveryPercent.ToString("0")) };
            }

            return new string[0];
        }

        /// <summary>火箭本次飞行获得的房间加成说明；没有加成返回空字符串。</summary>
        public static string RocketBonusTooltipLines(float missionControlBonus, bool fromSpaceport)
        {
            const string prefix = "STRINGS.ROOMS.MATERIALOVERHAUL.STATUS.";
            StringBuilder builder = new StringBuilder();
            // 按触发先后列出：发射场加成在起飞时，指挥站助推在飞行中（V1.0.2 #22）。
            if (fromSpaceport)
            {
                builder.Append(string.Format(Strings.Get(prefix + "ROCKET_SPACEPORT"),
                    Percent(RoomAdvancedEffects.SpaceportSpeedBonus), Percent(1f - RoomAdvancedEffects.SpaceportFuelFactor)));
            }

            if (missionControlBonus > 0f)
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(string.Format(Strings.Get(prefix + "ROCKET_MISSION_CONTROL"), Percent(missionControlBonus)));
            }

            return builder.ToString();
        }

        public static string Percent(float fraction)
        {
            return (fraction * 100f).ToString("0");
        }

        /// <summary>最后结算的乘数写法，如加成 0.1 显示为「1.1」。</summary>
        public static string Factor(float bonus)
        {
            return (1f + Math.Max(bonus, 0f)).ToString("0.##");
        }

        /// <summary>指挥范围只在群星版按距离筛选火箭；本体版不显示这一项。</summary>
        private static bool HasMissionControlRange()
        {
            return DlcManager.IsExpansion1Active();
        }

        private static string Criteria(string fieldName)
        {
            return Strings.Get(CriteriaPrefix + fieldName);
        }

        private static string JoinPrefabNames(string[] prefabIds)
        {
            if (prefabIds == null || prefabIds.Length == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < prefabIds.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("、");
                }

                builder.Append(PrefabName(prefabIds[i]));
            }

            return builder.ToString();
        }

        private static string FormatDecor(float value)
        {
            return (value >= 0f ? "+" : string.Empty) + value.ToString("0");
        }

        private static string FormatCelsius(float value)
        {
            return value.ToString("0");
        }
    }
}
