using System.Threading;

namespace MaterialOverhaul.Orders
{
    /// <summary>
    /// 一项订单：玩家添加的一次配方生产要求（建筑细节文档 §2.3.1、V0.0.3 开发计划 §5.1）。
    /// 相同配方可以在不同位置重复添加，不自动合并到第一张同配方订单。
    ///
    /// <see cref="Id"/> 是订单的持久身份：数组下标会随增删移动，配方标识又会重复，
    /// 只有单调编号能在存档往返后仍然指向「玩家当初点的那一笔」。
    /// </summary>
    public sealed class OrderEntry
    {
        private static int nextId = 1;

        /// <summary>
        /// 无限订单每轮的默认额度（批／轮），本版暂定 1：无限订单每完成一批就让出给后面的订单，
        /// 不会独占建筑。玩家可在侧屏逐笔设为 1–99。
        /// </summary>
        public const int PlaceholderInfiniteBatchQuota = Core.Balance.DefaultInfiniteBatchQuota;

        /// <summary>持久身份编号（单调递增，同配方不同笔不重复）。</summary>
        public int Id;

        /// <summary>原版配方标识（ComplexRecipe.id）。原版标识可读、可预测，直接复用，不新建平行配方系统。</summary>
        public string RecipeId;

        /// <summary>是否为无限订单。无限表示订单持续保留，不表示永远占用建筑。</summary>
        public bool IsInfinite;

        /// <summary>有限订单剩余数量（批次数），**包含正在生产的那一批**，与原版计数口径一致。</summary>
        public int Remaining;

        /// <summary>无限订单每轮连续生产的批次数（玩家设定额度，1–99）。</summary>
        public int BatchQuota;

        /// <summary>无限订单本轮已完成的批次数。中途缺料让出时保留，本轮完成后归零。</summary>
        public int BatchesDoneThisRound;

        private OrderEntry(string recipeId)
        {
            Id = NewId();
            RecipeId = recipeId;
        }

        /// <summary>分配一个新的订单身份编号。</summary>
        public static int NewId()
        {
            // 身份只需在单个存档的会话内唯一；int 足够，且与 KSerialization 支持的类型一致
            // （原版从未序列化 long/double，用 long 会在落盘时静默失效）。
            int id = Interlocked.Increment(ref nextId);
            if (id <= 0)
            {
                // 溢出后退回小号段重新计数；实际使用量远达不到这一步。
                nextId = 1;
                id = 1;
            }

            return id;
        }

        /// <summary>保证编号非零（旧存档或外部构造的订单补齐身份）。返回 true 表示本次补发了编号。</summary>
        public bool EnsureId()
        {
            if (Id > 0)
            {
                return false;
            }

            Id = NewId();
            return true;
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

        /// <summary>存档恢复：按保存的身份与数量重建订单。</summary>
        public static OrderEntry Restored(string recipeId, int id, bool isInfinite, int remaining, int batchQuota, int roundDone)
        {
            OrderEntry entry = isInfinite
                ? Infinite(recipeId, batchQuota)
                : Limited(recipeId, remaining);
            entry.Id = id > 0 ? id : NewId();
            entry.BatchesDoneThisRound = roundDone < 0 ? 0 : roundDone;
            return entry;
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
                ? "Order#" + Id + "[" + RecipeId + ", 无限 本轮 " + BatchesDoneThisRound + "/" + BatchQuota + "]"
                : "Order#" + Id + "[" + RecipeId + ", 剩余 " + Remaining + "]";
        }
    }
}
