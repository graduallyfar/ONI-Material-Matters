using MaterialOverhaul.Core;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 一次供电判断的输入条件（V0.0.3 开发计划 §4.1、§4.2）。
    /// 「需要加热」和「正在加热」在这里分开：需求可以不获供电地存在，实际注热必须有电。
    /// </summary>
    public struct PowerDemandContext
    {
        /// <summary>是否存在已锁定批次。没有有效批次时整机 0 W。</summary>
        public bool HasLockedBatch;

        /// <summary>是否已确认获得供电（原版 <c>EnergyConsumer.IsPowered</c>）。</summary>
        public bool PowerAvailable;

        /// <summary>是否被手动禁用。</summary>
        public bool ManuallyDisabled;

        /// <summary>是否因损坏停机。</summary>
        public bool BrokenDown;

        /// <summary>温度条件是否允许生产（来自 <see cref="RunDecision.AllowsProduction"/>）。</summary>
        public bool TemperatureAllowsProduction;

        /// <summary>自动化是否给出生产许可（红信号为 false）。</summary>
        public bool AutomationAllowsProduction;

        /// <summary>是否已有操作工到位；非复制人操作的建筑视为恒真。</summary>
        public bool HasWorker;

        /// <summary>本步提出的加热意图（尚未扣除供电条件）。</summary>
        public HeatingIntent CommandedHeating;

        /// <summary>本建筑的共用加热设置（W）；0 缺省时使用默认值。</summary>
        public float ConfiguredHeatingWatts;
    }

    /// <summary>
    /// 建筑当前的电力需求与实际消费。纯逻辑，便于离线核对 §4.1 的整张耗电矩阵。
    ///
    /// 已确认口径：
    ///   - 生产用电只在**真实生产**时计：温度许可 + 自动化许可 + 操作工到位 + 有电，四者同时成立；
    ///   - 加热用电按加热意图计，**不需要操作工、不需要自动化许可**（红信号下仍可补热）；
    ///   - 断电、手动禁用、损坏停机时两者都为 0，但**需求**仍然保留，避免来电后不再请求。
    /// </summary>
    public struct PowerDemand
    {
        /// <summary>本步请求的生产用电（W）：未获供电时也保留，供电路重新分配。</summary>
        public float RequestedProductionWatts;

        /// <summary>本步请求的加热用电（W）。</summary>
        public float RequestedHeatingWatts;

        /// <summary>实际消费的生产用电（W）。</summary>
        public float UsedProductionWatts;

        /// <summary>实际消费的加热用电（W）。</summary>
        public float UsedHeatingWatts;

        /// <summary>实际允许注入热量的加热意图；断电时为 <see cref="HeatingIntent.None"/>。</summary>
        public HeatingIntent AppliedHeating;

        /// <summary>本步是否真的在推进生产（用于生产热、磨损与界面显示）。</summary>
        public bool ProductionRunning;

        public float RequestedTotalWatts
        {
            get { return RequestedProductionWatts + RequestedHeatingWatts; }
        }

        public float UsedTotalWatts
        {
            get { return UsedProductionWatts + UsedHeatingWatts; }
        }

        /// <summary>按 §4.1 计算本步的电力需求与消费。</summary>
        public static PowerDemand Evaluate(PowerDemandContext context)
        {
            PowerDemand demand = default(PowerDemand);

            if (!context.HasLockedBatch || context.ManuallyDisabled || context.BrokenDown)
            {
                // 没有有效批次、手动禁用或损坏停机：不生产也不加热，也不保留需求。
                return demand;
            }

            bool productionAllowedByMachine =
                context.TemperatureAllowsProduction && context.AutomationAllowsProduction && context.HasWorker;

            demand.RequestedProductionWatts = productionAllowedByMachine ? Balance.ProductionWatts : 0f;
            demand.RequestedHeatingWatts = HeatingPower.HeatingWattsFor(context.CommandedHeating, context.ConfiguredHeatingWatts);

            if (!context.PowerAvailable)
            {
                // 需求保留、消费为 0：不能为了不耗电先把需求清零。
                return demand;
            }

            demand.UsedProductionWatts = demand.RequestedProductionWatts;
            demand.UsedHeatingWatts = demand.RequestedHeatingWatts;
            demand.AppliedHeating = context.CommandedHeating;
            demand.ProductionRunning = demand.UsedProductionWatts > 0f;
            return demand;
        }
    }
}
