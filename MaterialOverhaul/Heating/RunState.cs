using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 建筑的温控运行状态（V0.0.3 开发计划 §3.2、§5.3）。纯数据，随存档持久化。
    ///
    /// 三个字段各自表达一件事，不能用温度临时推断：
    ///   - <see cref="Started"/>：本批是否已经通过低温启动线。开工后跌破生产下限才复位，
    ///     复位后必须重新预热到启动线；「470 ℃已达到但还在等操作工」不算未预热完成。
    ///   - <see cref="HighTempWaiting"/>：是否处于高温暂停，等待回落到恢复线后再检查全部条件。
    ///   - <see cref="TopUpActive"/>：控制模式补热是否正在进行（回差状态，不能在读档时无条件清掉）。
    /// </summary>
    public struct RunState
    {
        /// <summary>是否已通过低温启动线（开工资格）。</summary>
        public bool Started;

        /// <summary>是否正在高温等待降温。</summary>
        public bool HighTempWaiting;

        /// <summary>控制模式补热是否正在进行。</summary>
        public bool TopUpActive;

        public static RunState Fresh
        {
            get { return default(RunState); }
        }

        public override string ToString()
        {
            return "RunState[已开工 " + Started + "，高温等待 " + HighTempWaiting + "，补热中 " + TopUpActive + "]";
        }
    }

    /// <summary>一次运行判断的完整结论：生产许可、温度效率、加热意图与新的运行状态。</summary>
    public struct RunDecision
    {
        /// <summary>温区五态（用于显示与效率取值）。</summary>
        public TempState State;

        /// <summary>温度效率 η_T。</summary>
        public float Efficiency;

        /// <summary>温度条件是否允许生产。**不代表正在生产**：供电、自动化、操作工等条件另行判断。</summary>
        public bool AllowsProduction;

        /// <summary>本步的加热意图。</summary>
        public HeatingIntent Heating;

        /// <summary>该配方是否匹配到温区条目；false 表示退化为原版行为。</summary>
        public bool ZoneConfigured;

        /// <summary>本步结束后应保存的运行状态。</summary>
        public RunState Next;

        public static RunDecision Passthrough
        {
            get
            {
                RunDecision decision = default(RunDecision);
                decision.State = TempState.Disabled;
                decision.Efficiency = 1f;
                decision.AllowsProduction = true;
                decision.Heating = HeatingIntent.None;
                decision.ZoneConfigured = false;
                decision.Next = RunState.Fresh;
                return decision;
            }
        }
    }
}
