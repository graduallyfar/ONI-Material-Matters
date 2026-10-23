namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 感生放射性的集中数值（辐射系统设计方案 §8、§9）。全部为工作初值，游戏平衡未验收。
    ///
    /// 建筑（含建造砖块）用活化度表示自身放射，液体、气体、天然格用原版放射性污染物病菌表示；
    /// 两者共用同一个受照门槛。门槛高于活化物自身的放射，避免活化物之间互相照射、越滚越大。
    /// </summary>
    public static class RadiationBalance
    {
        /// <summary>原版一个周期的秒数。</summary>
        public const float SecondsPerCycle = 600f;

        /// <summary>活化门槛（rads/周期）：只有超出这部分的辐射才会造成活化或污染。</summary>
        public const float ActivationThresholdRads = 500f;

        /// <summary>建筑活化度的半衰期（周期）。</summary>
        public const float BuildingHalfLifeCycles = 10f;

        /// <summary>活化度低于此值（rads/周期）时不挂自身放射源，避免大量微弱辐射源。</summary>
        public const float EmitMinRads = 10f;

        /// <summary>活化建筑自身放射的半径（格）。</summary>
        public const int EmitRadiusCells = 2;

        /// <summary>
        /// 估算活化建筑对周围格子贡献时使用的搜索半径（格）。
        /// 比放射半径多 2 格，覆盖多格建筑的原点到边缘的偏移，宁可多扣、不可少扣。
        /// </summary>
        public const int InducedSearchRadiusCells = EmitRadiusCells + 2;

        /// <summary>自身放射变化超过这个比例才重新通知原生 Sim，减少消息数量。</summary>
        public const float EmitRefreshRelativeChange = 0.05f;

        /// <summary>活化度低于此值且本轮未受照时，从活跃名单移除并清零。</summary>
        public const float ActivationDropRads = 0.5f;

        /// <summary>
        /// 污染物添加系数：每吨物质、每 1 rads/周期 超额辐射、每秒添加的污染物个数。
        /// 按原版 1 周期半衰期，1 吨水在 6000 rads/周期 下稳态约 16.5 万个，自身放射约 165 rads/周期。
        /// </summary>
        public const float ContaminantsPerTonRadSecond = 0.035f;

        /// <summary>气体每格只有 1–2 kg，按真实质量几乎不会被污染；按质量的 50 倍计算。</summary>
        public const float GasMassMultiplier = 50f;

        /// <summary>单格污染物上限；达到后不再添加，防止整数溢出。</summary>
        public const int MaxCellContaminants = 10000000;

        /// <summary>原版放射性污染物：每 1000 个产生 1 rads/周期（wiki 数据；换算在原生 Sim 中，托管程序集查不到，待游戏验证）。半衰期 1 周期已在程序集核实。</summary>
        public const float ContaminantRadsPerThousand = 1f;

        /// <summary>
        /// 拆除活化建筑时转成核废料的比例：活化度 ÷ 这个值（设计评审 2026-10-23，用户选定）。
        /// 例如活化度 275 rads/周期的钢墙，拆除时 27.5% 的主建造材料变成核废料。
        /// </summary>
        public const float DecommissionRadsPerFullConversion = 1000f;

        /// <summary>拆除时转成核废料的比例上限。</summary>
        public const float DecommissionMaxFraction = 0.5f;

        /// <summary>完整扫描一遍世界格子的周期（秒）；每秒扫描其中五分之一。</summary>
        public const float ScanPassSeconds = 5f;

        /// <summary>每次 1 秒更新扫描的份数，与 <see cref="ScanPassSeconds"/> 对应。</summary>
        public const int ScanSlicesPerPass = 5;
    }
}
