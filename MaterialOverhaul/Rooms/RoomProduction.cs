namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 房间生产效率加成的纯判定。
    ///
    /// 设计值：工业区与数据机房各 +20%；任务控制中心、地热调谐实验室 +20%（2026-10-20 指定的工作初值）。
    /// - 工业区只作用于 <see cref="RoomBuildingCatalogue"/> 工业建筑名单内的建筑；
    /// - 数据机房只作用于数据挖掘中心；
    /// - 任务控制中心只作用于航天指挥站（两种），地热调谐实验室只作用于地质调谐仪，按工作速度结算；
    /// - 批次建筑通过工作速度／无人进度结算，连续转换设备按速度倍率结算；
    /// - 不改变配方投入产出比例、能耗、暂停、缺料、断电或损坏条件。
    /// </summary>
    public static class RoomProduction
    {
        public const float IndustrialAreaMultiplier = 1.2f;

        public const float DataCenterMultiplier = 1.2f;

        public const float MissionControlMultiplier = 1.2f;

        public const float GeoTunerLabMultiplier = 1.2f;

        /// <summary>按房间类型返回展示用加成倍率；不是生产加成房间时返回 1。</summary>
        public static float MultiplierForRoom(string roomTypeId)
        {
            if (string.Equals(roomTypeId, RoomCatalogue.IndustrialAreaId, System.StringComparison.Ordinal))
            {
                return IndustrialAreaMultiplier;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.DataCenterId, System.StringComparison.Ordinal))
            {
                return DataCenterMultiplier;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.MissionControlId, System.StringComparison.Ordinal))
            {
                return MissionControlMultiplier;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.GeoTunerLabId, System.StringComparison.Ordinal))
            {
                return GeoTunerLabMultiplier;
            }

            return 1f;
        }

        /// <summary>返回该建筑在当前房间类型下的生产倍率；不适用时为 1。</summary>
        public static float Multiplier(string roomTypeId, string prefabId)
        {
            if (string.IsNullOrEmpty(roomTypeId) || string.IsNullOrEmpty(prefabId))
            {
                return 1f;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.IndustrialAreaId, System.StringComparison.Ordinal)
                && RoomBuildingCatalogue.IsIndustrialBuilding(prefabId))
            {
                return IndustrialAreaMultiplier;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.DataCenterId, System.StringComparison.Ordinal)
                && string.Equals(prefabId, RoomBuildingCatalogue.DataMiner, System.StringComparison.Ordinal))
            {
                return DataCenterMultiplier;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.MissionControlId, System.StringComparison.Ordinal)
                && (string.Equals(prefabId, RoomBuildingCatalogue.MissionControl, System.StringComparison.Ordinal)
                    || string.Equals(prefabId, RoomBuildingCatalogue.MissionControlCluster, System.StringComparison.Ordinal)))
            {
                return MissionControlMultiplier;
            }

            if (string.Equals(roomTypeId, RoomCatalogue.GeoTunerLabId, System.StringComparison.Ordinal)
                && string.Equals(prefabId, RoomBuildingCatalogue.GeoTuner, System.StringComparison.Ordinal))
            {
                return GeoTunerLabMultiplier;
            }

            return 1f;
        }
    }
}
