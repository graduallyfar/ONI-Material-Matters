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

        public static string PrimaryName(RoomDefinition definition)
        {
            return string.Format(Criteria("PRIMARY"), RoomName(definition));
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
