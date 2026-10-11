using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Orders
{
    /// <summary>
    /// 订单队列：后端由原版的「配方计数字典」改为有序列表，轮换按玩家添加顺序进行
    /// （建筑细节文档 §2.3.2、V0.0.1 开发计划 §7.3、V0.0.2 开发计划 §3）。
    ///
    /// 纯逻辑实现，材料齐备判断由调用方以委托注入，因此不依赖游戏对象。
    ///
    /// 三个概念（§2.3.1）：
    ///   - 订单：这里的一项 <see cref="OrderEntry"/>，同一配方可在不同位置重复；
    ///   - 批次：一次投料与结算，对应原版的一次 CompleteWorkingOrder；
    ///   - 轮次：无限订单本轮连续生产的批次额度，用完让出。
    ///
    /// 与已锁定批次的关系（§2.3.3）：
    ///   - 轮换指针只被**材料缺料**的订单推动；温度状态不影响指针位置。
    ///   - 已锁定批次在完成或取消之前保持不变；其间指针轮换不改变它。
    /// </summary>
    public sealed class OrderQueue
    {
        private readonly List<OrderEntry> orders = new List<OrderEntry>();
        private int cursor;
        private int lockedIndex = -1;

        /// <summary>只读订单视图（按玩家添加顺序）。</summary>
        public IList<OrderEntry> Orders
        {
            get { return orders.AsReadOnly(); }
        }

        public int Count
        {
            get { return orders.Count; }
        }

        /// <summary>当前轮换位置。</summary>
        public int Cursor
        {
            get { return cursor; }
        }

        /// <summary>已锁定批次的订单下标；-1 表示当前没有锁定批次。</summary>
        public int LockedIndex
        {
            get { return lockedIndex; }
        }

        public bool HasLockedBatch
        {
            get { return lockedIndex >= 0 && lockedIndex < orders.Count; }
        }

        public OrderEntry LockedBatch
        {
            get { return HasLockedBatch ? orders[lockedIndex] : null; }
        }

        // ---- 订单增删（原版界面操作的映射，§2.3.4）----

        /// <summary>追加订单到末尾。按玩家添加顺序，不合并同配方。</summary>
        public int Add(OrderEntry order)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            orders.Add(order);
            return orders.Count - 1;
        }

        /// <summary>
        /// 原版「+」一次 = 一笔 1 批的有限订单，追加到末尾。
        /// 同一配方重复点击就是多笔订单，不合并到第一笔。
        /// </summary>
        public int AddBatchRequest(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return -1;
            }

            return Add(OrderEntry.Limited(recipeId, 1));
        }

        /// <summary>把某配方标记为无限：替换该配方的全部订单为一笔无限订单，位置沿用原来第一笔的位置。</summary>
        public bool ReplaceRecipeWithInfinite(string recipeId, int quota)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return false;
            }

            return ReplaceRecipe(recipeId, OrderEntry.Infinite(recipeId, quota));
        }

        /// <summary>把某配方的数量设为固定批次数：替换该配方的全部订单为一笔有限订单，位置沿用原来第一笔的位置。</summary>
        public bool ReplaceRecipeWithBatches(string recipeId, int batches)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return false;
            }

            if (batches <= 0)
            {
                return RemoveAllOfRecipe(recipeId);
            }

            return ReplaceRecipe(recipeId, OrderEntry.Limited(recipeId, batches));
        }

        /// <summary>
        /// 原版「−」一次：对最后一笔同配方订单减 1 批，减到 0 或本身是无限订单则整笔移除。
        /// 返回 false 表示队列里没有该配方的订单。
        /// </summary>
        public bool DecrementLastBatch(string recipeId)
        {
            int index = LastIndexOfRecipe(recipeId);
            if (index < 0)
            {
                return false;
            }

            OrderEntry entry = orders[index];
            if (entry.IsInfinite || entry.Remaining <= 1)
            {
                RemoveAt(index);
                return true;
            }

            entry.Remaining--;
            return true;
        }

        /// <summary>移除某配方的全部订单（原版数量设为 0；取消当前订单时由调用方重新选择下一订单）。</summary>
        public bool RemoveAllOfRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return false;
            }

            return RemoveAllOfRecipeInternal(recipeId) >= 0;
        }

        /// <summary>
        /// 从队列移除订单，并修正轮换指针与已锁定下标。
        /// 调用方负责：取消当前批次时退回尚未消耗的原料并清除批次进度与温区目标（§2.3.4）。
        /// </summary>
        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= orders.Count)
            {
                return false;
            }

            orders.RemoveAt(index);

            if (orders.Count == 0)
            {
                cursor = 0;
                lockedIndex = -1;
                return true;
            }

            if (lockedIndex == index)
            {
                // 当前订单被取消：批次与温区目标失效，等调用方重新选择。
                lockedIndex = -1;
            }
            else if (lockedIndex > index)
            {
                lockedIndex--;
            }

            if (cursor > index)
            {
                cursor--;
            }
            else if (cursor == index && cursor >= orders.Count)
            {
                // 移除的是末尾项：回到开头继续检查。
                cursor = 0;
            }

            return true;
        }

        // ---- 查询 ----

        public bool ContainsRecipe(string recipeId)
        {
            return LastIndexOfRecipe(recipeId) >= 0;
        }

        /// <summary>该配方是否存在无限订单（用于原版「−」时不丢掉无限订单的本轮进度）。</summary>
        public bool HasInfiniteOrder(string recipeId)
        {
            foreach (OrderEntry entry in orders)
            {
                if (entry.IsInfinite && entry.RecipeId == recipeId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>该配方在原版计数口径下的总需求。队列里没有该配方时返回空。</summary>
        public RecipeTotal TotalFor(string recipeId)
        {
            RecipeTotal total = default(RecipeTotal);
            if (string.IsNullOrEmpty(recipeId))
            {
                return total;
            }

            foreach (OrderEntry entry in orders)
            {
                if (entry.RecipeId != recipeId)
                {
                    continue;
                }

                if (entry.IsInfinite)
                {
                    total.Infinite = true;
                }
                else
                {
                    total.Batches += entry.Remaining;
                }
            }

            return total;
        }

        /// <summary>
        /// 轮换问询：从当前轮换位置按添加顺序检查，返回第一个可执行订单下标；
        /// 全部不可执行时返回 -1 进入等待，且不移动指针（材料或订单变化后再检查，不反复空转）。
        ///
        /// 温度不足不作为跳过条件——温度只决定已锁定批次是加热还是生产（§2.3.3）。
        /// 单一订单缺料同样返回 -1（§2.3.2 共同规则 6）。
        /// </summary>
        /// <param name="isAvailable">
        /// 给定订单当前是否可执行：配方存在、还有剩余批次、原料齐备。
        /// 判断时须把输入存储与加工存储合并计算，并由调用方保证已投料批次视为齐备（§2.3.3）。
        /// </param>
        public int SelectNext(Func<OrderEntry, bool> isAvailable)
        {
            if (isAvailable == null)
            {
                throw new ArgumentNullException(nameof(isAvailable));
            }

            if (orders.Count == 0)
            {
                return -1;
            }

            int start = NormalizeCursor();
            for (int step = 0; step < orders.Count; step++)
            {
                int index = (start + step) % orders.Count;
                if (isAvailable(orders[index]))
                {
                    // 指针越过中间缺料的订单；那些订单保留在队列中且保留本轮进度。
                    cursor = index;
                    return index;
                }
            }

            return -1;
        }

        /// <summary>本 Mod 顺序表中当前候选订单的标识（从轮换位置开始）。</summary>
        public string PeekNextRecipeId()
        {
            if (orders.Count == 0)
            {
                return null;
            }

            int index = NormalizeCursor();
            return orders[index].RecipeId;
        }

        // ---- 批次锁定与完成（§2.3.3）----

        /// <summary>材料齐备后按顺序表下标锁定本批次。锁定发生在调温之前，不是正式生产时才开始。</summary>
        public bool Lock(int index)
        {
            if (index < 0 || index >= orders.Count)
            {
                return false;
            }

            ResetRoundIfComplete(orders[index]);
            lockedIndex = index;
            return true;
        }

        /// <summary>
        /// 按配方标识锁定开始生产的订单：优先轮换位置上的那一笔，其次队列中第一笔同配方订单。
        /// 原版只按配方计数，本 Mod 按订单区分，因此重复配方必须靠轮换位置确定是哪一笔。
        /// </summary>
        public bool LockForStart(string recipeId)
        {
            int index = IndexOfInProgressOrder(recipeId);
            if (index < 0)
            {
                return false;
            }

            ResetRoundIfComplete(orders[index]);
            lockedIndex = index;
            return true;
        }

        /// <summary>
        /// 完成一个批次（调用方传入原版刚刚完成的配方标识）：
        /// 有限订单扣减剩余数量，完成后移出队列；无限订单累加本轮进度，达到额度后让出并归零。
        /// 返回本次被完成的订单，找不到对应订单时返回 null。
        /// </summary>
        public OrderEntry CompleteBatch(string recipeId)
        {
            int index = IndexOfInProgressOrder(recipeId);
            if (index < 0)
            {
                return null;
            }

            OrderEntry entry = orders[index];

            if (entry.IsInfinite)
            {
                entry.BatchesDoneThisRound++;
                if (entry.BatchesDoneThisRound >= entry.BatchQuota)
                {
                    // 完成本轮后必须让出，继续检查后面的订单；下一轮重新使用设定额度。
                    entry.BatchesDoneThisRound = 0;
                    cursor = (index + 1) % orders.Count;
                }

                lockedIndex = -1;
                return entry;
            }

            entry.Remaining--;
            lockedIndex = -1;

            if (entry.Remaining <= 0)
            {
                // 有限订单完成：继续从后面检查，不每次重新从队首开始。
                RemoveAt(index);
            }

            return entry;
        }

        /// <summary>完成已锁定批次（调用方没有单独的配方信息时使用）。</summary>
        public OrderEntry CompleteBatch()
        {
            return HasLockedBatch ? CompleteBatch(orders[lockedIndex].RecipeId) : null;
        }

        /// <summary>
        /// 中途让出（缺料或暂停批次）：保留订单与剩余数量/本轮进度，
        /// 指针停在本订单之后，下次轮到它时继续剩余部分。返回下一个候选下标。
        /// </summary>
        public int Yield()
        {
            if (orders.Count == 0)
            {
                lockedIndex = -1;
                return -1;
            }

            int from = HasLockedBatch ? lockedIndex : NormalizeCursor();
            lockedIndex = -1;
            cursor = (from + 1) % orders.Count;
            return cursor;
        }

        /// <summary>存档恢复：重建顺序与指针位置（§8.3，读档不得重排订单）。</summary>
        public void Restore(IEnumerable<OrderEntry> entries, int restoredCursor)
        {
            orders.Clear();
            if (entries != null)
            {
                foreach (OrderEntry entry in entries)
                {
                    if (entry != null)
                    {
                        orders.Add(entry);
                    }
                }
            }

            cursor = orders.Count == 0 ? 0 : Clamp(restoredCursor, 0, orders.Count - 1);
            lockedIndex = -1;
        }

        // ---- 内部 ----

        /// <summary>
        /// 把某配方的全部订单换成给定的一笔：**原地改写第一笔**并移除同配方的其余订单，
        /// 因此该订单的位置不变（§2.3.1：修改已有订单数量不改变它的位置），
        /// 轮换指针与已锁定下标除被移除的重复项外也不需要修正。
        /// </summary>
        private bool ReplaceRecipe(string recipeId, OrderEntry replacement)
        {
            int first = FirstIndexOfRecipe(recipeId);
            if (first < 0)
            {
                // 原来没有该配方的订单：追加到末尾，位置等于玩家此刻添加。
                Add(replacement);
                return true;
            }

            OrderEntry target = orders[first];
            target.IsInfinite = replacement.IsInfinite;
            target.Remaining = replacement.Remaining;
            target.BatchQuota = replacement.BatchQuota;
            target.BatchesDoneThisRound = replacement.BatchesDoneThisRound;

            for (int i = orders.Count - 1; i > first; i--)
            {
                if (orders[i].RecipeId == recipeId)
                {
                    RemoveAt(i);
                }
            }

            return true;
        }

        private int FirstIndexOfRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return -1;
            }

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].RecipeId == recipeId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>移除某配方的全部订单，返回被移除项中最小的下标；没有移除任何项时返回 -1。</summary>
        private int RemoveAllOfRecipeInternal(string recipeId)
        {
            int first = FirstIndexOfRecipe(recipeId);
            if (first < 0)
            {
                return -1;
            }

            for (int i = orders.Count - 1; i >= first; i--)
            {
                if (orders[i].RecipeId == recipeId)
                {
                    RemoveAt(i);
                }
            }

            return first;
        }

        private int LastIndexOfRecipe(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId))
            {
                return -1;
            }

            for (int i = orders.Count - 1; i >= 0; i--)
            {
                if (orders[i].RecipeId == recipeId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>已锁定批次优先；否则从轮换位置按添加顺序找第一笔同配方订单。</summary>
        private int IndexOfInProgressOrder(string recipeId)
        {
            if (string.IsNullOrEmpty(recipeId) || orders.Count == 0)
            {
                return -1;
            }

            if (HasLockedBatch && orders[lockedIndex].RecipeId == recipeId)
            {
                return lockedIndex;
            }

            int start = NormalizeCursor();
            for (int step = 0; step < orders.Count; step++)
            {
                int index = (start + step) % orders.Count;
                if (orders[index].RecipeId == recipeId)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ResetRoundIfComplete(OrderEntry entry)
        {
            if (entry.IsInfinite && entry.BatchesDoneThisRound >= entry.BatchQuota)
            {
                // 上一轮已完成但尚未归零时，从新一轮重新使用设定额度。
                entry.BatchesDoneThisRound = 0;
            }
        }

        private int NormalizeCursor()
        {
            if (cursor < 0 || cursor >= orders.Count)
            {
                cursor = 0;
            }

            return cursor;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
