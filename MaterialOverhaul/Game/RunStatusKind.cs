namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 运行状态的分类。用于信息卡文字与日志，取值本身不参与规则运算。
    ///
    /// V0.1.2 按建筑细节文档 §2.3.5 与 V0.1.2 开发计划 §4.2 重排：
    ///   - 温度限制与温度效率分开表达：「高温保护」是超过建筑上限，「高温低效生产」只是越过配方最佳上限；
    ///   - 「等待升温」不等于「预热中」：只有真的在注热才写预热中；
    ///   - 「温度受限 · 生产暂停」用于最佳温区不可达（`B₁ &gt; U`）且已经到达受限目标的情况。
    /// </summary>
    public enum RunStatusKind
    {
        None = 0,

        /// <summary>没有订单，也没有当前批次。</summary>
        NoTask = 1,

        /// <summary>有订单但全部缺料，等待材料。</summary>
        WaitingMaterials = 2,

        /// <summary>损坏停机。</summary>
        BrokenDown = 3,

        /// <summary>被玩家手动禁用。</summary>
        ManuallyDisabled = 4,

        /// <summary>有加热需求但未获供电，等待供电。</summary>
        Unpowered = 5,

        /// <summary>高温保护：超过建筑上限，等待降温。</summary>
        HighTempWaiting = 6,

        /// <summary>预热中（确实在注热）。</summary>
        Preheat = 7,

        /// <summary>补热中（确实在注热）。</summary>
        TopUp = 8,

        /// <summary>强制运行持续加热（确实在注热）。</summary>
        Sustained = 9,

        /// <summary>自动化或其它非温度条件不允许生产。</summary>
        AutomationPaused = 10,

        /// <summary>等待操作工。</summary>
        WaitingWorker = 11,

        /// <summary>正常生产中。</summary>
        Producing = 12,

        /// <summary>低温低效生产中。</summary>
        ProducingLow = 13,

        /// <summary>高温低效生产中。</summary>
        ProducingHigh = 14,

        /// <summary>需要升温但当前没有实际注热（不假称为预热中）。</summary>
        WaitingHeat = 15,

        /// <summary>等待降温（控制模式越过最佳上限但未超过建筑上限，或处于恢复回差内）。</summary>
        WaitingCool = 16,

        /// <summary>最佳温区不可达且已到达受限目标：温度受限 · 生产暂停。</summary>
        TemperatureLimited = 17
    }
}
