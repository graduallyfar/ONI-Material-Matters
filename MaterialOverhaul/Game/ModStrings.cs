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

                    public static class FORCEDRUN
                    {
                        public static LocString ENABLE = "开启强制运行";
                        public static LocString DISABLE = "关闭强制运行";
                        public static LocString TOOLTIP_ENABLE = "强制运行：不因温区越界停止生产，并持续加热。原料、供电、自动化生产许可与建筑完好仍然必须满足。";
                        public static LocString TOOLTIP_DISABLE = "强制运行已开启。关闭后按温区输出端口当时的接线状态重新判断生产许可，并保留当前批次。";
                    }
                }
            }

            // STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.*
            public static class UISIDESCREENS
            {
                public static class MATERIALOVERHAUL
                {

                    /// <summary>
                    /// 配方详情「效果」里的温度行。{0} 最低可工作温度、{1} 最高可工作温度、
                    /// {2} 最佳温区下限、{3} 最佳温区上限。
                    /// </summary>
                    public static LocString RECIPE_TEMP_RANGE_DETAIL = "工作温区：{0} ~ {1}（最佳温区 {2} ~ {3}）";

                }
            }

            // STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.*
            public static class BUILDINGEFFECTS
            {
                public static class MATERIALOVERHAUL
                {
                    /// <summary>信息卡里的过热温度行：{0} 过热阈值。</summary>
                    public static LocString OVERHEAT_LINE = "过热温度：{0}";

                    /// <summary>
                    /// 过热温度行的提示：{0} 主建造材料名、{1} 材料熔点、{2} 过热阈值、{3} 致命温度。
                    /// </summary>
                    public static LocString OVERHEAT_TOOLTIP = "材料革新：过热阈值 = 主建造材料 {0} 的熔点 {1} × 85% = {2}。\n建筑本体超过该温度会过热受损；温区上限只停止生产，不提供耐热保护。\n致命温度：{3}。";

                    public static LocString HEAT_PREHEAT = "预热中";

                    public static LocString HEAT_TOPUP = "补热中";

                    public static LocString HEAT_SUSTAINED = "持续加热";

                    public static LocString HEAT_NONE = "无";

                    public static LocString HEAT_SOURCE = "材料革新：内置加热";

                    public static LocString HARDNESS_NONE = "主建造材料 {0}，生命值 {1}；该建筑未登记要求硬度，不产生机械磨损。";

                    public static LocString HARDNESS_OK = "主建造材料 {0}，硬度 {1}/{2}，生命值 {3}；硬度合格，机械磨损为零。";

                    public static LocString HARDNESS_WEAK = "主建造材料 {0}，硬度 {1}/{2}，生命值 {3}；硬度不足，按实际生产时间磨损（{4} 磨损单位/生产秒，{5} HP/磨损单位）。";

                    /// <summary>
                    /// 运行状态文字（v0.0.4 起显示在信息卡**状态栏**的状态项里），键名与 <c>RunStatusText</c> 一一对应。
                    /// </summary>
                    public static class STATUS
                    {
                        public static LocString NO_TASK = "无生产任务";

                        public static LocString WAITING_MATERIALS = "等待材料";

                        public static LocString BROKEN_DOWN = "损坏停机";

                        public static LocString MANUALLY_DISABLED = "手动禁用";

                        public static LocString UNPOWERED = "等待供电";

                        public static LocString HIGH_TEMP_WAITING = "等待降温";

                        public static LocString PREHEAT = "预热中";

                        public static LocString TOPUP = "补热中";

                        public static LocString SUSTAINED = "持续加热";

                        public static LocString AUTOMATION_PAUSED = "生产已被自动化暂停";

                        public static LocString WAITING_WORKER = "等待操作";

                        public static LocString PRODUCING = "生产中";

                        public static LocString PRODUCING_LOW = "低温低效生产";

                        public static LocString PRODUCING_HIGH = "高温低效生产";

                        public static LocString COOLING = "降温中";
                    }
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
                    public static LocString TEMPZONE_NOTE = "本建筑按配方温区运行，最佳温区内满效率；具体工作温区见配方详情。输出接线使用控制模式，未接线使用默认模式。";
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
                    public static class RUNMODE
                    {
                        public static LocString DEFAULT = "运行模式：默认模式";
                        public static LocString CONTROL = "运行模式：控制模式";
                        public static LocString PENDING = "运行模式：等待自动化网络就绪";
                        public static LocString DEFAULT_TOOLTIP = "温区输出端口未接线，在整个可工作温区内生产，仍受启动与恢复条件约束。输入端接线和红绿值不选择模式。";
                        public static LocString CONTROL_TOOLTIP = "温区输出端口已接线，只在最佳温区内生产，仍受启动与恢复条件约束。输出红绿值不选择模式；绿色不代表正在生产。";
                        public static LocString PENDING_TOOLTIP = "自动化网络尚未重建，保留存档模式与温控回差，暂不生产或加热。";
                        public static LocString FORCED = "强制运行已开启";
                        public static LocString FORCED_TOOLTIP = "强制运行覆盖温区停机限制并持续加热；关闭后按当时的输出接线重判。材料、供电、自动化许可、操作和建筑完好条件仍有效。";
                    }

                    public static class WEARRISK
                    {
                        public static LocString NAME = "机械磨损风险";
                        public static LocString TOOLTIP = "主建造材料硬度不足，实际生产会累计机械磨损。预热、待机和暂停不累计磨损。";
                    }

                    public static class HEATRISK
                    {
                        public static LocString NAME = "超过过热阈值";
                        public static LocString TOOLTIP = "建筑本体已超过实际过热阈值，存在过热受损风险。温区停机不提供耐热保护。";
                    }

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

                    public static class UNKNOWN
                    {
                        public static LocString NAME = "损坏（来源未知）";
                        public static LocString TOOLTIP = "损伤来自旧版本存档，当时没有记录来源。保留原版维修温度行为，不把它当成纯机械损坏。";
                    }

                    /// <summary>
                    /// 建筑信息卡**状态栏**里的运行状态条目（V0.0.4 开发计划 §2.3）。
                    /// NAME 与 TOOLTIP 都是格式化模板：NAME 只带状态文字；
                    /// TOOLTIP 依次带状态、本体温度、温度效率、请求功率、实际功率、实际注热、批次进度。
                    /// 原版已有的「无生产任务」「等待材料」「等待操作」「生产中」仍由原版状态项显示，
                    /// 这里只补充温度与自动化造成的状态，避免同一条信息出现两次。
                    /// </summary>
                    public static class RUNSTATUS
                    {
                        public static LocString NAME = "材料革新：{0}";

                        public static LocString TOOLTIP = "材料革新状态：{0}\n本体温度 {1}，温度效率 {2}\n请求功率 {3}，实际用电 {4}，实际注热 {5}\n当前批次进度 {6}";
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
                        public static LocString ACTIVE = "已锁定配方，且本体温度位于该配方的最佳温区。接线使用控制模式；绿色不代表正在生产。";
                        public static LocString INACTIVE = "未锁定配方，或本体温度不在最佳温区。未接线使用默认模式。";
                    }

                    /// <summary>
                    /// 「允许／暂停生产」自动化输入端口（建筑细节文档 §2.2.3）。
                    /// 端口本身复用原版逻辑控制端口，说明文本由本 Mod 在 <c>LogicPorts.OnSpawn</c> 时改写。
                    /// </summary>
                    public static class PRODUCTION_CONTROL
                    {
                        public static LocString DESCRIPTION = "允许／暂停生产";

                        public static LocString ACTIVE = "绿色：允许生产（仍需要材料、供电与对应的开工条件）；未接线默认放行";

                        public static LocString INACTIVE = "红色：暂停生产；不影响配方准备、预热与补热";
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
