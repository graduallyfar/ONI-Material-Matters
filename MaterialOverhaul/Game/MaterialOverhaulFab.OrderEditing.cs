using System.Collections.Generic;
using MaterialOverhaul.Core;
using MaterialOverhaul.Orders;

namespace MaterialOverhaul.Game
{
    // 原版数量入口与旧队列迁移。独立 partial 便于用实际源码回归刷新中的重复记账。
    public partial class MaterialOverhaulFab
    {
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
                // RefreshQueue 的嵌套刷新可能已将这次新增数量导入订单表。
                // 只追加尚未入账的批次，避免一次点击被「迁移 + 点击后置」重复记账。
                int missing = count - orders.TotalFor(recipe.id).Batches;
                for (int i = 0; i < missing; i++)
                {
                    orders.AddBatchRequest(recipe.id);
                }
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
                // 刷新可能已经迁入这个临时无限值，订单表与原版计数都要清理。
                orders.RemoveAllOfRecipe(recipe.id);
                AfterOrderChange();
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

    }
}
