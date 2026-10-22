namespace MaterialOverhaul.Core
{
    /// <summary>
    /// Mod 身份与版本。标识符集中在此，补丁与日志一律引用这里的常量。
    /// 注意：staticID 必须与 mod/mod.yaml 中的 staticID 一致。
    /// </summary>
    public static class ModInfo
    {
        public const string StaticID = "MaterialOverhaul";
        public const string DisplayName = "ONI Reform · 工程重构";
#if MATERIAL_OVERHAUL_POWER_ONLY
        public const string Version = "0.0.2";
#else
        public const string Version = "0.2.2";
#endif

        /// <summary>Harmony 实例标识，用于按 Mod 归属补丁。</summary>
        public const string HarmonyID = StaticID;
    }
}
