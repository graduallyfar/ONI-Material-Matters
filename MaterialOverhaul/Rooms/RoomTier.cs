namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 房间等级（房间系统设计 §4.1）。T1 保持原版；本 Mod 只改造 T2、T3。
    /// 等级只用于开发与显示归类，不直接参与原版房间判定。
    /// </summary>
    public enum RoomTier
    {
        /// <summary>T1 基础房间：公共厕所、卫生间、宿舍、食堂。保持原版。</summary>
        Basic = 1,

        /// <summary>T2 中级房间：医院、淋浴间、人造公园等。</summary>
        Improved = 2,

        /// <summary>T3 高级房间：带最低装饰度与部分温度要求。</summary>
        Advanced = 3
    }
}
