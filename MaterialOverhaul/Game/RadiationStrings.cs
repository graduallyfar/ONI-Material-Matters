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
                    public static class MANIFEST
                    {
                        public static LocString NAME = "显现放射：{0} rads/周期";
                        public static LocString TOOLTIP = "这个建筑累积的辐射已超过材料的显现限度，自身开始放射。\n\n当前自身放射：{0} rads/周期\n累积量：{1} rads（显现限度 {2} rads）\n累积量每 {3} 周期减半，降到限度以下就停止放射。附近的辐射粒子发生器会加快消耗它。";
                    }

                    public static class ACCUMULATING
                    {
                        public static LocString NAME = "辐射累积：{1} / {2} rads";
                        public static LocString TOOLTIP = "这个建筑正在累积受到的辐射。累积量超过材料的显现限度后，自身会开始放射。\n\n累积量：{1} rads（显现限度 {2} rads）\n离开辐射后，累积量每 {3} 周期减半。";
                    }
                }
            }
        }
    }
}
