namespace MaterialOverhaul.Game
{
    /// <summary>仅在电力版／整合版编译与登记的电力文本。</summary>
    public static partial class ModStrings
    {
        public static partial class UI
        {
            public static partial class UISIDESCREENS
            {
                public static partial class MATERIALOVERHAUL
                {
                    public static LocString POWER_WIRE_COUNT = "电线数量：{0}";
                    public static LocString POWER_WIRE_COUNT_PENDING = "电线数量：统计中";
                    public static LocString POWER_WIRE_COUNT_TOOLTIP = "选中对象所在独立电网中已建成的电线格数。\n不计规划线路、电桥本体和建筑；未连接电网时为0。";
                }
            }
        }
    }
}
