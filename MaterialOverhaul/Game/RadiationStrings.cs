namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 辐射系统的本地化键树。键名前缀为 <c>STRINGS.RADIATION.MATERIALOVERHAUL</c>，
    /// 由 <c>LocString.CreateLocStringKeys(typeof(RadiationStrings.RADIATION), "STRINGS.")</c> 建立。
    /// 字段默认值直接给中文：即使没有 .po，界面也能显示正确文本。
    /// </summary>
    public static class RadiationStrings
    {
        public static class RADIATION
        {
            public static class MATERIALOVERHAUL
            {
                public static class STATUS
                {
                    public static class INDUCED
                    {
                        public static LocString NAME = "感生放射：{0} rads/周期";
                        public static LocString TOOLTIP = "这个建筑长期处在强辐射中，自身已带有放射性。\n\n当前自身放射：{0} rads/周期\n离开辐射源后，放射强度每 {1} 周期减半。";
                    }
                }
            }
        }
    }
}
