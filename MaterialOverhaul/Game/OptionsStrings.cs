namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 模组界面“选项”按钮与系统开关对话框的本地化键树（仅整合版）。键名前缀为
    /// <c>STRINGS.MODOPTIONS.MATERIALOVERHAUL</c>，由
    /// <c>LocString.CreateLocStringKeys(typeof(OptionsStrings.MODOPTIONS), "STRINGS.")</c> 建立。
    /// </summary>
    public static class OptionsStrings
    {
        public static class MODOPTIONS
        {
            public static class MATERIALOVERHAUL
            {
                public static LocString BUTTON = "选项";
                public static LocString BUTTON_TOOLTIP = "选择启用物尽其用的哪些系统。\n修改保存后，重启游戏生效。";
                public static LocString TITLE = "物尽其用 · 系统开关";
                public static LocString INTRO = "点击下面的按钮开启或关闭对应系统。保存后需要重启游戏才会生效。";
                public static LocString STATE_ON = "{0}：开启";
                public static LocString STATE_OFF = "{0}：关闭";
                public static LocString PENDING = "（重启后生效）";
                public static LocString WARNING = "在已有存档中途关闭某个系统：该系统记在建筑上的数据（工业订单与磨损进度、建筑活化度、航天飞行加成等）会在下次存档时丢失；新增房间退回原版房间，复制人身上相关的效果和特质暂时失效。重新开启后系统从头开始计算。建议先备份存档。";
                public static LocString SAVE = "保存";
                public static LocString SAVE_FAILED = "系统开关保存失败：{0}";
                public static LocString RESTART_TITLE = "需要重启游戏";
                public static LocString RESTART_TEXT = "系统开关已保存，重启游戏后生效。现在重启吗？";
                public static LocString RESTART_NOW = "立即重启";
                public static LocString RESTART_LATER = "稍后";

                public static class SYSTEMS
                {
                    public static LocString INDUSTRIAL = "工业系统";
                    public static LocString INDUSTRIAL_TOOLTIP = "工业建筑的工作温区、内置加热、订单、磨损与材料改造。";
                    public static LocString POWER = "电力系统";
                    public static LocString POWER_TOOLTIP = "电线按最短路径和材料计算损耗，电池充放电与变压器分账，电网信息卡。";
                    public static LocString ROOM = "房间系统";
                    public static LocString ROOM_TOOLTIP = "房间分级、平均装饰度与温度条件、新增房间和房间效果。";
                    public static LocString RADIATION = "辐射系统";
                    public static LocString RADIATION_TOOLTIP = "感生放射性：建筑长期受强辐射后自身放射，液体、气体和地块带上放射性污染物。";
                }
            }
        }
    }
}
