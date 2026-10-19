namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 房间生产效率加成的纯判定。
    ///
    /// 设计值：工业区与数据机房各 +20%。工作口径（D09 未确认前）：
    /// - 工业区只作用于 <see cref="RoomBuildingCatalogue"/> 工业建筑名单内的建筑；
    /// - 数据机房只作用于数据挖掘中心；
    /// - 批次建筑通过工作速度／无人进度结算，连续转换设备尚未接入；
    /// - 不改变配方投入产出比例、能耗、暂停、缺料、断电或损坏条件。
    /// </summary>
    public static class RoomProduction
    {
        public const float IndustrialAreaMultiplier = 1.2f;

        public const float DataCenterMultiplier = 1.2f;

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

            return 1f;
        }
    }
}
