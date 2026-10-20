using System;
using System.Collections.Generic;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 房间条件判定的纯逻辑入口。输入 <see cref="RoomSnapshot"/>，不读取 Unity 全局状态，
    /// 因此可以用离线测试完整覆盖构成、环境与升级／冲突规则。
    /// </summary>
    public static class RoomEvaluator
    {
        /// <summary>
        /// 保持回差：房间已经是该类型时，平均装饰度下限放宽的点数（2026-10-20 指定的工作初值）。
        /// 只用于「保持」，新判定仍按原阈值，避免环境值在阈值附近波动时房间来回切换。
        /// </summary>
        public const float DecorRetainMargin = 5f;

        /// <summary>保持回差：房间已经是该类型时，温度条件两端各放宽的摄氏度（K 差相同）。</summary>
        public const float TemperatureRetainMargin = 1f;

        /// <summary>主设施是否满足：房间内出现任意一种专属设施。没有专属设施的定义视为满足。</summary>
        public static bool MatchesPrimary(RoomDefinition definition, RoomSnapshot snapshot)
        {
            if (definition == null || snapshot == null)
            {
                return false;
            }

            if (definition.PrimaryPrefabIds == null || definition.PrimaryPrefabIds.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < definition.PrimaryPrefabIds.Length; i++)
            {
                if (snapshot.HasPrefab(definition.PrimaryPrefabIds[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>单个附加条件是否满足。缺少采样（NaN）的环境值按不满足处理。</summary>
        public static bool IsSatisfied(RoomRequirement requirement, RoomSnapshot snapshot)
        {
            return IsSatisfied(null, requirement, snapshot);
        }

        /// <summary>
        /// 单个附加条件是否满足；传入定义以排除本房间自己的专属设施
        /// （「无其他高级娱乐建筑」「无其他科学建筑」需要把专属设施本身排除）。
        /// </summary>
        public static bool IsSatisfied(RoomDefinition definition, RoomRequirement requirement, RoomSnapshot snapshot)
        {
            return IsSatisfied(definition, requirement, snapshot, false);
        }

        /// <summary>
        /// 单个附加条件是否满足；<paramref name="retain"/> 为 true 表示房间当前已是该定义，
        /// 环境条件按 <see cref="DecorRetainMargin"/>、<see cref="TemperatureRetainMargin"/> 放宽。
        /// </summary>
        public static bool IsSatisfied(RoomDefinition definition, RoomRequirement requirement, RoomSnapshot snapshot, bool retain)
        {
            float decorMargin = retain ? DecorRetainMargin : 0f;
            float temperatureMargin = retain ? TemperatureRetainMargin : 0f;
            if (requirement == null || snapshot == null)
            {
                return false;
            }

            switch (requirement.Kind)
            {
                case RoomRequirementKind.MinimumSize:
                    return snapshot.CellCount >= requirement.IntValue;
                case RoomRequirementKind.MaximumSize:
                    return snapshot.CellCount <= requirement.IntValue;
                case RoomRequirementKind.MinimumPlants:
                    return snapshot.PlantCount >= requirement.IntValue;
                case RoomRequirementKind.NoIndustrialMachinery:
                    return !snapshot.HasIndustrialMachinery;
                case RoomRequirementKind.RequireIndustrialBuilding:
                    return snapshot.HasIndustrialBuilding;
                case RoomRequirementKind.RequireStorage:
                    return snapshot.HasStorage;
                case RoomRequirementKind.RequireLight:
                    return snapshot.HasLight;
                case RoomRequirementKind.RequireToilet:
                    return snapshot.HasToilet;
                case RoomRequirementKind.RequireDiningTable:
                    return snapshot.HasDiningTable;
                case RoomRequirementKind.RequireDecoration:
                    return snapshot.HasDecoration;
                case RoomRequirementKind.RequireOrnament:
                    return snapshot.HasOrnamentDisplayed;
                case RoomRequirementKind.RequireMasterpiece:
                    return snapshot.HasMasterpiece;
                case RoomRequirementKind.MinimumDecor:
                    return !float.IsNaN(snapshot.AverageDecor)
                        && snapshot.AverageDecor >= requirement.FloatValue - decorMargin;
                case RoomRequirementKind.TemperatureRange:
                    return InRange(
                        snapshot.AverageTemperatureK,
                        Temperatures.FromCelsius(requirement.MinValue) - temperatureMargin,
                        Temperatures.FromCelsius(requirement.MaxValue) + temperatureMargin);
                case RoomRequirementKind.TemperatureBelow:
                    return !float.IsNaN(snapshot.AverageTemperatureK)
                        && snapshot.AverageTemperatureK < Temperatures.FromCelsius(requirement.FloatValue) + temperatureMargin;
                case RoomRequirementKind.RequirePrefab:
                    return snapshot.HasPrefab(requirement.PrefabId);
                case RoomRequirementKind.RequireAllPrefabs:
                    return HasAllPrefabs(snapshot, requirement.PrefabIds);
                case RoomRequirementKind.NoOtherAdvancedRecreation:
                    return CountAdvancedRecreation(snapshot, definition) == 0;
                case RoomRequirementKind.NoOtherScienceBuilding:
                    return CountScienceBuildings(snapshot, definition) == 0;
                case RoomRequirementKind.NoLifeBuilding:
                    return !snapshot.HasLifeBuilding;
                default:
                    return false;
            }
        }

        /// <summary>主设施与全部附加条件都满足。</summary>
        public static bool IsSatisfied(RoomDefinition definition, RoomSnapshot snapshot)
        {
            return IsSatisfied(definition, snapshot, false);
        }

        /// <summary>主设施与全部附加条件都满足；<paramref name="retain"/> 见单条件重载。</summary>
        public static bool IsSatisfied(RoomDefinition definition, RoomSnapshot snapshot, bool retain)
        {
            return definition != null
                && snapshot != null
                && MatchesPrimary(definition, snapshot)
                && Missing(definition, snapshot, retain).Count == 0;
        }

        /// <summary>返回全部未满足的附加条件；调用方负责显示与日志。</summary>
        public static List<RoomRequirement> Missing(RoomDefinition definition, RoomSnapshot snapshot)
        {
            return Missing(definition, snapshot, false);
        }

        /// <summary>返回全部未满足的附加条件；<paramref name="retain"/> 见单条件重载。</summary>
        public static List<RoomRequirement> Missing(RoomDefinition definition, RoomSnapshot snapshot, bool retain)
        {
            List<RoomRequirement> missing = new List<RoomRequirement>();
            if (definition == null || snapshot == null || definition.Additional == null)
            {
                return missing;
            }

            for (int i = 0; i < definition.Additional.Length; i++)
            {
                if (!IsSatisfied(definition, definition.Additional[i], snapshot, retain))
                {
                    missing.Add(definition.Additional[i]);
                }
            }

            return missing;
        }

        /// <summary>
        /// 从候选定义中选出应生效的房间。
        ///
        /// 规则（对应开发计划 D02 的当前实现口径，确认后回写分设计）：
        /// 1. 只考虑主设施与全部附加条件都满足的定义；
        /// 2. 若当前已有房间 id 属于本 Mod，且按保持回差仍然满足，保持不换；
        /// 3. 新房间只能覆盖 <see cref="RoomDefinition.Beats"/> 明列的原版房间；
        ///    未列出的原版房间保持原版结果，例如同时有厕所与淋浴间时仍是卫生间；
        /// 4. 多个可覆盖候选按 Priority、SortKey 取最高。
        /// </summary>
        public static RoomDefinition Choose(
            IReadOnlyList<RoomDefinition> definitions,
            RoomSnapshot snapshot,
            string currentRoomId)
        {
            if (definitions == null || snapshot == null)
            {
                return null;
            }

            RoomDefinition current = FindById(definitions, currentRoomId);
            if (current != null && IsSatisfied(current, snapshot, true))
            {
                return current;
            }

            RoomDefinition best = null;
            for (int i = 0; i < definitions.Count; i++)
            {
                RoomDefinition candidate = definitions[i];
                if (candidate.IsModification || !IsSatisfied(candidate, snapshot))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(currentRoomId)
                    && currentRoomId != "Neutral"
                    && !Beats(candidate, currentRoomId))
                {
                    continue;
                }

                if (best == null || Beats_Order(candidate, best) > 0)
                {
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>候选是否明确优先于给定原版房间 id。</summary>
        public static bool Beats(RoomDefinition definition, string originalRoomId)
        {
            if (definition == null || definition.Beats == null || string.IsNullOrEmpty(originalRoomId))
            {
                return false;
            }

            for (int i = 0; i < definition.Beats.Length; i++)
            {
                if (string.Equals(definition.Beats[i], originalRoomId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static RoomDefinition FindById(IReadOnlyList<RoomDefinition> definitions, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (string.Equals(definitions[i].Id, id, StringComparison.Ordinal))
                {
                    return definitions[i];
                }
            }

            return null;
        }

        private static int Beats_Order(RoomDefinition left, RoomDefinition right)
        {
            int priority = left.Priority.CompareTo(right.Priority);
            if (priority != 0)
            {
                return priority;
            }

            return left.SortKey.CompareTo(right.SortKey);
        }

        private static bool InRange(float kelvin, float minKelvin, float maxKelvin)
        {
            return !float.IsNaN(kelvin) && kelvin >= minKelvin && kelvin <= maxKelvin;
        }

        private static bool HasAllPrefabs(RoomSnapshot snapshot, string[] prefabIds)
        {
            if (prefabIds == null)
            {
                return true;
            }

            for (int i = 0; i < prefabIds.Length; i++)
            {
                if (!snapshot.HasPrefab(prefabIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountAdvancedRecreation(RoomSnapshot snapshot, RoomDefinition definition)
        {
            int count = 0;
            foreach (string prefabId in snapshot.PrefabIds)
            {
                if (RoomBuildingCatalogue.IsAdvancedRecreation(prefabId) && !IsPrimary(definition, prefabId))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountScienceBuildings(RoomSnapshot snapshot, RoomDefinition definition)
        {
            int count = 0;
            foreach (string prefabId in snapshot.SciencePrefabIds)
            {
                if (!IsPrimary(definition, prefabId))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsPrimary(RoomDefinition definition, string prefabId)
        {
            if (definition == null || definition.PrimaryPrefabIds == null)
            {
                return false;
            }

            for (int i = 0; i < definition.PrimaryPrefabIds.Length; i++)
            {
                if (string.Equals(definition.PrimaryPrefabIds[i], prefabId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
