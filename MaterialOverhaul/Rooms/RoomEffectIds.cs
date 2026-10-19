namespace MaterialOverhaul.Rooms
{
    /// <summary>
    /// 已实装房间效果的稳定 id。定义在纯配置层，游戏接入层据此创建 <c>Klei.AI.Effect</c>
    /// 并挂到 <c>RoomType.effects</c>；只列出真正会发放的效果，未接入项不占位。
    /// </summary>
    public static class RoomEffectIds
    {
        public const string ArtificialParkMorale = "MO_RoomArtificialParkMorale";
        public const string ShowerRelief = "MO_RoomShowerRelief";
        public const string BanquetRelief = "MO_RoomBanquetRelief";
        public const string DanceHallMorale = "MO_RoomDanceHallMorale";
        public const string ArcadeHallMorale = "MO_RoomArcadeHallMorale";
        public const string BeachParadiseMorale = "MO_RoomBeachParadiseMorale";
        public const string GalleryMorale = "MO_RoomGalleryMorale";
        public const string WindTunnelMorale = "MO_RoomWindTunnelMorale";
        public const string SpaMorale = "MO_RoomSpaMorale";
        public const string TeaRoomMorale = "MO_RoomTeaRoomMorale";
    }
}
