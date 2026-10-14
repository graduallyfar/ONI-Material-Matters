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
                        public static LocString TOOLTIP_ENABLE = "不因温区越界停产，并持续加热；原料、供电、自动化许可与建筑完好仍需满足。";
                        public static LocString TOOLTIP_DISABLE = "关闭后按输出端口当时的接线重判生产许可，保留当前批次。";
                    }
                }
            }

            // STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.*
            public static class UISIDESCREENS
            {
                public static class MATERIALOVERHAUL
                {

                    /// <summary>
                    /// 配方详情「效果」里的温度行（V0.1.2：只显示**配方最佳温区**，不再是四边界）。
                    /// {0} 最佳温区下限、{1} 最佳温区上限。
                    /// </summary>
                    public static LocString RECIPE_TEMP_RANGE_DETAIL = "最佳温区：{0} ~ {1}";

                    public static class HEATING_POWER
                    {
                        public static LocString TITLE = "加热功率";
                        public static LocString TOOLTIP = "{0} W：预热、补热与强制持续加热共用（20–4000 W）。生产用电另计，总用电为两者之和；设置随本建筑保存。";
                    }

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
                    /// 过热温度行的提示（V0.1.2：75% 按 K 计算，且该值同时是建筑最高工作温度）。
                    /// {0} 主建造材料名、{1} 材料熔点、{2} 过热阈值／最高工作温度、{3} 致命温度。
                    /// </summary>
                    public static LocString OVERHEAT_TOOLTIP = "上限＝过热阈值＝{0} 熔点 {1} × 75%（按 K 计）= {2}。超过后默认／控制模式停产并停止内置加热，只有强制运行可越界生产。温区停机不提供耐热保护；致命温度 {3}。";

                    public static LocString HEAT_PREHEAT = "预热中";

                    public static LocString HEAT_TOPUP = "补热中";

                    public static LocString HEAT_SUSTAINED = "持续加热";

                    public static LocString HEAT_NONE = "无";

                    public static LocString HEAT_SOURCE = "内置加热";

                    public static LocString HARDNESS_NONE = "{0} · 无硬度要求，不磨损（HP {1}）";

                    public static LocString HARDNESS_OK = "{0} · 硬度 {1}/{2} · 无机械磨损（HP {3}）";

                    public static LocString HARDNESS_WEAK = "{0} · 硬度 {1}/{2} 不足，按生产时间磨损（HP {3}，{4} 磨损/生产秒，{5} HP/磨损单位）";

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

                        public static LocString AUTOMATION_PAUSED = "自动化暂停生产";

                        public static LocString WAITING_WORKER = "等待操作";

                        public static LocString PRODUCING = "生产中";

                        public static LocString PRODUCING_LOW = "低温低效生产";

                        public static LocString PRODUCING_HIGH = "高温低效生产";

                        public static LocString COOLING = "降温中";

                        /// <summary>需要升温但没有实际注热：不假称为预热中（V0.1.2 §4.2）。</summary>
                        public static LocString WAITING_HEAT = "等待升温";

                        /// <summary>最佳温区不可达且已停在受限目标：温度受限 · 生产暂停。</summary>
                        public static LocString TEMP_LIMITED = "温度受限 · 生产暂停";

                        /// <summary>独立温区限制提示：最佳温区整体高于建筑上限（V0.1.2 §4.1 第 3 条）。</summary>
                        public static LocString ZONE_UNREACHABLE = "最佳温区不可达";

                        /// <summary>独立温区限制提示：最佳区高侧被建筑上限截断，但仍有交集。</summary>
                        public static LocString ZONE_LIMITED = "最佳温区受限";

                        /// <summary>
                        /// 状态悬停的温区说明行。{0} 本体温度、{1} 建筑下限、{2} 建筑上限、
                        /// {3} 主建造材料、{4} 配方最佳下限、{5} 配方最佳上限。
                        /// </summary>
                        public static LocString ZONE_DETAIL = "温度 {0} · 工作范围 {1} ~ {2}（{3}）· 最佳温区 {4} ~ {5}";

                        /// <summary>加热目标未受限时的一行：{0} 实际目标。</summary>
                        public static LocString ZONE_TARGET = "加热目标 {0}";

                        /// <summary>加热目标被建筑上限封顶时：{0} 原目标、{1} 实际目标。</summary>
                        public static LocString ZONE_TARGET_CAPPED = "加热目标 {1}（原 {0}，受建材上限限制）";

                        /// <summary>不可达时的完整说明：{0} 配方最佳下限、{1} 建筑上限。</summary>
                        public static LocString ZONE_UNREACHABLE_DETAIL = "控制模式需要最佳下限 {0}，但建筑上限只有 {1}：无可用温区，预热到上限后暂停，不自动跳单。改用默认模式或熔点更高的建材。";

                        /// <summary>受限时的完整说明：{0}B₁、{1}可用上限、{2}B₂、{3}L、{4}U。</summary>
                        public static LocString ZONE_LIMITED_DETAIL = "控制模式可用 {0} ~ {1}（受建材上限限制，最佳上限 {2}）；默认模式仍在 {3} ~ {4} 内按效率生产。";
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
                    public static LocString TEMPZONE_NOTE = "按温区运行：上限＝主建造材料熔点 ×75%，配方另有最佳温区并满效率。输出接线＝控制模式。";
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
                        public static LocString PENDING = "等待自动化就绪";
                        public static LocString DEFAULT_TOOLTIP = "输出端口未接线：在整个工作温区生产。输入端接线与红绿值不决定模式。";
                        public static LocString CONTROL_TOOLTIP = "输出端口已接线：只在最佳温区内生产。红绿值不决定模式，绿色不代表正在生产。";
                        public static LocString PENDING_TOOLTIP = "自动化网络未就绪：保留存档模式与温控回差，暂不生产或加热。";
                        public static LocString FORCED = "强制运行已开启";
                        public static LocString FORCED_TOOLTIP = "覆盖温区停机限制；过热受损后停止内置加热，完全维修后解除。关闭后按当时接线重判；材料、供电、自动化许可、操作与建筑完好仍需满足。";
                    }

                    public static class WEARRISK
                    {
                        public static LocString NAME = "机械磨损风险";
                        public static LocString TOOLTIP = "硬度不足，实际生产累计机械磨损；预热、待机与暂停不累计。";
                    }

                    public static class HEATRISK
                    {
                        public static LocString NAME = "超过过热阈值";
                        public static LocString TOOLTIP = "已超过过热阈值，存在受损风险；温区停机不提供耐热保护。";
                    }

                    /// <summary>
                    /// 独立温区限制提示（V0.1.2 §4.1 第 3 条）：只说明普通控制生产被建材上限限制，
                    /// 不冒充损坏，也不把默认模式的低效生产写成停产。
                    /// </summary>
                    public static class ZONELIMIT
                    {
                        public static LocString NAME = "温区受限";
                        public static LocString TOOLTIP = "配方最佳温区超出建筑可用范围，限制针对控制模式；默认模式仍按效率生产，强制运行可越过上限。";
                    }

                    public static class MECHANICAL
                    {
                        public static LocString NAME = "机械损坏";
                        public static LocString TOOLTIP = "硬度不足，按生产时间累计磨损导致损坏；维修不重置温度。";
                    }

                    public static class OVERHEAT
                    {
                        public static LocString NAME = "过热损坏";
                        public static LocString TOOLTIP = "超过过热阈值导致损坏；内置加热已停止，完全维修后解除，维修重置温度。";
                    }

                    public static class BOTH
                    {
                        public static LocString NAME = "机械损坏＋过热损坏";
                        public static LocString TOOLTIP = "同时存在机械磨损与过热损坏；内置加热已停止，完全维修后解除。维修按过热方式处理并重置温度，不会额外多扣一份机械伤害。";
                    }

                    public static class UNKNOWN
                    {
                        public static LocString NAME = "损坏（来源未知）";
                        public static LocString TOOLTIP = "损伤来自旧存档，当时未记录来源；保留原版维修温度行为，不当作纯机械损坏。";
                    }

                    /// <summary>
                    /// 建筑信息卡**状态栏**里的运行状态条目（V0.0.4 开发计划 §2.3）。
                    /// NAME 与 TOOLTIP 都是格式化模板：NAME 只带状态文字（「材料革新」前缀已移除，
                    /// 状态项本身带图标与专属类别）；
                    /// TOOLTIP 的参数顺序仍是状态、温度、效率、请求功率、实际用电、实际注热、批次进度，
                    /// 但模板只取温度、效率、功率与批次——状态文字已由标题显示，不重复。
                    /// 原版已有的「无生产任务」「等待材料」「等待操作」「生产中」仍由原版状态项显示，
                    /// 这里只补充温度与自动化造成的状态，避免同一条信息出现两次。
                    /// </summary>
                    public static class RUNSTATUS
                    {
                        public static LocString NAME = "{0}";

                        public static LocString TOOLTIP = "温度 {1} · 效率 {2} · 批次 {6}\n用电 {4}（需求 {3}）· 注热 {5}";
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
                        public static LocString ACTIVE = "已锁定配方且温度在最佳温区；接线即控制模式，绿色不代表正在生产。";
                        public static LocString INACTIVE = "未锁定配方或温度不在最佳温区；未接线即默认模式。";
                    }

                    /// <summary>
                    /// 「允许／暂停生产」自动化输入端口（建筑细节文档 §2.2.3）。
                    /// 端口本身复用原版逻辑控制端口，说明文本由本 Mod 在 <c>LogicPorts.OnSpawn</c> 时改写。
                    /// </summary>
                    public static class PRODUCTION_CONTROL
                    {
                        public static LocString DESCRIPTION = "允许／暂停生产";

                        public static LocString ACTIVE = "绿色：允许生产（仍需材料、供电与开工条件）；未接线默认放行";

                        public static LocString INACTIVE = "红色：暂停生产；不影响配方准备、预热与补热";
                    }
                }
            }

            // STRINGS.BUILDING.ATTRIBUTES.MATERIALOVERHAUL.OVERHEAT.DESCRIPTION
            // 过热温度属性修正项的说明文本（v0.1.2：最高工作温度 = 过热阈值 = 主建造材料熔点 × 75%）。
            public static class ATTRIBUTES
            {
                public static class MATERIALOVERHAUL
                {
                    public static class OVERHEAT
                    {
                        public static LocString DESCRIPTION = "最高工作温度＝过热阈值，取主建造材料熔点的 75%（按 K 计）";
                    }
                }
            }
        }
    }
}
