using MaterialOverhaul.Orders;

namespace MaterialOverhaul.Tests
{
    /// <summary>
    /// v0.0.2 订单流程：以「订单」为单位（允许同配方重复）、数量与原版计数口径对齐、
    /// 有限订单连续执行、无限订单按轮次让出、取消与窗口编辑不改变位置
    /// （建筑细节文档 §2.3.1–§2.3.4，V0.0.2 开发计划 §3）。
    /// </summary>
    public static class OrderFlowTests
    {
        public static void Run()
        {
            RepeatedRecipeKeepsSeparateOrders();
            TotalsAggregatePerRecipe();
            LimitedOrderRunsContinuously();
            InfiniteRoundYieldsToNextOrder();
            MissingMaterialsKeepsRoundProgress();
            ReplaceRecipeKeepsPosition();
            DecrementRemovesLastOrderOfRecipe();
            RemoveAllOfRecipeFixesCursor();
            SelectionWaitsWithoutMovingPointer();
        }

        private static void RepeatedRecipeKeepsSeparateOrders()
        {
            TestRunner.Section("同配方重复添加各成独立订单");
            OrderQueue queue = new OrderQueue();

            // 原版界面每点一次「+」回调一次，本 Mod 每次都新建一笔订单。
            queue.AddBatchRequest("copper");
            queue.AddBatchRequest("iron");
            queue.AddBatchRequest("copper");
            TestRunner.CheckEqual(3, queue.Count, "三次添加等于三笔订单，不合并");
            TestRunner.CheckEqual("copper", queue.Orders[2].RecipeId, "重复配方保留在末尾位置");

            int index = queue.SelectNext(o => o.RecipeId == "copper");
            TestRunner.CheckEqual(0, index, "从轮换位置取到第一笔 copper");
            queue.LockForStart("copper");
            TestRunner.CheckEqual(0, queue.LockedIndex, "锁定轮换位置上那一笔，而不是队列里最后一笔同配方订单");

            queue.CompleteBatch("copper");
            TestRunner.CheckEqual(2, queue.Count, "只消耗被锁定的那一笔订单");
            TestRunner.CheckEqual("iron", queue.Orders[0].RecipeId, "其余订单顺序不变");
            TestRunner.CheckEqual("copper", queue.Orders[1].RecipeId, "另一笔同配方订单仍在队列");
            TestRunner.CheckEqual(1, queue.TotalFor("copper").Batches, "另一笔同配方订单数量不受影响");
        }

        private static void TotalsAggregatePerRecipe()
        {
            TestRunner.Section("按配方汇总数量（用于同步原版计数）");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited("copper", 2));
            queue.Add(OrderEntry.Limited("iron", 3));
            queue.Add(OrderEntry.Limited("copper", 1));

            TestRunner.CheckEqual(3, queue.TotalFor("copper").Batches, "同配方多笔订单数量求和");
            TestRunner.Check(!queue.TotalFor("copper").Infinite, "有限订单不是无限口径");
            TestRunner.CheckEqual(3, queue.TotalFor("iron").Batches, "其他配方数量不受影响");
            TestRunner.Check(queue.TotalFor("gold").IsEmpty, "未添加的配方数量为空");
            TestRunner.CheckEqual(0, queue.TotalFor(null).Batches, "空索引返回空");

            queue.Add(OrderEntry.Infinite("iron", OrderEntry.PlaceholderInfiniteBatchQuota));
            TestRunner.Check(queue.TotalFor("iron").Infinite, "存在无限订单时按无限口径");
            TestRunner.CheckEqual(3, queue.TotalFor("iron").Batches, "无限口径下仍保留有限批次数");
        }

        private static void LimitedOrderRunsContinuously()
        {
            TestRunner.Section("有限订单连续执行到数量完成");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited("copper", 3));
            queue.Add(OrderEntry.Limited("iron", 2));

            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "先执行第一笔订单");
            queue.LockForStart("copper");
            queue.CompleteBatch("copper");
            TestRunner.CheckEqual(2, queue.Orders[0].Remaining, "剩余数量逐批扣减");

            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "原料持续齐备时继续同一笔订单，不让给后面的订单");
            queue.LockForStart("copper");
            queue.CompleteBatch("copper");
            queue.LockForStart("copper");
            queue.CompleteBatch("copper");

            TestRunner.CheckEqual(1, queue.Count, "数量完成后移出队列");
            TestRunner.CheckEqual("iron", queue.Orders[0].RecipeId, "完成后继续检查后面的订单");
            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "轮到后面那笔订单");
        }

        private static void InfiniteRoundYieldsToNextOrder()
        {
            TestRunner.Section("无限订单完成本轮后让出");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Infinite("copper", OrderEntry.PlaceholderInfiniteBatchQuota));
            queue.Add(OrderEntry.Limited("iron", 1));

            TestRunner.CheckEqual(0, queue.SelectNext(o => true), "无限订单先执行");
            queue.LockForStart("copper");
            queue.CompleteBatch("copper");

            TestRunner.CheckEqual(0, queue.Orders[0].BatchesDoneThisRound, "本轮额度用完后进度归零");
            TestRunner.CheckEqual(1, queue.Cursor, "让出后指针移到后面的订单");
            TestRunner.CheckEqual(1, queue.SelectNext(o => true), "让出后可选中后面的订单");
            TestRunner.Check(queue.TotalFor("copper").Infinite, "无限订单本身仍留在队列");
        }

        private static void MissingMaterialsKeepsRoundProgress()
        {
            TestRunner.Section("无限订单本轮中途缺料让出");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Infinite("copper", 3));
            queue.Add(OrderEntry.Limited("iron", 1));

            queue.LockForStart("copper");
            queue.CompleteBatch("copper");
            TestRunner.CheckEqual(1, queue.Orders[0].BatchesDoneThisRound, "本轮进度 1/3");

            // copper 下一批缺料，iron 齐备：只跳过 copper，不删除它，也不清本轮进度。
            int next = queue.SelectNext(o => o.RecipeId == "iron");
            TestRunner.CheckEqual(1, next, "缺料时让出给后面订单");
            TestRunner.CheckEqual(1, queue.Orders[0].BatchesDoneThisRound, "让出后保留本轮进度");
            TestRunner.CheckEqual(2, queue.Count, "缺料的订单仍留在队列中");
        }

        private static void ReplaceRecipeKeepsPosition()
        {
            TestRunner.Section("侧屏直接设置数量：位置与指针不变");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited("copper", 1));
            queue.Add(OrderEntry.Limited("iron", 1));
            queue.Add(OrderEntry.Limited("copper", 1));

            queue.SelectNext(o => o.RecipeId == "iron");
            TestRunner.CheckEqual(1, queue.Cursor, "指针先停在 iron");

            queue.ReplaceRecipeWithBatches("copper", 5);
            TestRunner.CheckEqual(2, queue.Count, "同配方多笔订单合并为一笔");
            TestRunner.CheckEqual("copper", queue.Orders[0].RecipeId, "沿用原来第一笔的位置");
            TestRunner.CheckEqual(5, queue.Orders[0].Remaining, "数量改为设定值");
            TestRunner.CheckEqual("iron", queue.Orders[1].RecipeId, "其他订单顺序不变");
            TestRunner.CheckEqual(1, queue.Cursor, "指针仍指向 iron");

            queue.ReplaceRecipeWithInfinite("iron", 4);
            TestRunner.Check(queue.TotalFor("iron").Infinite, "改为无限订单");
            TestRunner.CheckEqual("iron", queue.Orders[1].RecipeId, "无限订单沿用原位置");
            TestRunner.CheckEqual(4, queue.Orders[1].BatchQuota, "无限订单额度写入");
            TestRunner.CheckEqual(0, queue.Orders[1].BatchesDoneThisRound, "重设额度时本轮进度归零");

            queue.ReplaceRecipeWithBatches("copper", 0);
            TestRunner.CheckEqual(1, queue.Count, "数量设为 0 等于取消该配方订单");
            TestRunner.Check(!queue.ContainsRecipe("copper"), "取消后队列里没有该配方");

            queue.ReplaceRecipeWithBatches("gold", 2);
            TestRunner.CheckEqual(2, queue.Count, "新配方追加到末尾");
            TestRunner.CheckEqual("gold", queue.Orders[1].RecipeId, "追加位置在末尾");
        }

        private static void DecrementRemovesLastOrderOfRecipe()
        {
            TestRunner.Section("原版「−」按订单逐笔回退");
            OrderQueue queue = new OrderQueue();
            queue.AddBatchRequest("copper");
            queue.Add(OrderEntry.Limited("copper", 3));
            queue.AddBatchRequest("iron");

            TestRunner.Check(queue.DecrementLastBatch("copper"), "回退最后一笔 copper");
            TestRunner.CheckEqual(3, queue.Count, "数量为 3 的那笔减为 2，订单本身还在");
            TestRunner.CheckEqual(2, queue.Orders[1].Remaining, "剩余数量减 1");

            TestRunner.Check(queue.DecrementLastBatch("copper"), "再回退一次");
            TestRunner.CheckEqual(1, queue.Orders[1].Remaining, "剩余 1");

            TestRunner.Check(queue.DecrementLastBatch("copper"), "最后一笔减到 0 时整笔移除");
            TestRunner.CheckEqual(2, queue.Count, "该笔订单被移除");
            TestRunner.CheckEqual("iron", queue.Orders[1].RecipeId, "后面的订单顺序不变");

            TestRunner.Check(!queue.DecrementLastBatch("gold"), "没有该配方订单时返回 false");

            queue.ReplaceRecipeWithInfinite("iron", 2);
            TestRunner.Check(queue.DecrementLastBatch("iron"), "无限订单也能被回退");
            TestRunner.Check(!queue.ContainsRecipe("iron"), "无限订单整笔移除");
        }

        private static void RemoveAllOfRecipeFixesCursor()
        {
            TestRunner.Section("取消某配方全部订单");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited("copper", 1));
            queue.Add(OrderEntry.Limited("iron", 1));
            queue.Add(OrderEntry.Limited("copper", 1));

            queue.SelectNext(o => true);
            queue.LockForStart("copper");
            queue.SelectNext(o => o.RecipeId == "iron");
            queue.SelectNext(o => o.RecipeId == "copper");
            TestRunner.CheckEqual(2, queue.Cursor, "指针停在第二笔 copper");

            TestRunner.Check(queue.RemoveAllOfRecipe("copper"), "移除 copper 的全部订单");
            TestRunner.CheckEqual(1, queue.Count, "只剩 iron");
            TestRunner.CheckEqual(0, queue.Cursor, "指针回到仍在队列中的订单");
            TestRunner.Check(!queue.HasLockedBatch, "被取消的当前订单同时清除锁定批次");
        }

        private static void SelectionWaitsWithoutMovingPointer()
        {
            TestRunner.Section("全部订单缺料时等待");
            OrderQueue queue = new OrderQueue();
            queue.Add(OrderEntry.Limited("copper", 1));
            queue.Add(OrderEntry.Limited("iron", 1));
            queue.SelectNext(o => o.RecipeId == "iron");
            TestRunner.CheckEqual(1, queue.Cursor, "指针在 iron");

            TestRunner.CheckEqual(-1, queue.SelectNext(o => false), "全部缺料返回等待");
            TestRunner.CheckEqual(1, queue.Cursor, "等待时不移动指针（不空转）");
        }
    }
}
