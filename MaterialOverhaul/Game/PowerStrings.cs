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
                    public static LocString POWER_WIRE_COUNT_TOOLTIP = "选中对象所在电路已建成的电线格数，不计电桥本体";
                    public static LocString POWER_LOSS = "线路损耗：{0:0.##} W";
                    public static LocString POWER_DELIVERED = "实际输送：{0:0.##} W";
                    public static LocString POWER_LOSS_TOOLTIP = "输电时电线上损失的功率，以及实际送到用电设备、电池和变压器的功率\n线路越长、导线越差，损耗越大";
                    public static LocString POWER_LOSS_PENDING = "线路损耗：等待结算";
                    public static LocString POWER_LOSS_FALLBACK = "线路损耗：原版结算";
                    public static LocString POWER_LOSS_REPORT = "线路损耗";
                    public static LocString POWER_REASON_UNSUPPORTED = "路径或连接类型尚不支持";
                    public static LocString POWER_REASON_MATERIAL = "未配置电导率：{0}";
                    public static LocString POWER_REASON_RANGE = "路径损耗超出数值范围";
                    public static LocString POWER_REASON_VIRTUAL = "接电端类型或虚拟连接尚不支持";
                    public static LocString POWER_REASON_PATCH = "结算补丁未就绪";
                }
            }
        }
    }
}
