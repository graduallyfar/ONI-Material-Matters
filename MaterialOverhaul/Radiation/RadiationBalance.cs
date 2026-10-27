namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 辐射「累积 → 显现」的集中数值。全部为工作初值，游戏平衡未验收。
    ///
    /// 任何物质都把受到的照射累积成累积量 D（rads），按半衰期衰减；D 超过材料的显现限度 L 后开始放射，
    /// 成为新的来源。建筑（含建造砖块）的累积量记在自己身上、显现后挂放射源；格子里的物质、管道里的流体、
    /// 地上的物品记在每格累积表里，显现后写入原版放射性污染物。原版放射物质本身就是来源，不累积。
    /// </summary>
    public static class RadiationBalance
    {
        /// <summary>原版一个周期的秒数。</summary>
        public const float SecondsPerCycle = 600f;

        /// <summary>累积量的半衰期（周期），所有物质相同。</summary>
        public const float AccumulationHalfLifeCycles = 10f;

        /// <summary>自身放射低于此值（rads/周期）时忽略不计：不挂放射源，也不写入污染物。</summary>
        public const float EmitMinRads = 1f;

        /// <summary>显现建筑自身放射的半径（格）。</summary>
        public const int EmitRadiusCells = 2;

        /// <summary>自身放射变化超过这个比例才重新通知原生 Sim，减少消息数量。</summary>
        public const float EmitRefreshRelativeChange = 0.05f;

        /// <summary>未受照且累积量低于此值（rads）时清零，不再跟踪。</summary>
        public const float AccumulationDropRads = 1f;

        /// <summary>累积量的数值上限（rads），防止大片显现物互相照射时无限增长。</summary>
        public const float MaxAccumulationRads = 1000000f;

        /// <summary>存档只保存不低于此值的累积量（rads）。</summary>
        public const float SaveMinRads = 1f;

        /// <summary>建筑累积量达到显现限度的这个比例时，在状态栏显示累积进度。</summary>
        public const float AccumulatingStatusFraction = 0.5f;

        /// <summary>液体、气体的显现限度（rads）；显现后按污染物放射。</summary>
        public const float FluidLimitRads = 3000f;

        /// <summary>
        /// 污染物添加系数：每吨物质、每 1 rads/周期 超出限度的放射、每秒添加的污染物个数。
        /// 按原版 1 周期半衰期，1 吨物质的稳态自身放射约为超出部分的 3%。
        /// </summary>
        public const float ContaminantsPerTonRadSecond = 0.035f;

        /// <summary>气体每格只有 1–2 kg，按真实质量几乎不会被污染；按质量的 50 倍计算。</summary>
        public const float GasMassMultiplier = 50f;

        /// <summary>单格污染物上限；达到后不再添加，防止整数溢出。</summary>
        public const int MaxCellContaminants = 10000000;

        /// <summary>原版放射性污染物：每 1000 个产生 1 rads/周期（wiki 数据；换算在原生 Sim 中，待游戏验证）。</summary>
        public const float ContaminantRadsPerThousand = 1f;

        /// <summary>原版放射性污染物的半衰期（周期，已在程序集核实）。</summary>
        public const float ContaminantHalfLifeCycles = 1f;

        /// <summary>
        /// 格子物质（流体、天然格）被辐射粒子发生器付账时使用的放射比例，
        /// 与污染物添加系数对应：1 吨物质稳态约放出超出部分的 3%。
        /// </summary>
        public const float CellDrainFraction = 0.03f;

        /// <summary>
        /// 1 点辐射粒子能量对应的累积量（rads）。原版发生器每吸收 10 rads 产生 1 点粒子，
        /// 粒子撞击固体时按同一比例换回累积量。
        /// </summary>
        public const float RadsPerParticle = 10f;

        /// <summary>吸入：每吸入 1000 个污染物增加的辐射剂量（rads）。</summary>
        public const float InhaleRadsPerThousand = 0.06f;

        /// <summary>接触：身上每 1000 个污染物每周期增加的辐射剂量（rads），约为照射的四分之一。</summary>
        public const float ContactRadsPerThousandPerCycle = 0.25f;

        /// <summary>
        /// 拆除显现建筑时转成核废料的比例：自身放射 ÷ 这个值（设计评审 2026-10-23，用户选定）。
        /// 例如自身放射 275 rads/周期的钢墙，拆除时 27.5% 的主建造材料变成核废料。
        /// </summary>
        public const float DecommissionRadsPerFullConversion = 1000f;

        /// <summary>拆除时转成核废料的比例上限。</summary>
        public const float DecommissionMaxFraction = 0.5f;

        /// <summary>自身放射低于此值（rads/周期）的建筑拆除时不转换核废料。</summary>
        public const float DecommissionMinRads = 10f;

        /// <summary>完整扫描一遍世界格子的周期（秒）。</summary>
        public const float ScanPassSeconds = 5f;

        /// <summary>每 0.2 秒扫描一份，五秒扫完一遍，即每遍 25 份。</summary>
        public const int ScanSlicesPerPass = 25;

        /// <summary>复制人接触剂量与发生器付账的结算间隔（秒）。</summary>
        public const float SettleIntervalSeconds = 1f;
    }
}
