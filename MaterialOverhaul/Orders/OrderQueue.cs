using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Orders
{
    /// <summary>
    /// 订单队列：后端由原版的「配方计数字典」改为有序列表，轮换按玩家添加顺序进行
    /// （建筑细节文档 §2.3.2、V0.0.1 开发计划 §7.3）。
    ///
    /// 纯逻辑实现，材料齐备判断由调用方以委托注入，因此不依赖游戏对象。
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
                cursor = 0;
            }

            return true;
        }

        /// <summary>
        /// 轮换问询：从当前指针按添加顺序检查，返回第一个材料齐备的订单下标；
        /// 全部缺料时返回 -1 进入等待，且不移动指针（材料或订单变化后再检查，不反复空转）。
        ///
        /// 温度不足不作为跳过条件——温度只决定已锁定批次是加热还是生产（§2.3.3）。
        /// 单一订单缺料同样返回 -1（§2.3.2 共同规则 6）。
        /// </summary>
        /// <param name="hasMaterials">给定订单的原料是否齐备；判断时须把输入存储与加工存储合并计算。</param>
        public int SelectNext(Func<OrderEntry, bool> hasMaterials)
        {
            if (hasMaterials == null)
            {
                throw new ArgumentNullException(nameof(hasMaterials));
            }

            if (orders.Count == 0)
            {
                return -1;
            }

            if (HasLockedBatch)
            {
                // 已锁定批次在完成或取消前保持不变。
                return lockedIndex;
            }

            for (int step = 0; step < orders.Count; step++)
            {
                int index = (cursor + step) % orders.Count;
                if (hasMaterials(orders[index]))
                {
                    // 指针越过中间缺料的订单；那些订单保留在队列中且保留本轮进度。
                    cursor = index;
                    return index;
                }
            }

            return -1;
        }

        /// <summary>材料齐备后锁定本批次。锁定发生在调温之前，不是正式生产时才开始（§2.3.3）。</summary>
        public bool Lock(int index)
        {
            if (index < 0 || index >= orders.Count)
            {
                return false;
            }

            OrderEntry entry = orders[index];
            if (entry.IsInfinite && entry.BatchesDoneThisRound >= entry.BatchQuota)
            {
                // 上一轮已完成但尚未归零时，从新一轮重新使用设定额度。
                entry.BatchesDoneThisRound = 0;
            }

            lockedIndex = index;
            return true;
        }

        /// <summary>
        /// 完成一个批次。有限订单扣减剩余数量，完成后移出队列；
        /// 无限订单累加本轮进度，达到额度后让出并归零，下一次轮到它时重新使用设定额度。
        /// 返回本次被完成的订单（可能已被移出队列）。
        /// </summary>
        public OrderEntry CompleteBatch()
        {
            if (!HasLockedBatch)
            {
                return null;
            }

            OrderEntry entry = orders[lockedIndex];

            if (entry.IsInfinite)
            {
                entry.BatchesDoneThisRound++;
                if (entry.BatchesDoneThisRound >= entry.BatchQuota)
                {
                    // 完成本轮后必须让出，继续检查后面的订单。
                    entry.BatchesDoneThisRound = 0;
                    cursor = (lockedIndex + 1) % orders.Count;
                }
            }
            else
            {
                entry.Remaining--;
                if (entry.Remaining <= 0)
                {
                    int index = lockedIndex;
                    lockedIndex = -1;
                    RemoveAt(index);
                    return entry;
                }
            }

            lockedIndex = -1;
            return entry;
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

            int from = HasLockedBatch ? lockedIndex : cursor;
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
