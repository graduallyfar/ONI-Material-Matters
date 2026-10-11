namespace MaterialOverhaul.Orders
{
    /// <summary>
    /// 一项订单：玩家添加的一次配方生产要求（建筑细节文档 §2.3.1）。
    /// 相同配方可以在不同位置重复添加，不自动合并到第一张同配方订单。
    /// </summary>
    public sealed class OrderEntry
    {
        /// <summary>
        /// 无限订单每轮的**占位**批次数。
        ///
        /// 已确认的规则是「无限订单完成本轮后让出，本轮批次数由玩家设定」，但原版界面只有「∞」开关、
        /// 没有额度输入，侧屏交互尚未实现（V0.0.1 开发计划 §7.5）。因此本次用 1 批/轮作占位：
        /// 无限订单每完成一批就让出给后面的订单，不会独占建筑。
        /// **这不是最终数值**，侧屏额度接入后由玩家设定值取代。
        /// </summary>
        public const int PlaceholderInfiniteBatchQuota = 1;

        /// <summary>原版配方标识（ComplexRecipe.id）。原版标识可读、可预测，直接复用，不新建平行配方系统。</summary>
        public string RecipeId;

        /// <summary>是否为无限订单。无限表示订单持续保留，不表示永远占用建筑。</summary>
        public bool IsInfinite;

        /// <summary>有限订单剩余数量（批次数），**包含正在生产的那一批**，与原版计数口径一致。</summary>
        public int Remaining;

        /// <summary>无限订单每轮连续生产的批次数（玩家设定额度）。</summary>
        public int BatchQuota;

        /// <summary>无限订单本轮已完成的批次数。中途缺料让出时保留，本轮完成后归零。</summary>
        public int BatchesDoneThisRound;

        private OrderEntry(string recipeId)
        {
            RecipeId = recipeId;
        }

        /// <summary>有限订单：生产指定批次数完成后移出队列。</summary>
        public static OrderEntry Limited(string recipeId, int remaining)
        {
            return new OrderEntry(recipeId)
            {
                IsInfinite = false,
                Remaining = remaining < 1 ? 1 : remaining,
                BatchQuota = 0,
                BatchesDoneThisRound = 0
            };
        }

        /// <summary>无限订单：每轮连续生产 batchQuota 个批次后让出，本轮进度随订单保留。</summary>
        public static OrderEntry Infinite(string recipeId, int batchQuota)
        {
            return new OrderEntry(recipeId)
            {
                IsInfinite = true,
                Remaining = 0,
                BatchQuota = batchQuota < 1 ? 1 : batchQuota,
                BatchesDoneThisRound = 0
            };
        }

        /// <summary>本订单是否已经完成并应移出队列。无限订单永不因数量完成而移出。</summary>
        public bool IsFinished
        {
            get { return !IsInfinite && Remaining <= 0; }
        }

        /// <summary>无限订单本轮是否已经用满额度（用满就要让出给后面的订单）。</summary>
        public bool IsRoundComplete
        {
            get { return IsInfinite && BatchesDoneThisRound >= BatchQuota; }
        }

        public override string ToString()
        {
            return IsInfinite
                ? "Order[" + RecipeId + ", 无限 本轮 " + BatchesDoneThisRound + "/" + BatchQuota + "]"
                : "Order[" + RecipeId + ", 剩余 " + Remaining + "]";
        }
    }
}
