namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 本 Mod 的本地化键树。
    ///
    /// **为什么必须这样定义（已核实原版机制）**：
    /// `Localization.OverloadStrings` 只遍历**已经存在的 LocString 静态字段**去覆盖文本，
    /// 它不会新增键。因此把自定义键直接写进 mod 的 `strings/*.po` 不会生效——
    /// 界面会原样显示 `MISSING.STRINGS.XXX`。
    ///
    /// 真正建键的是 `LocString.CreateLocStringKeys`：它为字段树里的每个 LocString
    /// 执行 `SetKey(键)` 并 `Strings.Add(键, 文本)`。键名规则是
    /// `"STRINGS." + 各级嵌套类名 + "." + 字段名`，所以下面的类名必须与目标键路径逐段对应。
    ///
    /// 字段默认值直接给中文：即使没有 .po，界面也能显示正确文本。
    /// 必须在 `Localization.Initialize` 之后注册（本 Mod 的 OnLoad 满足这一点）。
    /// </summary>
    public static class ModStrings
    {
        // STRINGS.UI.USERMENUACTIONS.MATERIALOVERHAUL.*
        public static class UI
        {
            public static class USERMENUACTIONS
            {
                public static class MATERIALOVERHAUL
                {
                    public static class RUNMODE
                    {
                        public static LocString TO_DEFAULT = "切换为默认模式";
                        public static LocString TO_CONTROL = "切换为控制模式";
                        public static LocString TOOLTIP_DEFAULT = "默认模式：在整个可工作温区内都能生产，温度只影响效率。";
                        public static LocString TOOLTIP_CONTROL = "控制模式：只在最佳温区内生产；生产时温度低于补热线会自动补热。";
                    }

                    public static class FORCEDRUN
                    {
                        public static LocString ENABLE = "开启强制运行";
                        public static LocString DISABLE = "关闭强制运行";
                        public static LocString TOOLTIP_ENABLE = "强制运行：不因温区越界停止生产，并持续加热。原料、供电、自动化生产许可与建筑完好仍然必须满足。";
                        public static LocString TOOLTIP_DISABLE = "强制运行已开启。关闭后按当前模式的温区限制重新判断生产许可，并保留当前批次。";
                    }
                }
            }

            // STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.*
            public static class UISIDESCREENS
            {
                public static class MATERIALOVERHAUL
                {
                    /// <summary>
                    /// 配方名后面追加的工作温区文本。{0} 配方名、{1} 最低可工作温度、{2} 最高可工作温度。
                    /// 由 <c>ComplexRecipe.GetUIName</c> 的后置补丁追加，只对有温区数值的配方生效。
                    /// </summary>
                    public static LocString RECIPE_TEMP_RANGE = "{0}（工作温区 {1} ~ {2}）";
                }
            }
        }

        // STRINGS.BUILDINGS.DAMAGESOURCES.MATERIALOVERHAUL.MECHANICAL_WEAR
        // STRINGS.BUILDINGS.PREFABS.MATERIALOVERHAUL.*
        public static class BUILDINGS
        {
            public static class DAMAGESOURCES
            {
                public static class MATERIALOVERHAUL
                {
                    public static LocString MECHANICAL_WEAR = "机械磨损";
                }
            }

            public static class PREFABS
            {
                public static class MATERIALOVERHAUL
                {
                    /// <summary>
                    /// 建筑信息卡描述里追加的一句话：告诉玩家本建筑需要处在配方的工作温区内。
                    /// 由 <c>BuildingDef.Desc</c> 的后置补丁追加到原版描述之后，只对已有温区数值的建筑生效。
                    /// </summary>
                    public static LocString TEMPZONE_NOTE = "本建筑需要在当前配方的工作温区内运行：低于最低可工作温度时停产预热，高于最高可工作温度时停产等待降温，最佳温区内才是满效率。";
                }
            }
        }

        // STRINGS.BUILDING.STATUSITEMS.* 与 STRINGS.BUILDING.LOGICPORTS.*
        public static class BUILDING
        {
            public static class STATUSITEMS
            {
                public static class MATERIALOVERHAUL
                {
                    public static class MECHANICAL
                    {
                        public static LocString NAME = "机械损坏";
                        public static LocString TOOLTIP = "主建造材料硬度不足，按实际生产时间累计磨损导致损坏。维修采用普通方式，不重置温度。";
                    }

                    public static class OVERHEAT
                    {
                        public static LocString NAME = "过热损坏";
                        public static LocString TOOLTIP = "建筑本体温度超过过热阈值导致损坏。维修按原版过热方式处理并重置温度。";
                    }

                    public static class BOTH
                    {
                        public static LocString NAME = "机械损坏＋过热损坏";
                        public static LocString TOOLTIP = "同时存在机械磨损与过热损坏。维修按过热方式处理并重置温度，但这不代表额外多扣一份机械伤害。";
                    }
                }
            }

            public static class LOGICPORTS
            {
                public static class MATERIALOVERHAUL
                {
                    public static class TEMPERATURE_ZONE
                    {
                        public static LocString DESCRIPTION = "温区信号";
                        public static LocString ACTIVE = "已锁定批次处于最佳温区";
                        public static LocString INACTIVE = "未处于最佳温区，或没有已锁定批次";
                    }
                }
            }

            // STRINGS.BUILDING.ATTRIBUTES.MATERIALOVERHAUL.OVERHEAT.DESCRIPTION
            // 过热温度属性修正项的说明文本（v0.0.2：过热阈值 = 主建造材料熔点 × 85%）。
            public static class ATTRIBUTES
            {
                public static class MATERIALOVERHAUL
                {
                    public static class OVERHEAT
                    {
                        public static LocString DESCRIPTION = "材料革新：过热阈值取主建造材料熔点的 85%";
                    }
                }
            }
        }
    }
}
