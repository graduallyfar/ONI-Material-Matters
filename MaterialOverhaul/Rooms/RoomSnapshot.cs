using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 一个房间的纯数据快照：由游戏接入层从 <c>Room</c>、格子和建筑组件采样一次，
    /// 之后所有房间条件共用同一份数据，避免每个候选房间重复扫描格子和建筑。
    ///
    /// 平均温度／装饰度为 <see cref="float.NaN"/> 表示没有可用采样（例如全是真空格），
    /// 此时环境条件按「不满足」处理，不把异常值当成 0 自动通过。
    /// </summary>
    public sealed class RoomSnapshot
    {
        public int CellCount;

        public float AverageTemperatureK = float.NaN;

        public float AverageDecor = float.NaN;

        public int PlantCount;

        public int BuildingCount;

        public bool HasIndustrialMachinery;

        public bool HasIndustrialBuilding;

        public bool HasStorage;

        public bool HasLight;

        public bool HasToilet;

        public bool HasDiningTable;

        public bool HasDecoration;

        public bool HasOrnamentDisplayed;

        public bool HasMasterpiece;

        public bool HasLifeBuilding;

        /// <summary>房间内实际出现的 prefab id（建造完成后的建筑与植物）。</summary>
        public readonly HashSet<string> PrefabIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>房间内带「科学建筑」标签的 prefab id，用于「无其他科学建筑」判定。</summary>
        public readonly HashSet<string> SciencePrefabIds = new HashSet<string>(StringComparer.Ordinal);

        public bool HasPrefab(string prefabId)
        {
            return !string.IsNullOrEmpty(prefabId) && PrefabIds.Contains(prefabId);
        }
    }
}
