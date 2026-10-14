namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 一条配方声明的温区（单位：开尔文）——**只有配方最佳温区**（V0.1.2 开发计划 §1.1、§2.1）。
    ///
    /// V0.1.2 起温区归属拆成两处，本结构只承担配方那一半：
    ///   - `[BestMin, BestMax]`：配方最佳温区，只由配方决定，换建材、换建筑都不变；
    ///   - 最低工作温度 L 由**建筑类型**决定，见 <see cref="BuildingZoneTable"/>；
    ///   - 最高工作温度 U 由**主建造材料**决定（`0.75 × 熔点(K)`），见
    ///     <see cref="MaterialOverhaul.Heating.OverheatRule"/> 与 <see cref="HeatCeiling"/>。
    ///
    /// 因此这里不再有 WorkMin／WorkMax，也不再有「四边界必须升序」的旧假设：
    /// `BestMax > U` 与 `BestMin > U` 都是建材限制造成的正常情况，不是配置错误。
    ///
    /// 结构本身不参与游戏运算的边界裁剪：把 L、配方最佳区、U 合成可运算温区的是
    /// <see cref="ResolvedZone"/>／<see cref="TempZoneResolver"/>。
    /// </summary>
    public struct TempZone
    {
        /// <summary>最佳温区下限 B₁。要求 B₁ ≥ 建筑最低工作温度 L。</summary>
        public float BestMin;

        /// <summary>最佳温区上限 B₂。要求 B₁ &lt; B₂；高于建筑上限 U 是合法情况。</summary>
        public float BestMax;

        /// <summary>本条配方是否已经登记温度数值；false 表示按原版行为运行。</summary>
        public bool Configured;

        public TempZone(float bestMin, float bestMax)
        {
            BestMin = bestMin;
            BestMax = bestMax;
            Configured = true;
        }

        /// <summary>未配置：判断恒通过、效率恒为 1，行为等同原版。</summary>
        public static TempZone Unconfigured
        {
            get { return default(TempZone); }
        }

        /// <summary>最佳温区是否是两个有限值且严格升序（B₁ &lt; B₂）。</summary>
        public bool IsOrdered
        {
            get { return IsFinite(BestMin) && IsFinite(BestMax) && BestMin < BestMax; }
        }

        /// <summary>按摄氏度书写两个边界，统一在这里换算为开尔文。</summary>
        public static TempZone Celsius(float bestMinCelsius, float bestMaxCelsius)
        {
            return new TempZone(
                Core.Temperatures.FromCelsius(bestMinCelsius),
                Core.Temperatures.FromCelsius(bestMaxCelsius));
        }

        public override string ToString()
        {
            if (!Configured)
            {
                return "TempZone(Unconfigured)";
            }

            return "TempZone[best " + BestMin.ToString("F2") + ".." + BestMax.ToString("F2") + "]";
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// 主建造材料的温度上限来源。判断层不引用任何游戏类型，只接收一个取值委托：
    /// 目前由 <c>MaterialOverhaulFab</c> 传入「主建造材料熔点 × 过热系数 = 实际过热阈值」。
    ///
    /// 返回 false 表示熔点不可用或建筑上限不高于建筑下限：此时该建筑不启用本 Mod 温控，
    /// 回到原版行为，**不产生无限上限**（V0.1.2 开发计划 §7 第 4、5 项）。
    /// </summary>
    public delegate bool HeatCeilingResolver(out float ceilingKelvin);
}
