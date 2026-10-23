namespace MaterialOverhaul.Core
{
    /// <summary>构建时固定的系统身份；独立版中另一系统的代码不会进入程序集。</summary>
    public static class BuildProfile
    {
#if MATERIAL_OVERHAUL_POWER_ONLY
        public const string Name = "Power";
        public const string DisplayName = "电力独立验证版";
        public const bool IndustrialEnabled = false;
        public const bool PowerEnabled = true;
        public const bool RoomEnabled = false;
        public const bool RadiationEnabled = false;
#elif MATERIAL_OVERHAUL_INDUSTRIAL_ONLY
        public const string Name = "Industrial";
        public const string DisplayName = "工业独立验证版";
        public const bool IndustrialEnabled = true;
        public const bool PowerEnabled = false;
        public const bool RoomEnabled = false;
        public const bool RadiationEnabled = false;
#elif MATERIAL_OVERHAUL_ROOM_ONLY
        public const string Name = "Room";
        public const string DisplayName = "房间独立验证版";
        public const bool IndustrialEnabled = false;
        public const bool PowerEnabled = false;
        public const bool RoomEnabled = true;
        public const bool RadiationEnabled = false;
#elif MATERIAL_OVERHAUL_RADIATION_ONLY
        public const string Name = "Radiation";
        public const string DisplayName = "辐射独立验证版";
        public const bool IndustrialEnabled = false;
        public const bool PowerEnabled = false;
        public const bool RoomEnabled = false;
        public const bool RadiationEnabled = true;
#else
        public const string Name = "Combined";
        public const string DisplayName = "V1.0.3 整合版（工业／电力／房间／辐射）";
        public const bool IndustrialEnabled = true;
        public const bool PowerEnabled = true;
        public const bool RoomEnabled = true;
        public const bool RadiationEnabled = true;
#endif
    }
}
