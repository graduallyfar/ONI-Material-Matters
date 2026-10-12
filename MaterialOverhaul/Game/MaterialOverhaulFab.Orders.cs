using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Orders;
using MaterialOverhaul.Zones;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 订单身份、批次锁定与逐笔编辑（V0.0.3 开发计划 §5.1、§5.2）。
    ///
    /// 原版队列是「每配方一个计数」的字典，没有顺序概念，轮换按 <c>recipe_list</c> 下标推进。
    /// 本 Mod 用有序订单表接管「下一个执行哪一笔」，并把订单表的数量折算回原版计数字典，
    /// 使原版界面、预取与可用性判断继续按原版口径工作。
    ///
    /// 与批次的关系：**批次准备**（原版 <c>StartWorkingOrder</c>）时按订单身份锁定本批；
    /// 该批次完成或取消之前，温度暂停、后续增删、其他原料送达都不抢占它。
    /// </summary>
    public partial class MaterialOverhaulFab
    {
        /// <summary>本 Mod 订单表里是否有数据。没有数据时轮换与原版一致。</summary>
        public bool HasOrderData
        {
            get { return orders.Count > 0; }
        }

        /// <summary>当前配方的显示名（配方选择界面同源）。</summary>
        public string RecipeDisplayName(string recipeId)
        {
            ComplexRecipe[] recipes = fabricator != null ? fabricator.GetRecipes() : null;
            int index = IndexOfRecipe(recipes, recipeId);
            if (index < 0)
            {
                return recipeId;
            }

            string name = recipes[index].GetUIName(false);
            return string.IsNullOrEmpty(name) ? recipeId : name;
        }

        /// <summary>本建筑的配方表；不可用时返回空数组。</summary>
        public ComplexRecipe[] Recipes
        {
            get
            {
                ComplexRecipe[] recipes = fabricator != null ? fabricator.GetRecipes() : null;
                return recipes ?? new ComplexRecipe[0];
            }
        }

        /* ---- 原版界面入口的映射 ---- */

        /// <summary>原版「+」之后同步订单表：新增一笔 1 批订单；计数越过上限变无限时整笔替换为无限订单。</summary>
        public void OnVanillaIncrement(ComplexRecipe recipe, int previousCount)
        {
            if (fabricator == null || recipe == null)
            {
                return;
            }

            int count = fabricator.GetRecipeQueueCount(recipe);
            if (FabricatorAccess.IsInfinite(count))
            {
                orders.ReplaceRecipeWithInfinite(recipe.id, Balance.DefaultInfiniteBatchQuota);
            }
            else if (FabricatorAccess.IsInfinite(previousCount))
            {
                // 原版在无限状态下点「+」会把计数清成 0：订单表同样取消该配方。
                orders.RemoveAllOfRecipe(recipe.id);
            }
            else
            {
                orders.AddBatchRequest(recipe.id);
            }

            AfterOrderChange();
        }

        /// <summary>原版「−」之后同步订单表：按订单逐笔回退。</summary>
        public void OnVanillaDecrement(ComplexRecipe recipe, int previousCount)
        {
            if (fabricator == null || recipe == null)
            {
                return;
            }

            int count = fabricator.GetRecipeQueueCount(recipe);
            if (count == previousCount)
            {
                // respectInfinite 为真且已是无限：原版不做改动，保留无限订单及其本轮进度。
                return;
            }

            if (FabricatorAccess.IsInfinite(count))
            {
                // 原版会把数量 0 递减成无限。本 Mod 不凭一次递减凭空创建订单，并把计数改回 0。
                ForceVanillaCount(recipe.id, 0);
                ModLog.Info(PrefabId + "：" + recipe.id
                    + " 原版数量为 0 时递减会变成无限，本 Mod 不创建订单，已把计数改回 0。");
                return;
            }

            if (FabricatorAccess.IsInfinite(previousCount))
            {
                // 原版把无限降到队列上限：改记为对应批次数，之后继续逐笔回退。
                orders.ReplaceRecipeWithBatches(recipe.id, count);
            }
            else if (count <= 0)
            {
                orders.RemoveAllOfRecipe(recipe.id);
            }
            else
            {
                orders.DecrementLastBatch(recipe.id);
            }

            AfterOrderChange();
        }

        /// <summary>
        /// 原版侧屏直接设置数量。**这是有歧义的合计口径**（会合并同配方多笔订单），
        /// 只用于兼容旧界面与批量改写；逐笔编辑走 <see cref="EditOrderRemaining"/> 等方法（§5.1）。
        /// </summary>
        public void OnVanillaSetCount(ComplexRecipe recipe, int count)
        {
            if (fabricator == null || recipe == null)
            {
                return;
            }

            if (FabricatorAccess.IsInfinite(count))
            {
                orders.ReplaceRecipeWithInfinite(recipe.id, Balance.DefaultInfiniteBatchQuota);
            }
            else if (count > 0)
            {
                orders.ReplaceRecipeWithBatches(recipe.id, count);
            }
            else
            {
                orders.RemoveAllOfRecipe(recipe.id);
            }

            AfterOrderChange();
        }

        /* ---- 逐笔编辑（§5.1）---- */

        /// <summary>逐笔编辑有限订单剩余数量；调到 0 按取消处理。返回 false 表示订单不存在。</summary>
        public bool EditOrderRemaining(int orderId, int remaining)
        {
            bool cancelCurrent = remaining <= 0 && orders.IsLockedOrder(orderId);
            if (!orders.SetRemaining(orderId, remaining))
            {
                return false;
            }

            if (cancelCurrent)
            {
                CancelCurrentBatch();
            }

            AfterOrderChange();
            return true;
        }

        /// <summary>逐笔切换有限／无限口径。切到无限时用量额度。</summary>
        public bool EditOrderInfinite(int orderId, bool infinite, int batchesOrQuota)
        {
            bool cancelCurrent = !infinite && batchesOrQuota <= 0 && orders.IsLockedOrder(orderId);
            if (!orders.SetInfinite(orderId, infinite, batchesOrQuota))
            {
                return false;
            }

            if (cancelCurrent)
            {
                CancelCurrentBatch();
            }

            AfterOrderChange();
            return true;
        }

        /// <summary>逐笔修改无限订单每轮额度（1–99），保留已完成数。</summary>
        public bool EditOrderQuota(int orderId, int quota)
        {
            if (!orders.SetInfiniteQuota(orderId, quota))
            {
                return false;
            }

            AfterOrderChange();
            return true;
        }

        /// <summary>
        /// 逐笔取消：取消精确对应那一笔；若它正是当前批次，同时退回加工仓原料并清零本批进度。
        /// 即使队列里仍有相同配方也必须能取消当前笔（不能依赖原版 <c>IsRecipeQueued</c>）。
        /// </summary>
        public bool CancelOrder(int orderId)
        {
            if (orders.FindById(orderId) == null)
            {
                return false;
            }

            bool wasCurrentBatch = orders.IsLockedOrder(orderId);
            orders.RemoveById(orderId);

            if (wasCurrentBatch)
            {
                CancelCurrentBatch();
            }

            AfterOrderChange();
            ModLog.Info(PrefabId + "：已取消订单 #" + orderId + (wasCurrentBatch ? "（含当前批次，原料已退回输入仓）" : string.Empty) + "。");
            return true;
        }

        /// <summary>
        /// 取消当前批次并退回原料：走原版 <c>CancelWorkingOrder</c>，
        /// 它会调用 <c>buildStorage.Transfer(inStorage)</c> 并把本批进度清零。
        /// </summary>
        private void CancelCurrentBatch()
        {
            if (fabricator == null || !FabricatorAccess.HasWorkingOrder(fabricator))
            {
                productionPausedByTemperature = false;
                return;
            }

            FabricatorAccess.CancelWorkingOrder(fabricator);
            productionPausedByTemperature = false;
            runState = RunState.Fresh;
            SyncRunStateFields();
            currentRequestedWatts = 0f;
            currentUsedWatts = 0f;
            powerDemand = default(PowerDemand);
        }

        /* ---- 调度 ---- */

        /// <summary>
        /// 自动化红信号下的批次准备（建筑细节文档 §2.2.3、V0.0.3 开发计划 §3.1）。
        ///
        /// 设计约定：**红色只暂停生产，不影响配方准备、预热和补热**。原版把「总体可运行」
        /// 当作开始一批的前提（<c>RefreshAndStartNextOrder</c> 先判断 <c>operational.IsOperational</c>），
        /// 因此红信号下没有当前批次时根本无法锁定，也就无从预热。这里补上准备路径：
        ///   1. 需要时先让原版重算队列（建立预取额度并提交取料需求）；
        ///   2. 选中材料齐备的那一笔并锁定；
        ///   3. 调用原版唯一开始入口 <c>StartWorkingOrder</c> 完成投料与订单记账。
        /// 整个过程**不推进生产、不结算产物**：红信号下原版不会建立操作任务，复制人不会被占用。
        ///
        /// 只在「确实挂了自动化输入端口」且「红信号是唯一阻塞原因」时接管，其余情况完全走原版。
        /// </summary>
        public void PrepareBatchWhenAutomationBlocks()
        {
            if (fabricator == null || !hasAutomationInput)
            {
                return;
            }

            if (AutomationAllowsProduction || HasLockedBatch)
            {
                // 绿信号走原版路径；已有当前批次时不需要准备。
                if (!AutomationAllowsProduction && HasLockedBatch && queueRefreshNeeded)
                {
                    // 红信号下原版不会刷新队列：原料到货等变化时补一次，保证后续批次的取料继续。
                    RefreshVanillaQueue();
                }

                return;
            }

            if (orders.Count == 0 || IsBrokenDown || IsManuallyDisabled || !IsPowered)
            {
                return;
            }

            ComplexRecipe[] recipes = fabricator.GetRecipes();
            if (recipes == null || recipes.Length == 0)
            {
                return;
            }

            // 红信号下原版不会跑 RefreshQueue，预取额度与取料需求需要这里补上。
            if (queueRefreshNeeded || !FabricatorAccess.AnyPrefetchQuota(fabricator))
            {
                RefreshVanillaQueue();
            }

            int orderIndex = orders.SelectNext(entry => IsOrderExecutable(entry, recipes));
            if (orderIndex < 0)
            {
                FabricatorAccess.NextOrderIsWorkable(fabricator) = false;
                return;
            }

            string recipeId = orders.Orders[orderIndex].RecipeId;
            int recipeIndex = IndexOfRecipe(recipes, recipeId);
            if (recipeIndex < 0)
            {
                FabricatorAccess.NextOrderIsWorkable(fabricator) = false;
                return;
            }

            if (!FabricatorAccess.HasPrefetchQuota(fabricator, recipeIndex))
            {
                // 材料还没到齐、额度还没建立：保留订单，等存储变化后再来一次（不空转）。
                FabricatorAccess.NextOrderIsWorkable(fabricator) = false;
                return;
            }

            FabricatorAccess.NextOrderIdx(fabricator) = recipeIndex;
            FabricatorAccess.NextOrderIsWorkable(fabricator) = true;

            // 唯一的开始入口；锁定订单身份与运行状态由它的前缀补丁完成。
            FabricatorAccess.StartWorkingOrder(fabricator, recipeIndex);

            // 同一模拟步内立刻按新批次重新判定，避免这一帧仍报告「无批次、输出红」。
            UpdateRunState();
        }

        /// <summary>让原版重算队列：刷新预取额度、取料需求与材料需求。</summary>
        private void RefreshVanillaQueue()
        {
            queueRefreshNeeded = false;
            FabricatorAccess.RefreshQueue(fabricator);
        }

        /// <summary>
        /// 重新决定「下一个执行哪一笔订单」，写入原版 <c>nextOrderIdx</c> 与 <c>nextOrderIsWorkable</c>。
        ///
        /// 顺序完全由本 Mod 订单表给出（玩家添加顺序，同配方可重复）；
        /// 「还有没有剩余批次」与「原料是否齐备」仍用原版判断，避免复制一份原版逻辑后产生偏差。
        /// 全部订单都不可执行时不移动轮换指针，等材料或订单变化后再检查（不空转）。
        /// </summary>
        public void RefreshNextOrder()
        {
            if (fabricator == null)
            {
                return;
            }

            EnsureOrdersImported();
            if (orders.Count == 0)
            {
                return;
            }

            ComplexRecipe[] recipes = fabricator.GetRecipes();
            if (recipes == null || recipes.Length == 0)
            {
                return;
            }

            int index = orders.SelectNext(entry => IsOrderExecutable(entry, recipes));
            if (index < 0)
            {
                FabricatorAccess.NextOrderIsWorkable(fabricator) = false;
                return;
            }

            string recipeId = orders.Orders[index].RecipeId;
            int recipeIndex = IndexOfRecipe(recipes, recipeId);
            if (recipeIndex < 0)
            {
                FabricatorAccess.NextOrderIsWorkable(fabricator) = false;
                ModLog.WarnOnce("order|recipe-unavailable|" + PrefabId + "|" + recipeId,
                    PrefabId + "：订单表里的配方 '" + recipeId + "' 不在本建筑的配方列表中（DLC 未启用或配方被移除）。");
                return;
            }

            FabricatorAccess.NextOrderIdx(fabricator) = recipeIndex;
            FabricatorAccess.NextOrderIsWorkable(fabricator) = true;
        }

        /// <summary>无副作用的下一批订单预告（不改轮换指针）。全部缺料或没有订单时返回 null。</summary>
        public OrderEntry NextPreviewOrder()        {
            if (fabricator == null || orders.Count == 0)
            {
                return null;
            }

            ComplexRecipe[] recipes = fabricator.GetRecipes();
            if (recipes == null || recipes.Length == 0)
            {
                return null;
            }

            return orders.PreviewNextOrder(entry => IsOrderExecutable(entry, recipes));
        }

        /// <summary>
        /// 订单表里没有、但原版计数里还有数量的配方（旧存档、其它码路直接改原版计数）纳入订单表，
        /// 否则这些订单会因为不在订单表里而永远轮不到。
        /// </summary>
        public void EnsureOrdersImported()
        {
            if (fabricator == null)
            {
                return;
            }

            ComplexRecipe[] recipes = fabricator.GetRecipes();
            Dictionary<string, int> counts = FabricatorAccess.QueueCounts(fabricator);
            if (recipes == null || recipes.Length == 0 || counts == null)
            {
                return;
            }

            bool imported = false;
            foreach (ComplexRecipe recipe in recipes)
            {
                if (recipe == null || orders.ContainsRecipe(recipe.id))
                {
                    continue;
                }

                int count;
                if (!counts.TryGetValue(recipe.id, out count) || count == 0)
                {
                    continue;
                }

                if (FabricatorAccess.IsInfinite(count))
                {
                    orders.Add(OrderEntry.Infinite(recipe.id, Balance.DefaultInfiniteBatchQuota));
                }
                else
                {
                    orders.Add(OrderEntry.Limited(recipe.id, count));
                }

                imported = true;
                ModLog.Info("订单迁移：" + PrefabId + " 把原版队列里的 " + recipe.id
                    + "（数量 " + count + "）纳入本 Mod 订单表。");
            }

            if (imported)
            {
                SyncOrderFields();
            }
        }

        /// <summary>由补丁在原版 <c>StartWorkingOrder</c> 之前调用：把「开始的那一笔」按身份锁定。</summary>
        public void LockOrderForStart(string recipeId, int recipeIndex)
        {
            RefreshModeFromOutputConnection();
            if (orders.Count == 0)
            {
                return;
            }

            // 优先按原版即将开始的下标对应的订单身份锁定；下标对不上时退回配方口径。
            int orderIndex = -1;
            if (orders.Cursor >= 0 && orders.Cursor < orders.Count
                && orders.Orders[orders.Cursor].RecipeId == recipeId
                && IsRecipeIndexConsistent(recipeIndex, recipeId))
            {
                orderIndex = orders.Cursor;
            }
            else
            {
                orderIndex = IndexOfOrderForRecipe(recipeId);
            }

            if (orderIndex < 0 || !orders.Lock(orderIndex))
            {
                ModLog.WarnOnce("order|lock-missing|" + PrefabId + "|" + recipeId,
                    PrefabId + "：原版开始的批次 '" + recipeId + "' 在订单表里找不到对应订单，批次仍按原版结算。");
                return;
            }

            SyncOrderFields();

            // 新批次重新建立启动资格与加热意图（§3.2「新批次」）。温区要按**本批**配方重新解析，
            // 不能沿用上一批缓存下来的温区。
            UpdateCachedZone(recipeId);
            if (cachedZoneConfigured)
            {
                // 原版 OnSpawn 把加工仓退回输入仓后重新锁定同一批，不视为新批次。
                // 网络尚未就绪时先保留已保存回差，由连接初始化再校验。
                if (orders.LockedOrderId == restoredBatchOrderId && restoredBatchOrderId > 0)
                {
                    if (modeConnectionReady)
                        runState = RunController.OnLoaded(BodyTemperature, cachedZone, Policy, runState);
                }
                else
                {
                    runState = RunController.OnBatchLocked(BodyTemperature, cachedZone, Policy, runState);
                }
                SyncRunStateFields();
            }
            else
            {
                runState = RunState.Fresh;
            }

            restoredBatchOrderId = 0;
            runDecision = RunDecision.Passthrough;
            productionPausedByTemperature = false;
        }

        private bool IsRecipeIndexConsistent(int recipeIndex, string recipeId)
        {
            ComplexRecipe[] recipes = Recipes;
            return recipeIndex >= 0 && recipeIndex < recipes.Length && recipes[recipeIndex].id == recipeId;
        }

        private int IndexOfOrderForRecipe(string recipeId)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders.Orders[i].RecipeId == recipeId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>批次完成前的订单记账。必须在原版正文之前完成，无限订单的本轮额度才会即时生效。</summary>
        public void OnBatchCompleting(string recipeId)
        {
            int orderId = orders.LockedOrderId;
            OrderEntry completed = orderId > 0 ? orders.CompleteBatchById(orderId) : orders.CompleteBatch(recipeId);
            if (completed == null)
            {
                // 已锁定批次被取消时原版不会走完成路径，因此这里只可能是身份丢失或外部改表。
                ModLog.WarnOnce("order|complete-unmanaged|" + PrefabId + "|" + recipeId,
                    PrefabId + "：完成了一个不在订单表里的批次 '" + recipeId + "'。");
                return;
            }

            SyncOrderFields();
        }

        /// <summary>
        /// 批次完成后的计数对齐：原版正文内部已经自己扣过一次计数（走私有的
        /// <c>DecrementRecipeQueueCountInternal</c>），这里以订单表为准写回并让队列重新计算。
        /// </summary>
        public void OnBatchCompleted(string recipeId)
        {
            SyncVanillaCount(recipeId);

            // 原版会在正文末尾立刻开始下一批；这时状态已由 LockOrderForStart 重建，
            // 不能再无条件清空，否则会把刚建立的开工资格与加热意图抹掉。
            if (!HasLockedBatch)
            {
                runState = RunState.Fresh;
                SyncRunStateFields();
                productionPausedByTemperature = false;
            }

            if (fabricator != null)
            {
                fabricator.SetQueueDirty();
            }
        }

        /// <summary>订单表变化后统一处理：写回原版计数、持久化、让队列重新计算。</summary>
        private void AfterOrderChange()
        {
            SyncAllVanillaCounts();
            SyncOrderFields();

            if (fabricator != null)
            {
                fabricator.SetQueueDirty();
            }
        }

        /// <summary>把所有配方的订单合计写回原版计数字典（无限订单写原版的无限哨兵值）。</summary>
        private void SyncAllVanillaCounts()
        {
            foreach (ComplexRecipe recipe in Recipes)
            {
                if (recipe != null)
                {
                    SyncVanillaCount(recipe.id);
                }
            }
        }

        /// <summary>把订单表里该配方的总需求写回原版计数字典（无限订单写原版的无限哨兵值）。</summary>
        private void SyncVanillaCount(string recipeId)
        {
            RecipeTotal total = orders.TotalFor(recipeId);
            ForceVanillaCount(recipeId, total.Infinite ? ComplexFabricator.QUEUE_INFINITE : total.Batches);
        }

        private void ForceVanillaCount(string recipeId, int batches)
        {
            if (fabricator == null || string.IsNullOrEmpty(recipeId))
            {
                return;
            }

            Dictionary<string, int> counts = FabricatorAccess.QueueCounts(fabricator);
            if (counts != null)
            {
                counts[recipeId] = batches;
            }
        }

        /// <summary>
        /// 该订单当前是否可执行：配方存在、还有剩余批次、原料齐备。
        ///
        /// 剩余批次与原版一致：无限订单恒有剩余，正在生产的那一批已从计数里扣掉。
        /// 原料沿用原版判断（金属精炼器的冷却液要求也在其中）；**温度与自动化都不参与**——
        /// 温度只决定已锁定批次是加热还是生产（§3.1、§3.4）。
        /// </summary>
        private bool IsOrderExecutable(OrderEntry entry, ComplexRecipe[] recipes)
        {
            if (entry == null)
            {
                return false;
            }

            int index = IndexOfRecipe(recipes, entry.RecipeId);
            if (index < 0)
            {
                return false;
            }

            ComplexRecipe recipe = recipes[index];
            if (FabricatorAccess.GetRemainingQueueCount(fabricator, recipe) <= 0)
            {
                return false;
            }

            return FabricatorAccess.HasIngredients(fabricator, recipe, fabricator.inStorage);
        }

        private static int IndexOfRecipe(ComplexRecipe[] recipes, string recipeId)
        {
            if (recipes == null || string.IsNullOrEmpty(recipeId))
            {
                return -1;
            }

            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i] != null && recipes[i].id == recipeId)
                {
                    return i;
                }
            }

            return -1;
        }

        /* ---- 序列化 ---- */

        private void SyncOrderFields()
        {
            int count = orders.Count;
            orderIds = new int[count];
            orderRecipeIds = new string[count];
            orderInfinite = new bool[count];
            orderRemaining = new int[count];
            orderQuotas = new int[count];
            orderRoundDone = new int[count];

            for (int i = 0; i < count; i++)
            {
                OrderEntry entry = orders.Orders[i];
                entry.EnsureId();
                orderIds[i] = entry.Id;
                orderRecipeIds[i] = entry.RecipeId;
                orderInfinite[i] = entry.IsInfinite;
                orderRemaining[i] = entry.Remaining;
                orderQuotas[i] = entry.IsInfinite ? entry.BatchQuota : 0;
                orderRoundDone[i] = entry.BatchesDoneThisRound;
            }

            orderCursor = orders.Cursor;
            lockedOrderId = orders.LockedOrderId;
            OrderEntry locked = orders.LockedBatch;
            lockedRecipeId = locked != null ? locked.RecipeId : string.Empty;
        }

        private void RestoreOrders()
        {
            List<OrderEntry> entries = new List<OrderEntry>();
            int count = orderRecipeIds != null ? orderRecipeIds.Length : 0;

            for (int i = 0; i < count; i++)
            {
                string recipeId = orderRecipeIds[i];
                if (string.IsNullOrEmpty(recipeId))
                {
                    continue;
                }

                bool infinite = ArrayValue(orderInfinite, i, false);
                int quota = ArrayValue(orderQuotas, i, 0);
                if (!infinite && quota > 0)
                {
                    // 兼容极早期只用「额度非零 = 无限」的存档写法。
                    infinite = true;
                }

                OrderEntry entry = OrderEntry.Restored(
                    recipeId,
                    ArrayValue(orderIds, i, 0),
                    infinite,
                    ArrayValue(orderRemaining, i, 1),
                    quota > 0 ? quota : Balance.DefaultInfiniteBatchQuota,
                    ArrayValue(orderRoundDone, i, 0));
                entries.Add(entry);
            }

            orders.Restore(entries, orderCursor, lockedOrderId);

            int assigned = orders.EnsureIds();
            if (assigned > 0)
            {
                ModLog.Info("读档迁移：" + PrefabId + " 为 " + assigned + " 笔旧订单补发了身份编号。");
                SyncOrderFields();
            }

            if (entries.Count > 0)
            {
                ModLog.Info("读档恢复订单：" + PrefabId + " 共 " + entries.Count + " 项，轮换位置 " + orders.Cursor
                    + "，当前批次订单 #" + orders.LockedOrderId + "。");
            }

            // 身份字段只用于迁移记录，恢复后由订单表重新维护。
            lockedRecipeId = string.Empty;
        }

        private static int ArrayValue(int[] array, int index, int fallback)
        {
            if (array == null || index < 0 || index >= array.Length)
            {
                return fallback;
            }

            return array[index];
        }

        private static bool ArrayValue(bool[] array, int index, bool fallback)
        {
            if (array == null || index < 0 || index >= array.Length)
            {
                return fallback;
            }

            return array[index];
        }
    }
}
