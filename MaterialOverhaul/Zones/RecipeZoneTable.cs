using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 配方温区侧表：索引为原版配方标识（<c>ComplexRecipe.id</c>）。
    ///
    /// 原版配方数据结构里不存在"每个配方拥有自己的温度范围"的对应物
    /// （V0.0.1 开发计划 §4.1），因此温区数值必须由本 Mod 提供；
    /// 复用原版配方标识作索引，不新建平行配方系统。
    ///
    /// 侧表**不参与存档**，每次启动重建（§4.3、§8.1）。
    /// 具体填哪些建筑与配方、四边界取什么数值属待定项（§4.4、§10 第 1、2 项），
    /// 未填入前所有配方退化为 Disabled，行为等同原版。
    /// </summary>
    public static class RecipeZoneTable
    {
        private static readonly SideTable<TempZone> Table = new SideTable<TempZone>("配方温区");

        public static int Count
        {
            get { return Table.Count; }
        }

        /// <summary>登记一条。四边界在此处校验，非法值退化为 Disabled 并记录日志（§3.7）。</summary>
        public static void Register(string recipeId, TempZone zone)
        {
            Table.Set(recipeId, TempZoneEvaluator.Validate(recipeId, zone));
        }

        public static bool TryGet(string recipeId, out TempZone zone)
        {
            return Table.TryGet(recipeId, out zone);
        }

        /// <summary>取温区；未匹配时返回 Disabled 并记录一次可定位日志（§9，不允许静默失效）。</summary>
        public static TempZone GetOrDisabled(string recipeId)
        {
            return Table.GetOrLogMissing(recipeId, TempZone.Disabled);
        }
    }
}
