namespace MaterialOverhaul.Core
{
    /// <summary>
    /// Mod 身份与版本。标识符集中在此，补丁与日志一律引用这里的常量。
    /// 注意：staticID 必须与 mod/mod.yaml 中的 staticID 一致。
    /// </summary>
    public static class ModInfo
    {
        public const string StaticID = "MaterialOverhaul";
        public const string DisplayName = "Material Matters";
        // 须与 MaterialOverhaul.csproj 中各 SystemProfile 的 Version 一致（ProfileChecks 核对）。
#if MATERIAL_OVERHAUL_POWER_ONLY
        public const string Version = "0.0.2";
#elif MATERIAL_OVERHAUL_RADIATION_ONLY
        public const string Version = "0.0.1";
#elif MATERIAL_OVERHAUL_INDUSTRIAL_ONLY || MATERIAL_OVERHAUL_ROOM_ONLY
        public const string Version = "0.2.2";
#else
        public const string Version = "1.0.3";
#endif

        /// <summary>Harmony 实例标识，用于按 Mod 归属补丁。</summary>
        public const string HarmonyID = StaticID;
    }
}
