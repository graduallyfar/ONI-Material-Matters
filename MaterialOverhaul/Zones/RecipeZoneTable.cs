using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 配方温区侧表：索引为原版配方标识（<c>ComplexRecipe.id</c>），值为**配方最佳温区**
    /// `[B₁, B₂]`（V0.1.2 开发计划 §2.1）。
    ///
    /// 原版配方数据结构里不存在「每个配方拥有自己的温度范围」的对应物，
    /// 因此这两条数值必须由本 Mod 提供，复用原版配方标识作索引，不新建平行配方系统。
    ///
    /// V0.1.2 起这张表**只放配方的两个边界**：
    ///   - 最低工作温度在 <see cref="BuildingZoneTable"/>（建筑级）；
    ///   - 最高工作温度由主建造材料熔点推出（建筑实例级）；
    ///   - `B₂ &gt; U` 与 `B₁ &gt; U` 都是合法配置，不在这里判错。
    ///
    /// 侧表**不参与存档**，每次启动重建。未登记的配方退化为未配置，行为等同原版。
    /// </summary>
    public static class RecipeZoneTable
    {
        private static readonly SideTable<TempZone> Table = new SideTable<TempZone>("配方温区");

        public static int Count
        {
            get { return Table.Count; }
        }

        /// <summary>登记一条。两个边界在此处校验，非法值退化为未配置并记录日志。</summary>
        public static void Register(string recipeId, TempZone zone)
        {
            Table.Set(recipeId, TempZoneResolver.Validate(recipeId, zone));
        }

        public static bool TryGet(string recipeId, out TempZone zone)
        {
            return Table.TryGet(recipeId, out zone);
        }

        /// <summary>取温区；未匹配时返回未配置并记录一次可定位日志（不允许静默失效）。</summary>
        public static TempZone GetOrUnconfigured(string recipeId)
        {
            return Table.GetOrLogMissing(recipeId, TempZone.Unconfigured);
        }
    }
}
