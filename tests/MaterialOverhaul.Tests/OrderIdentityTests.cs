using System;
using System.Collections.Generic;
using System.IO;
using MaterialOverhaul.Core;
using MaterialOverhaul.Orders;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// 订单身份、逐笔编辑与无副作用预告（V0.0.3 开发计划 §5.1、§5.2、§9.1「订单」）。
    /// A 用有温区的铝配方，B 用无温区的其他金属配方，用于验证调度隔离。
    /// </summary>
    public static class OrderIdentityTests
    {
        private const string A = "MetalRefinery_AluminumOre_Aluminum";
        private const string B = "MetalRefinery_CopperOre_Copper";

        public static void Run()
        {
            IdentitiesAreDistinct();
            PreviewHasNoSideEffect();
            AToBToA();
            LimitedRunsContinuously();
            InfiniteQuota();
            MissingMaterialYields();
            AllMissingWaits();
            CancelOneOfDuplicates();
            EditDoesNotMerge();
            SaveLoadKeepsIdentity();
            SerializedRoundTrip();
            QueuePanel();
        }

        private static void IdentitiesAreDistinct()
        {
            TestRunner.Section("同配方重复添加各有独立身份");
            OrderEntry first = OrderEntry.Limited(A, 1);
            OrderEntry second = OrderEntry.Limited(A, 1);
            TestRunner.Check(first.Id != second.Id, "两笔同配方订单身份不同");
            TestRunner.Check(first.Id > 0 && second.Id > 0, "身份为单调正整数");
        }

        private static void PreviewHasNoSideEffect()
        {
            TestRunner.Section("预告查询不动轮换指针");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited(A, 1));
            queue.Add(OrderEntry.Limited(B, 1));

            HashSet<string> ready = new HashSet<string> { B };
            int before = queue.Cursor;
            int previewed = queue.PreviewNext(o => ready.Contains(o.RecipeId));
            TestRunner.CheckEqual(1, previewed, "预告给出第一个可执行订单");
            TestRunner.CheckEqual(before, queue.Cursor, "预告不移动指针");

            int selected = queue.SelectNext(o => ready.Contains(o.RecipeId));
            TestRunner.CheckEqual(1, selected, "真正选择时才移动指针");
            TestRunner.CheckEqual(1, queue.Cursor, "选择后指针停在选中订单");
        }

        private static void AToBToA()
        {
            TestRunner.Section("A→B→A 顺序执行");
            OrderQueue queue = new OrderQueue();
            OrderEntry a1 = OrderEntry.Limited(A, 1);
            OrderEntry b1 = OrderEntry.Limited(B, 1);
            OrderEntry a2 = OrderEntry.Limited(A, 1);
            queue.Add(a1);
            queue.Add(b1);
            queue.Add(a2);

            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "先做 A");
            queue.Lock(0);
            queue.CompleteBatchById(a1.Id);

            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "接着做 B（A 已移出）");
            TestRunner.CheckEqual(B, queue.Orders[0].RecipeId, "B 前移到第一位");
            queue.Lock(0);
            queue.CompleteBatchById(b1.Id);

            TestRunner.CheckEqual(A, queue.Orders[0].RecipeId, "最后一笔是 A");
            queue.Lock(0);
            queue.CompleteBatchById(a2.Id);
            TestRunner.CheckEqual(0, queue.Count, "三轮后队列清空");
        }

        private static void LimitedRunsContinuously()
        {
            TestRunner.Section("有限订单连续执行");
            OrderQueue queue = new OrderQueue();
            OrderEntry a = OrderEntry.Limited(A, 3);
            queue.Add(a);
            queue.Add(OrderEntry.Limited(B, 1));

            for (int i = 0; i < 3; i++)
            {
                int index = queue.SelectNext(o => true);
                TestRunner.CheckEqual(0, index, "第 " + (i + 1) + " 批仍选同一笔订单");
                queue.Lock(index);
                queue.CompleteBatchById(a.Id);
            }

            TestRunner.CheckEqual(1, queue.Count, "数量完成后移出队列");
            TestRunner.CheckEqual(B, queue.Orders[0].RecipeId, "接着轮到后面的订单");
        }

        private static void InfiniteQuota()
        {
            TestRunner.Section("无限订单额度 1／3");
            OrderQueue queue = new OrderQueue();
            OrderEntry infinite = OrderEntry.Infinite(A, 1);
            queue.Add(infinite);
            queue.Add(OrderEntry.Limited(B, 1));

            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "额度 1：先做无限订单");
            queue.Lock(0);
            queue.CompleteBatchById(infinite.Id);
            TestRunner.CheckEqual(1, queue.SelectNext(o => true), "完成 1 批后让出给后面的订单");

            TestRunner.Check(queue.SetInfiniteQuota(infinite.Id, 3), "额度改为 3");
            TestRunner.CheckEqual(3, queue.Orders[0].BatchQuota, "额度写入");
            TestRunner.CheckEqual(0, queue.Orders[0].BatchesDoneThisRound, "改额度保留已完成数（当前为 0）");

            // 让无限订单重新排到前面，连续做满 3 批。
            queue.RemoveById(queue.Orders[1].Id);
            for (int i = 0; i < 3; i++)
            {
                queue.Lock(0);
                queue.CompleteBatchById(infinite.Id);
            }

            TestRunner.CheckEqual(0, queue.Orders[0].BatchesDoneThisRound, "完成 3 批后本轮归零");
            TestRunner.CheckEqual(1, queue.Count, "无限订单本身仍留在队列");
        }

        private static void MissingMaterialYields()
        {
            TestRunner.Section("缺料让出、后续到料不抢当前批次");
            OrderQueue queue = new OrderQueue();
            OrderEntry a = OrderEntry.Limited(A, 2);
            OrderEntry b = OrderEntry.Limited(B, 1);
            queue.Add(a);
            queue.Add(b);

            queue.Lock(0);
            TestRunner.CheckEqual(A, queue.LockedBatch.RecipeId, "当前批次为 A");

            // A 缺料，B 齐备：下一批让出给 B，但当前锁定的 A 批次不动。
            int next = queue.SelectNext(o => o.RecipeId == B);
            TestRunner.CheckEqual(1, next, "缺料时让出给后面订单");
            TestRunner.CheckEqual(2, queue.Count, "缺料订单仍留在队列");
            TestRunner.CheckEqual(A, queue.LockedBatch.RecipeId, "当前批次不被抢占");
            TestRunner.CheckEqual(2, queue.FindById(a.Id).Remaining, "缺料订单保留剩余数量");
        }

        private static void AllMissingWaits()
        {
            TestRunner.Section("全部缺料等待");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited(A, 1));
            queue.Add(OrderEntry.Limited(B, 1));

            int cursor = queue.Cursor;
            TestRunner.CheckEqual(-1, queue.SelectNext(o => false), "全部缺料返回等待");
            TestRunner.CheckEqual(cursor, queue.Cursor, "等待时不移动指针");
        }

        private static void CancelOneOfDuplicates()
        {
            TestRunner.Section("取消同配方其中一笔");
            OrderQueue queue = new OrderQueue();
            OrderEntry first = OrderEntry.Limited(A, 2);
            OrderEntry second = OrderEntry.Limited(A, 3);
            queue.Add(first);
            queue.Add(OrderEntry.Limited(B, 1));
            queue.Add(second);

            TestRunner.Check(queue.RemoveById(second.Id), "取消第三笔 A");
            TestRunner.CheckEqual(2, queue.Count, "队列减少一笔");
            TestRunner.CheckEqual(2, queue.FindById(first.Id).Remaining, "第一笔 A 数量不受影响");
            TestRunner.Check(queue.ContainsRecipe(A), "同配方仍有订单");

            queue.Lock(queue.IndexOfId(first.Id));
            TestRunner.Check(queue.RemoveById(first.Id), "取消当前那一笔 A");
            TestRunner.Check(!queue.HasLockedBatch, "取消当前订单后清除批次锁定");
            TestRunner.CheckEqual(1, queue.Count, "只剩 B");
            TestRunner.CheckEqual(B, queue.Orders[0].RecipeId, "未操作的那一笔保持位置");
        }

        private static void EditDoesNotMerge()
        {
            TestRunner.Section("逐笔数量编辑不合并同配方订单");
            OrderQueue queue = new OrderQueue();
            OrderEntry first = OrderEntry.Limited(A, 2);
            OrderEntry second = OrderEntry.Limited(A, 3);
            queue.Add(first);
            queue.Add(second);

            queue.SetRemaining(second.Id, 7);
            TestRunner.CheckEqual(2, queue.Count, "仍然两笔订单");
            TestRunner.CheckEqual(2, queue.FindById(first.Id).Remaining, "第一笔保持原值");
            TestRunner.CheckEqual(7, queue.FindById(second.Id).Remaining, "第二笔改为 7");

            queue.SetRemaining(first.Id, 0);
            TestRunner.CheckEqual(1, queue.Count, "调到 0 按取消处理");
            TestRunner.CheckEqual(7, queue.FindById(second.Id).Remaining, "另一笔不受影响");

            TestRunner.Check(queue.SetInfinite(second.Id, true, 1), "切换为无限订单");
            TestRunner.Check(queue.FindById(second.Id).IsInfinite, "标记为无限");
            TestRunner.CheckEqual(1, queue.FindById(second.Id).BatchQuota, "使用默认额度 1");
        }

        private static void SaveLoadKeepsIdentity()
        {
            TestRunner.Section("读档恢复同一笔订单身份");
            OrderQueue saved = new OrderQueue();
            OrderEntry first = OrderEntry.Limited(A, 4);
            OrderEntry second = OrderEntry.Limited(A, 2);
            saved.Add(first);
            saved.Add(OrderEntry.Limited(B, 1));
            saved.Add(second);
            saved.Lock(2);

            OrderQueue loaded = new OrderQueue();
            loaded.Restore(saved.Orders, saved.Cursor, saved.LockedOrderId);

            TestRunner.CheckEqual(3, loaded.Count, "订单数量恢复");
            TestRunner.CheckEqual(second.Id, loaded.LockedOrderId, "恢复的是同一笔订单身份");
            TestRunner.CheckEqual(2, loaded.Orders[2].Remaining, "被锁定那一笔的数量正确");
            TestRunner.CheckEqual(4, loaded.Orders[0].Remaining, "同配方另一笔不被混淆");

            OrderQueue missing = new OrderQueue();
            missing.Restore(saved.Orders, 0, 999999);
            TestRunner.Check(!missing.HasLockedBatch, "身份在队列里找不到时不伪造锁定批次");
        }

        private static void SerializedRoundTrip()
        {
            TestRunner.Section("订单身份在存档字段中往返");
            OrderQueue queue = new OrderQueue();
            OrderEntry infinite = OrderEntry.Infinite(A, 3);
            infinite.BatchesDoneThisRound = 2;
            queue.Add(infinite);
            OrderEntry limited = OrderEntry.Limited(B, 5);
            queue.Add(limited);
            queue.Lock(1);

            // 模拟组件序列化：身份、数量、额度、本轮进度逐项写出再读回。
            int[] ids = new int[queue.Count];
            string[] recipes = new string[queue.Count];
            bool[] infiniteFlags = new bool[queue.Count];
            int[] remaining = new int[queue.Count];
            int[] quotas = new int[queue.Count];
            int[] roundDone = new int[queue.Count];
            for (int i = 0; i < queue.Count; i++)
            {
                OrderEntry entry = queue.Orders[i];
                ids[i] = entry.Id;
                recipes[i] = entry.RecipeId;
                infiniteFlags[i] = entry.IsInfinite;
                remaining[i] = entry.Remaining;
                quotas[i] = entry.BatchQuota;
                roundDone[i] = entry.BatchesDoneThisRound;
            }

            List<OrderEntry> restored = new List<OrderEntry>();
            for (int i = 0; i < ids.Length; i++)
            {
                restored.Add(OrderEntry.Restored(recipes[i], ids[i], infiniteFlags[i], remaining[i], quotas[i], roundDone[i]));
            }

            OrderQueue roundTripped = new OrderQueue();
            roundTripped.Restore(restored, queue.Cursor, queue.LockedOrderId);

            TestRunner.CheckEqual(queue.Count, roundTripped.Count, "订单数量一致");
            TestRunner.CheckEqual(infinite.Id, roundTripped.Orders[0].Id, "身份一致");
            TestRunner.Check(roundTripped.Orders[0].IsInfinite, "无限口径一致");
            TestRunner.CheckEqual(3, roundTripped.Orders[0].BatchQuota, "本轮额度一致");
            TestRunner.CheckEqual(2, roundTripped.Orders[0].BatchesDoneThisRound, "本轮进度一致");
            TestRunner.CheckEqual(5, roundTripped.Orders[1].Remaining, "有限数量一致");
            TestRunner.CheckEqual(Balance.MaxInfiniteBatchQuota, 99, "无限额度上限为 99");
        }

        private static void QueuePanel()
        {
            TestRunner.Section("信息卡单独的订单队列面板");
            DirectoryInfo dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MaterialOverhaul", "MaterialOverhaul.csproj")))
            {
                dir = dir.Parent;
            }
            TestRunner.Check(dir != null, "能找到仓库根目录");
            if (dir == null)
            {
                return;
            }

            string source = File.ReadAllText(Path.Combine(dir.FullName, "MaterialOverhaul", "Game", "Patches", "OrderQueuePanelPatches.cs"));
            TestRunner.Check(source.Contains("[HarmonyPatch(typeof(SimpleInfoScreen), \"OnPrefabInit\")]"), "面板在信息卡建好时创建");
            TestRunner.Check(source.Contains("Method(\"CreateCollapsableSection\""), "用原版可折叠面板，样式与其他面板一致");
            TestRunner.Check(source.Contains("status.transform.GetSiblingIndex() + 1"), "面板紧跟状态面板");
            TestRunner.Check(source.Contains("fab.HasOrderData") && source.Contains("panel.Commit()"), "没有订单时提交空面板，原版会把它隐藏");
            TestRunner.Check(source.Contains("i == locked") && source.Contains("i == queue.Cursor"), "标出生产中的批次与下一张轮到的订单");
        }
    }
}
