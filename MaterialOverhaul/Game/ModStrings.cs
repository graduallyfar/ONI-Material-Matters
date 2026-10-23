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
    public static partial class ModStrings
    {
        // STRINGS.UI.USERMENUACTIONS.MATERIALOVERHAUL.*
        public static partial class UI
        {
            public static class USERMENUACTIONS
            {
                public static class MATERIALOVERHAUL
                {

                    public static class FORCEDRUN
                    {
                        public static LocString ENABLE = "启用强制运行";
                        public static LocString DISABLE = "禁用强制运行";
                        public static LocString TOOLTIP_ENABLE = "忽略温区限制继续生产，并持续加热。\n过热受损后停止加热，修好后恢复；自动化信号仍能暂停生产。";
                        public static LocString TOOLTIP_DISABLE = "恢复温区限制，保留当前生产进度。";
                        public static LocString TOOLTIP_NO_HEATING = "忽略温区限制继续生产。\n这个建筑没有内置加热，需要靠生产热量或外部热源升温。";
                    }
                }
            }

            // STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.*
            public static partial class UISIDESCREENS
            {
                public static partial class MATERIALOVERHAUL
                {

                    /// <summary>
                    /// 配方详情「效果」里的温度行（V0.1.2：只显示**配方最佳温区**，不再是四边界）。
                    /// {0} 最佳温区下限、{1} 最佳温区上限。
                    /// </summary>
                    public static LocString RECIPE_TEMP_RANGE_DETAIL = "最佳<link=\"HEAT\">温度</link>：{0} - {1}";
                    public static LocString RECIPE_TEMP_RANGE_TOOLTIP = "建筑温度在这个范围内时，配方的温度效率为100%。";

                    /// <summary>
                    /// 建筑信息卡里单独的「订单队列」面板：按添加顺序列出全部订单。
                    /// {0} 序号、{1} 配方名、{2} 剩余批次 / 本轮已完成、{3} 本轮额度。
                    /// </summary>
                    public static class ORDER_QUEUE
                    {
                        public static LocString TITLE = "订单队列";
                        public static LocString LIMITED = "{0}. {1} · 剩余 {2} 批";
                        public static LocString INFINITE = "{0}. {1} · 持续生产（本轮 {2}/{3}）";
                        public static LocString CURRENT = "（生产中）";
                        public static LocString NEXT = "（轮到）";
                        public static LocString MORE = "还有 {0} 张订单";
                        public static LocString TOOLTIP = "订单按添加顺序轮换：缺料的订单暂时跳过，下次轮到再检查；\n有限订单做完后移出队列，持续生产每轮做完额度后让给下一张。";
                    }

                    public static class HEATING_POWER
                    {
                        public static LocString TITLE = "加热功率";
                        public static LocString TOOLTIP = "内置加热耗电：<b>{0} W</b>\n用于预热和维持温度，生产耗电另计。";
                    }

                }
            }

            // STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.*
            public static class BUILDINGEFFECTS
            {
                public static class MATERIALOVERHAUL
                {
                    /// <summary>
                    /// 信息卡效果列表里的工作温区行（按原版分工：机制与数值放效果区）。
                    /// {0} 建筑最低工作温度、{1} 建筑最高工作温度（＝过热阈值）。
                    /// </summary>
                    public static LocString TEMPZONE_LINE = "工作<link=\"HEAT\">温度</link>：{0} - {1}";
                    public static LocString PROCESS_BEST = "最佳<link=\"HEAT\">温度</link>：{0} - {1}";
                    public static LocString PROCESS_BEST_TOOLTIP = "建筑温度在这个范围内时，温度效率为100%。";
                    public static LocString INDUSTRIAL_STATUS = "建筑温度：{0}\n温度效率：{1}\n耗电：{2}（需求：{3}）";
                    public static LocString GENERATION_STATUS = "额定发电：{0} · 当前发电：{1}";
                    public static LocString IMMUNE_HEATING_TOOLTIP = "保留原版过热豁免，不会受过热损伤；内置加热仍受工作温区和供电限制。";

                    /// <summary>
                    /// 工作温区行的提示。只说明规则来源与模式选择；过热阈值的推导明细由原版
                    /// 「过热温度」行的属性明细承担，配方最佳温区引导到配方详情。
                    /// </summary>
                    public static LocString TEMPZONE_TOOLTIP = "这个建筑正常生产所需的温度范围。\n最高温度由建造材料决定，各配方的最佳温度见配方详情。";
                    public static LocString TEMPZONE_IMMUNE_TOOLTIP = "这个建筑正常生产所需的温度范围。\n最高温度为主材料熔点的75%；保留原版过热豁免，没有内置加热。";

                    public static LocString HEAT_PREHEAT = "预热中";

                    public static LocString HEAT_TOPUP = "补热中";

                    public static LocString HEAT_SUSTAINED = "持续加热";

                    public static LocString HEAT_NONE = "无";

                    public static LocString HEAT_SOURCE = "内置加热";

                    public static LocString HARDNESS_NONE = "建造材料：{0}\n生命值：{1}\n这个建筑没有硬度要求，不会产生机械磨损。";

                    public static LocString HARDNESS_OK = "建造材料：{0} · 硬度：{1}（要求：{2}）\n生命值：{3}\n材料硬度足够，不会产生机械磨损。";

                    public static LocString HARDNESS_WEAK = "建造材料：{0} · 硬度：{1}（要求：{2}）\n生命值：{3}\n机械磨损：{4}单位/工作秒，每单位损失{5}生命值。";

                    /// <summary>
                    /// 运行状态文字（v0.0.4 起显示在信息卡**状态栏**的状态项里），键名与 <c>RunStatusText</c> 一一对应。
                    /// </summary>
                    public static class STATUS
                    {
                        public static LocString ZONE_START = "启动温度：{0}\n这个建筑没有内置加热。";
                        public static LocString ZONE_START_CAPPED = "启动温度：{1}（原目标：{0}，受建造材料限制）\n这个建筑没有内置加热。";
                        public static LocString ZONE_UNREACHABLE_NO_HEATING = "配方的最佳温度至少为{0}，超过建造材料的工作上限{1}。\n控制模式无法生产，可改用默认模式或熔点更高的建造材料。";
                        public static LocString NO_TASK = "无制作队列";

                        public static LocString WAITING_MATERIALS = "等待材料";

                        public static LocString BROKEN_DOWN = "建筑损坏";

                        public static LocString MANUALLY_DISABLED = "已禁用";

                        public static LocString UNPOWERED = "等待供电";

                        public static LocString HIGH_TEMP_WAITING = "等待降温";

                        public static LocString PREHEAT = "预热中";

                        public static LocString TOPUP = "补热中";

                        public static LocString SUSTAINED = "持续加热";

                        public static LocString AUTOMATION_PAUSED = "自动化暂停";

                        public static LocString WAITING_WORKER = "操作任务";

                        public static LocString PRODUCING = "生产中";

                        public static LocString PRODUCING_LOW = "低温生产（效率降低）";

                        public static LocString PRODUCING_HIGH = "高温生产（效率降低）";

                        public static LocString COOLING = "降温中";

                        /// <summary>需要升温但没有实际注热：不假称为预热中（V0.1.2 §4.2）。</summary>
                        public static LocString WAITING_HEAT = "等待升温";

                        /// <summary>预热中／等待升温后附带的开工温度：{0} 实际目标。</summary>
                        public static LocString TARGET_SUFFIX = "（目标 {0}）";

                        /// <summary>最佳温区不可达且已停在受限目标：温度受限 · 生产暂停。</summary>
                        public static LocString TEMP_LIMITED = "生产暂停：温区受限";

                        /// <summary>独立温区限制提示：最佳温区整体高于建筑上限（V0.1.2 §4.1 第 3 条）。</summary>
                        public static LocString ZONE_UNREACHABLE = "最佳温区不可达";

                        /// <summary>独立温区限制提示：最佳区高侧被建筑上限截断，但仍有交集。</summary>
                        public static LocString ZONE_LIMITED = "最佳温区受限";

                        /// <summary>
                        /// 状态悬停的温区说明行。{0} 本体温度、{1} 建筑下限、{2} 建筑上限、
                        /// {3} 主建造材料、{4} 配方最佳下限、{5} 配方最佳上限。
                        /// </summary>
                        public static LocString ZONE_DETAIL = "工作温度：{1} - {2}\n建造材料：{3}\n配方最佳温度：{4} - {5}";

                        /// <summary>加热目标未受限时的一行：{0} 实际目标。</summary>
                        public static LocString ZONE_TARGET = "加热目标：{0}";

                        /// <summary>加热目标被建筑上限封顶时：{0} 原目标、{1} 实际目标。</summary>
                        public static LocString ZONE_TARGET_CAPPED = "加热目标：{1}（原目标：{0}，受建造材料限制）";

                        /// <summary>不可达时的完整说明：{0} 配方最佳下限、{1} 建筑上限。</summary>
                        public static LocString ZONE_UNREACHABLE_DETAIL = "配方的最佳温度至少为{0}，超过建造材料的工作上限{1}。\n控制模式无法生产，可改用默认模式或熔点更高的建造材料。";

                        /// <summary>受限时的完整说明：{0}B₁、{1}可用上限、{2}B₂、{3}L、{4}U。</summary>
                        public static LocString ZONE_LIMITED_DETAIL = "控制模式可用温度：{0} - {1}（配方最佳上限：{2}）\n默认模式可在{3} - {4}内生产，强制运行可越过限制。";
                    }
                }
            }
        }

        // STRINGS.BUILDINGS.DAMAGESOURCES.MATERIALOVERHAUL.MECHANICAL_WEAR
        public static class BUILDINGS
        {
            public static class DAMAGESOURCES
            {
                public static class MATERIALOVERHAUL
                {
                    public static LocString MECHANICAL_WEAR = "机械磨损";
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
                        public static LocString DEFAULT = "默认模式";
                        public static LocString CONTROL = "控制模式";
                        public static LocString PENDING = "等待自动化连接";
                        public static LocString DEFAULT_TOOLTIP = "在工作温区内生产。\n连接温区信号输出后切换为控制模式。";
                        public static LocString CONTROL_TOOLTIP = "只在配方最佳温区内生产。\n断开温区信号输出后切换为默认模式。";
                        public static LocString PENDING_TOOLTIP = "正在确认温区信号输出的连接，确认前不生产也不加热。";
                        public static LocString FORCED = "强制运行";
                        public static LocString FORCED_TOOLTIP = "忽略温区限制继续生产，并持续加热。\n过热受损后停止加热，修好后恢复。";
                        public static LocString FORCED_NO_HEATING_TOOLTIP = "忽略温区限制继续生产。\n没有内置加热，需要靠生产热量或外部热源升温。";
                    }

                    public static class WEARRISK
                    {
                        public static LocString NAME = "硬度不足";
                        public static LocString TOOLTIP = "建造材料硬度低于要求，工作时会磨损。";
                    }

                    public static class HEATRISK
                    {
                        public static LocString NAME = "过热";
                        public static LocString TOOLTIP = "建筑温度超过过热温度，可能受到损伤。";
                    }

                    /// <summary>
                    /// 独立温区限制提示（V0.1.2 §4.1 第 3 条）：只说明普通控制生产被建材上限限制，
                    /// 不冒充损坏，也不把默认模式的低效生产写成停产。
                    /// </summary>
                    public static class ZONELIMIT
                    {
                        public static LocString NAME = "温区受限";
                        public static LocString TOOLTIP = "配方的最佳温区超出建造材料的工作范围。\n控制模式只能在重合部分生产，或启用强制运行。";
                    }

                    public static class MECHANICAL
                    {
                        public static LocString NAME = "损坏：机械磨损";
                        public static LocString TOOLTIP = "这个建筑因材料硬度不足而受损。\n维修后保留建筑温度。";
                    }

                    public static class OVERHEAT
                    {
                        public static LocString NAME = "损坏：过热";
                        public static LocString TOOLTIP = "这个建筑因过热而受损。\n维修后会降低建筑温度。";
                        public static LocString HEATING_STOPPED_TOOLTIP = "内置加热已停止，完全修复后才能恢复。";
                    }

                    public static class BOTH
                    {
                        public static LocString NAME = "损坏：机械磨损、过热";
                        public static LocString TOOLTIP = "这个建筑存在机械和过热损坏。\n维修后会降低建筑温度。";
                    }

                    public static class UNKNOWN
                    {
                        public static LocString NAME = "损坏：原因不明";
                        public static LocString TOOLTIP = "这个建筑已经受损，需要维修。";
                    }

                    /// <summary>
                    /// 建筑信息卡**状态栏**里的运行状态条目（V0.0.4 开发计划 §2.3）。
                    /// NAME 与 TOOLTIP 都是格式化模板：NAME 只带状态文字（「材料革新」前缀已移除，
                    /// 状态项本身带图标与专属类别）；
                    /// TOOLTIP 的参数顺序仍是状态、温度、效率、请求功率、实际用电、加热状态、批次进度，
                    /// 模板显示温度、效率、功率、加热状态与生产进度；状态文字由标题显示，不重复。
                    /// 原版已有的「无生产任务」「等待材料」「等待操作」「生产中」仍由原版状态项显示，
                    /// 这里只补充温度与自动化造成的状态，避免同一条信息出现两次。
                    /// </summary>
                    public static class RUNSTATUS
                    {
                        public static LocString NAME = "{0}";

                        public static LocString TOOLTIP = "建筑温度：{1}\n温度效率：{2} · 生产进度：{6}\n耗电：{4}（需求：{3}）\n内置加热：{5}";
                    }
                }
            }

            public static class LOGICPORTS
            {
                public static class MATERIALOVERHAUL
                {
                    public static class TEMPERATURE_ZONE
                    {
                        public static LocString DESCRIPTION = "最佳温度信号";
                        public static LocString ACTIVE = "建筑温度处于当前配方的最佳温区时，输出<b><style=\"logic_on\">绿色信号</style></b>。\n连接后建筑使用控制模式。";
                        public static LocString INACTIVE = "温度不在最佳温区，或配方没有温度要求时，输出<b><style=\"logic_off\">红色信号</style></b>。\n未连接时建筑使用默认模式。";
                        public static LocString INDUSTRIAL_ACTIVE = "有可执行的工艺且建筑温度处于最佳温区时，输出<b><style=\"logic_on\">绿色信号</style></b>。\n连接后建筑使用控制模式。";
                        public static LocString INDUSTRIAL_INACTIVE = "没有可执行的工艺，或温度不在最佳温区时，输出<b><style=\"logic_off\">红色信号</style></b>。\n未连接时建筑使用默认模式。";
                    }

                    /// <summary>
                    /// 「允许／暂停生产」自动化输入端口（建筑细节文档 §2.2.3）。
                    /// 端口本身复用原版逻辑控制端口，说明文本由本 Mod 在 <c>LogicPorts.OnSpawn</c> 时改写。
                    /// </summary>
                    public static class PRODUCTION_CONTROL
                    {
                        public static LocString DESCRIPTION = "生产控制";

                        public static LocString ACTIVE = "<b><style=\"logic_on\">绿色信号</style></b>：允许生产。未连接时允许生产。";

                        public static LocString INACTIVE = "<b><style=\"logic_off\">红色信号</style></b>：暂停生产，预热和维持温度不受影响。";
                    }
                }
            }

            // STRINGS.BUILDING.ATTRIBUTES.MATERIALOVERHAUL.OVERHEAT.DESCRIPTION
            // 基础上限来源；v0.2.2 起最终值另计主材料百分比。
            public static class ATTRIBUTES
            {
                public static class MATERIALOVERHAUL
                {
                    public static class OVERHEAT
                    {
                        public static LocString DESCRIPTION = "基础工作上限：建造材料熔点的75%（另计材料修正）";
                    }
                }
            }
        }
    }
}
