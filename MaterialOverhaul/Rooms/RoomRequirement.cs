using System;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 房间附加条件的种类。纯判定层只认这些种类与快照数据，
    /// 不引用 Unity／游戏对象，因此可以在离线测试里完整覆盖。
    /// </summary>
    public enum RoomRequirementKind
    {
        /// <summary>房间格数下限（含）。</summary>
        MinimumSize,

        /// <summary>房间格数上限（含）。</summary>
        MaximumSize,

        /// <summary>植物数量下限（不计分枝）。</summary>
        MinimumPlants,

        /// <summary>不得有带原版「工业机械」标签的建筑。</summary>
        NoIndustrialMachinery,

        /// <summary>至少一栋本 Mod 分类的「工业建筑」。</summary>
        RequireIndustrialBuilding,

        /// <summary>至少一个带 Storage 的存储设备。</summary>
        RequireStorage,

        /// <summary>至少一盏正在发光的光照设备。</summary>
        RequireLight,

        /// <summary>至少一个带原版 Decoration 标签的装饰建筑。</summary>
        RequireDecoration,

        /// <summary>至少一个正在展示藏品的基座。</summary>
        RequireOrnament,

        /// <summary>至少一件「杰出」品质的艺术品。</summary>
        RequireMasterpiece,

        /// <summary>房间平均装饰度下限（含）。</summary>
        MinimumDecor,

        /// <summary>房间平均温度区间（摄氏度，两端含）。</summary>
        TemperatureRange,

        /// <summary>房间平均温度严格低于给定摄氏度。</summary>
        TemperatureBelow,

        /// <summary>至少一个指定 prefab。</summary>
        RequirePrefab,

        /// <summary>指定 prefab 必须全部存在（每种至少一个）。</summary>
        RequireAllPrefabs,

        /// <summary>除本房间的专属设施外，不得有其它高级娱乐建筑。</summary>
        NoOtherAdvancedRecreation,

        /// <summary>除本房间的专属设施外，不得有其它科学建筑。</summary>
        NoOtherScienceBuilding,

        /// <summary>不得有生活建筑（床、厕所、餐桌、娱乐设施等）。</summary>
        NoLifeBuilding
    }

    /// <summary>
    /// 一条房间附加条件。数值与 prefab 名单在配置层给出，显示文本由游戏接入层按种类生成。
    /// </summary>
    public sealed class RoomRequirement
    {
        public RoomRequirementKind Kind;

        /// <summary>格数、数量等整数阈值。</summary>
        public int IntValue;

        /// <summary>装饰度、温度等浮点阈值；TemperatureBelow 时为上限摄氏度。</summary>
        public float FloatValue;

        /// <summary>TemperatureRange 的下限摄氏度。</summary>
        public float MinValue;

        /// <summary>TemperatureRange 的上限摄氏度。</summary>
        public float MaxValue;

        /// <summary>RequirePrefab 的 prefab id。</summary>
        public string PrefabId;

        /// <summary>RequireAllPrefabs 的 prefab id 列表。</summary>
        public string[] PrefabIds;

        public static RoomRequirement MinimumSize(int cells)
        {
            return new RoomRequirement { Kind = RoomRequirementKind.MinimumSize, IntValue = cells };
        }

        public static RoomRequirement MaximumSize(int cells)
        {
            return new RoomRequirement { Kind = RoomRequirementKind.MaximumSize, IntValue = cells };
        }

        public static RoomRequirement MinimumPlants(int count)
        {
            return new RoomRequirement { Kind = RoomRequirementKind.MinimumPlants, IntValue = count };
        }

        public static RoomRequirement NoIndustrialMachinery()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.NoIndustrialMachinery };
        }

        public static RoomRequirement RequireIndustrialBuilding()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequireIndustrialBuilding };
        }

        public static RoomRequirement RequireStorage()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequireStorage };
        }

        public static RoomRequirement RequireLight()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequireLight };
        }

        public static RoomRequirement RequireDecoration()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequireDecoration };
        }

        public static RoomRequirement RequireOrnament()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequireOrnament };
        }

        public static RoomRequirement RequireMasterpiece()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequireMasterpiece };
        }

        public static RoomRequirement MinimumDecor(float decor)
        {
            return new RoomRequirement { Kind = RoomRequirementKind.MinimumDecor, FloatValue = decor };
        }

        public static RoomRequirement TemperatureRange(float minCelsius, float maxCelsius)
        {
            return new RoomRequirement
            {
                Kind = RoomRequirementKind.TemperatureRange,
                MinValue = minCelsius,
                MaxValue = maxCelsius
            };
        }

        public static RoomRequirement TemperatureBelow(float maxCelsiusExclusive)
        {
            return new RoomRequirement
            {
                Kind = RoomRequirementKind.TemperatureBelow,
                FloatValue = maxCelsiusExclusive
            };
        }

        public static RoomRequirement RequirePrefab(string prefabId)
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequirePrefab, PrefabId = prefabId };
        }

        public static RoomRequirement RequireAllPrefabs(params string[] prefabIds)
        {
            return new RoomRequirement { Kind = RoomRequirementKind.RequireAllPrefabs, PrefabIds = prefabIds };
        }

        public static RoomRequirement NoOtherAdvancedRecreation()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.NoOtherAdvancedRecreation };
        }

        public static RoomRequirement NoOtherScienceBuilding()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.NoOtherScienceBuilding };
        }

        public static RoomRequirement NoLifeBuilding()
        {
            return new RoomRequirement { Kind = RoomRequirementKind.NoLifeBuilding };
        }

        /// <summary>是否依赖平均环境值（用于监控与重判）。</summary>
        public bool UsesEnvironment
        {
            get
            {
                return Kind == RoomRequirementKind.MinimumDecor
                    || Kind == RoomRequirementKind.TemperatureRange
                    || Kind == RoomRequirementKind.TemperatureBelow;
            }
        }
    }
}
