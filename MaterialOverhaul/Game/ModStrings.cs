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

                    /// <summary>
                    /// 配方详情「效果」里的温度行。{0} 最低可工作温度、{1} 最高可工作温度、
                    /// {2} 最佳温区下限、{3} 最佳温区上限。
                    /// </summary>
                    public static LocString RECIPE_TEMP_RANGE_DETAIL = "工作温区：{0} ~ {1}（最佳温区 {2} ~ {3}）";

                    /// <summary>逐笔订单面板（V0.0.3 §5.1）：侧屏标题。</summary>
                    public static class ORDER_LIST
                    {
                        public static LocString TITLE = "材料革新：订单列表";

                        public static LocString EMPTY = "还没有订单。用上方的配方列表添加。";

                        public static LocString ROW_CURRENT = "▶";

                        public static LocString ROW_OTHER = "　";

                        public static LocString ROW_LIMITED = "{0}　剩余 {1} 批";

                        public static LocString ROW_INFINITE = "{0}　无限（本轮 {1}/{2}）";

                        public static LocString ROW_TOOLTIP = "本行的配方：{0}\n按玩家添加顺序排列，同一配方可以重复添加。";

                        public static LocString BUTTON_REMOVE = "取消";

                        public static LocString BUTTON_REMOVE_TOOLTIP = "取消这一笔订单。如果它正是当前批次，原料会退回输入仓，本批进度清零。";

                        public static LocString BUTTON_MINUS = "−";

                        public static LocString BUTTON_MINUS_TOOLTIP = "这一笔的剩余批次数减 1；减到 0 等于取消该笔。";

                        public static LocString BUTTON_PLUS = "+";

                        public static LocString BUTTON_PLUS_TOOLTIP = "这一笔的剩余批次数加 1。";

                        public static LocString BUTTON_INFINITE = "∞";

                        public static LocString BUTTON_INFINITE_TOOLTIP = "把这一笔改为无限订单（每轮按额度生产后让出）。再点一次改回有限订单。";

                        public static LocString BUTTON_QUOTA_DOWN = "≤";

                        public static LocString BUTTON_QUOTA_UP = "≥";

                        public static LocString BUTTON_QUOTA_TOOLTIP = "无限订单每轮额度的上下限：1–99 批。修改额度保留本轮已完成数。";

                        public static LocString LIMIT_HINT = "无限额度范围 1–{0}。";
                    }
                }
            }

            // STRINGS.UI.BUILDINGEFFECTS.MATERIALOVERHAUL.*
            public static class BUILDINGEFFECTS
            {
                public static class MATERIALOVERHAUL
                {
                    /// <summary>信息卡里的过热温度行：{0} 过热阈值。</summary>
                    public static LocString OVERHEAT_LINE = "过热温度：{0}（材料革新：主建造材料熔点 × 85%）";

                    /// <summary>
                    /// 过热温度行的提示：{0} 主建造材料名、{1} 材料熔点、{2} 过热阈值、{3} 致命温度。
                    /// </summary>
                    public static LocString OVERHEAT_TOOLTIP = "材料革新：过热阈值 = 主建造材料 {0} 的熔点 {1} × 85% = {2}。\n建筑本体超过该温度会过热受损；温区上限只停止生产，不提供耐热保护。\n致命温度：{3}。";

                    /// <summary>工作温区行：{0} 最低可工作温度、{1} 最高可工作温度、{2} 最佳温区下限、{3} 最佳温区上限。</summary>
                    public static LocString TEMPZONE_LINE = "工作温区：{0} ~ {1}（最佳温区 {2} ~ {3}）";

                    /// <summary>当前生产行：{0} 订单摘要、{1} 批次进度百分比。</summary>
                    public static LocString CURRENT_BATCH = "当前生产：{0}；批次进度 {1}";

                    public static LocString CURRENT_NO_ORDER = "无订单记录（来自旧存档迁移）";

                    /// <summary>功率行：{0} 请求功率、{1} 实际用电、{2} 实际注热。</summary>
                    public static LocString POWER_LINE = "请求功率 {0}；实际用电 {1}；实际注热 {2}";

                    public static LocString HEAT_PREHEAT = "预热中";

                    public static LocString HEAT_TOPUP = "补热中";

                    public static LocString HEAT_SUSTAINED = "持续加热";

                    public static LocString HEAT_NONE = "无";

                    public static LocString HEAT_SOURCE = "材料革新：内置加热";

                    /// <summary>有限订单摘要：{0} 配方名、{1} 剩余批次数。</summary>
                    public static LocString ORDER_LIMITED = "{0}（剩余 {1} 批）";

                    /// <summary>无限订单摘要：{0} 配方名、{1} 本轮已完成、{2} 本轮额度。</summary>
                    public static LocString ORDER_INFINITE = "{0}（无限，本轮 {1}/{2}）";

                    public static LocString NO_TASK = "无生产任务";

                    /// <summary>等待材料：{0} 待检查的订单笔数。</summary>
                    public static LocString WAITING_MATERIALS = "等待材料（待检查订单 {0} 笔）";

                    /// <summary>下一批预告：{0} 订单摘要。</summary>
                    public static LocString NEXT_BATCH = "下一生产：{0}";

                    public static LocString HARDNESS_NONE = "主建造材料 {0}，生命值 {1}；该建筑未登记要求硬度，不产生机械磨损。";

                    public static LocString HARDNESS_OK = "主建造材料 {0}，硬度 {1}/{2}，生命值 {3}；硬度合格，机械磨损为零。";

                    public static LocString HARDNESS_WEAK = "主建造材料 {0}，硬度 {1}/{2}，生命值 {3}；硬度不足，按实际生产时间磨损（{4} 磨损单位/生产秒，{5} HP/磨损单位）。";

                    /// <summary>过热「条件」行：只超过阈值、还没扣血时的提示（{0} 过热阈值）。</summary>
                    public static LocString OVERHEAT_CONDITION = "本体温度已超过过热阈值 {0}，继续升温会过热受损。";

                    /// <summary>信息卡里的订单列表标题。</summary>
                    public static LocString ORDER_LIST_HEADER = "订单列表（按添加顺序）：";

                    /// <summary>订单列表每一行（{0} 当前批次标记、{1} 序号、{2} 订单摘要）。</summary>
                    public static LocString ORDER_LIST_ROW = "{0}{1}. {2}";

                    /// <summary>运行状态文字（信息卡首行），键名与 <c>RunStatusText</c> 一一对应。</summary>
                    public static class STATUS
                    {
                        public static LocString NO_TASK = "无生产任务";

                        public static LocString WAITING_MATERIALS = "等待材料";

                        public static LocString BROKEN_DOWN = "损坏停机";

                        public static LocString MANUALLY_DISABLED = "手动禁用";

                        public static LocString UNPOWERED = "等待供电";

                        public static LocString HIGH_TEMP_WAITING = "高温保护";

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

                    public static class UNKNOWN
                    {
                        public static LocString NAME = "损坏（来源未知）";
                        public static LocString TOOLTIP = "损伤来自旧版本存档，当时没有记录来源。保留原版维修温度行为，不把它当成纯机械损坏。";
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
