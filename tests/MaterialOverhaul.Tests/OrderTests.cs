using System.Collections.Generic;
using MaterialOverhaul.Orders;

namespace MaterialOverhaul.Tests
{
    public static class OrderTests
    {
        public static void Run()
        {
            QueueKeepsInsertionOrder();
            SkipsMissingMaterials();
            WaitsWhenNothingAvailable();
            SingleOrderWaits();
            LockedBatchIsNotPreempted();
            LimitedOrderLeavesQueue();
            InfiniteOrderYieldsAfterQuota();
            YieldKeepsRoundProgress();
            RemoveFixesCursorAndLock();
            RestoreKeepsOrder();
            PeekNextCandidate();
        }

        private static void PeekNextCandidate()
        {
            TestRunner.Section("候选取值（接入层映射用）");
            OrderQueue queue = Build("copper", "iron", "gold");
            TestRunner.CheckEqual("copper", queue.PeekNextRecipeId(), "初始候选为第一项");

            HashSet<string> ready = Ready("gold");
            queue.SelectNext(o => ready.Contains(o.RecipeId));
            TestRunner.CheckEqual("gold", queue.PeekNextRecipeId(), "缺料跳过后候选为选中的订单");

            OrderQueue empty = new OrderQueue();
            TestRunner.Check(empty.PeekNextRecipeId() == null, "空队列没有候选");

            OrderQueue restored = new OrderQueue();
            restored.Restore(new List<OrderEntry> { OrderEntry.Limited("a", 1), OrderEntry.Limited("b", 1) }, 1);
            TestRunner.CheckEqual("b", restored.PeekNextRecipeId(), "读档后候选按恢复的轮换位置");
        }

        private static OrderQueue Build(params string[] recipeIds)
        {
            OrderQueue queue = new OrderQueue();
            foreach (string id in recipeIds)
            {
                queue.Add(OrderEntry.Limited(id, 5));
            }

            return queue;
        }

        private static HashSet<string> Ready(params string[] ids)
        {
            return new HashSet<string>(ids);
        }

        private static void QueueKeepsInsertionOrder()
        {
            TestRunner.Section("按添加顺序建立订单");
            OrderQueue queue = Build("copper", "iron", "copper");
            TestRunner.CheckEqual(3, queue.Count, "相同配方重复添加不合并");
            TestRunner.CheckEqual("copper", queue.Orders[0].RecipeId, "第一项保持添加顺序");
            TestRunner.CheckEqual("iron", queue.Orders[1].RecipeId, "第二项保持添加顺序");
            TestRunner.CheckEqual("copper", queue.Orders[2].RecipeId, "第三项保持添加顺序");
        }

        private static void SkipsMissingMaterials()
        {
            TestRunner.Section("缺料向后检查");
            OrderQueue queue = Build("copper", "iron", "gold");
            HashSet<string> ready = Ready("gold");
            int index = queue.SelectNext(o => ready.Contains(o.RecipeId));
            TestRunner.CheckEqual(2, index, "跳过前两项缺料订单");
            TestRunner.CheckEqual(2, queue.Cursor, "指针停在选中的订单");

            queue.Lock(index);
            TestRunner.Check(queue.HasLockedBatch, "材料齐备后锁定批次");
            TestRunner.CheckEqual(2, queue.Cursor, "锁定不改变指针位置");
        }

        private static void WaitsWhenNothingAvailable()
        {
            TestRunner.Section("全部缺料进入等待");
            OrderQueue queue = Build("copper", "iron");
            HashSet<string> ready = Ready();
            TestRunner.CheckEqual(-1, queue.SelectNext(o => ready.Contains(o.RecipeId)), "全部缺料返回等待");
            TestRunner.CheckEqual(0, queue.Cursor, "等待时不移动指针（不空转）");

            ready.Add("iron");
            TestRunner.CheckEqual(1, queue.SelectNext(o => ready.Contains(o.RecipeId)), "材料变化后重新检查可选中");
        }

        private static void SingleOrderWaits()
        {
            TestRunner.Section("单一订单缺料");
            OrderQueue queue = Build("copper");
            TestRunner.CheckEqual(-1, queue.SelectNext(o => false), "单一订单缺料进入等待材料");
        }

        private static void LockedBatchIsNotPreempted()
        {
            TestRunner.Section("已锁定批次不被抢占");
            OrderQueue queue = Build("copper", "iron");
            queue.Lock(queue.SelectNext(o => o.RecipeId == "copper"));
            TestRunner.CheckEqual("copper", queue.LockedBatch.RecipeId, "锁定 copper");

            HashSet<string> lateArrival = Ready("iron");
            int index = queue.SelectNext(o => lateArrival.Contains(o.RecipeId));
            TestRunner.CheckEqual(0, index, "其他订单到料不抢占已锁定批次");
            TestRunner.CheckEqual("copper", queue.LockedBatch.RecipeId, "锁定批次保持不变");
        }

        private static void LimitedOrderLeavesQueue()
        {
            TestRunner.Section("有限订单完成与连续执行");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited("copper", 2));
            queue.Lock(0);

            queue.CompleteBatch();
            TestRunner.CheckEqual(1, queue.Count, "未完成时留在队列");
            TestRunner.CheckEqual(1, queue.Orders[0].Remaining, "剩余数量逐批扣减");
            TestRunner.Check(!queue.HasLockedBatch, "完成一批后解锁");

            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "剩余数量仍可继续（连续执行同一订单）");
            queue.Lock(0);
            queue.CompleteBatch();
            TestRunner.CheckEqual(0, queue.Count, "数量完成后移出队列");
        }

        private static void InfiniteOrderYieldsAfterQuota()
        {
            TestRunner.Section("无限订单完成本轮后让出");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Infinite("copper", 2));
            queue.Add(OrderEntry.Limited("iron", 1));

            queue.Lock(0);
            TestRunner.CheckEqual(2, queue.LockedBatch.BatchQuota, "无限订单额度为 2");
            queue.CompleteBatch();
            TestRunner.CheckEqual(1, queue.Orders[0].BatchesDoneThisRound, "本轮进度累加");
            queue.Lock(0);
            queue.CompleteBatch();
            TestRunner.CheckEqual(0, queue.Orders[0].BatchesDoneThisRound, "本轮完成后额度归零");
            TestRunner.CheckEqual(1, queue.Cursor, "完成本轮后让出，指针移到后面的订单");
            TestRunner.CheckEqual(1, queue.SelectNext(o => true), "让出后可选中后面的订单");
        }

        private static void YieldKeepsRoundProgress()
        {
            TestRunner.Section("无限订单中途让出保留进度");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Infinite("copper", 3));
            queue.Add(OrderEntry.Limited("iron", 1));

            queue.Lock(0);
            queue.CompleteBatch();
            queue.Lock(0);
            queue.Yield();
            TestRunner.CheckEqual(1, queue.Orders[0].BatchesDoneThisRound, "中途让出保留本轮剩余批次数");
            TestRunner.CheckEqual(1, queue.Cursor, "让出后指针指向后面的订单");

            queue.Yield();
            TestRunner.CheckEqual(1, queue.Orders[0].BatchesDoneThisRound, "多次让出不清除本轮进度");
        }

        private static void RemoveFixesCursorAndLock()
        {
            TestRunner.Section("取消订单后的指针修正");
            OrderQueue queue = Build("copper", "iron", "gold");
            queue.Lock(2);
            queue.RemoveAt(0);
            TestRunner.CheckEqual(2, queue.Count, "订单被移除");
            TestRunner.CheckEqual(1, queue.LockedIndex, "已锁定下标随之左移");
            TestRunner.CheckEqual("gold", queue.LockedBatch.RecipeId, "锁定批次仍是原订单");

            queue.RemoveAt(1);
            TestRunner.Check(!queue.HasLockedBatch, "取消当前订单后清除锁定批次");
            TestRunner.CheckEqual(1, queue.Count, "队列剩下 iron");
            TestRunner.CheckEqual("iron", queue.Orders[0].RecipeId, "剩余订单顺序不变");
        }

        private static void RestoreKeepsOrder()
        {
            TestRunner.Section("存档恢复");
            OrderQueue queue = new OrderQueue();
            List<OrderEntry> entries = new List<OrderEntry>
            {
                OrderEntry.Limited("copper", 3),
                OrderEntry.Infinite("iron", 2),
                OrderEntry.Limited("gold", 1)
            };
            queue.Restore(entries, 2);
            TestRunner.CheckEqual(3, queue.Count, "恢复全部订单");
            TestRunner.CheckEqual("iron", queue.Orders[1].RecipeId, "恢复后顺序不变");
            TestRunner.CheckEqual(2, queue.Cursor, "恢复轮换位置");
            TestRunner.Check(!queue.HasLockedBatch, "读档后无锁定批次，需重新校验");

            queue.Restore(new List<OrderEntry>(), 5);
            TestRunner.CheckEqual(0, queue.Count, "空队列恢复为空");
            TestRunner.CheckEqual(0, queue.Cursor, "空队列指针归零");
        }
    }
}
