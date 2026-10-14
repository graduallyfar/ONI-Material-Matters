using MaterialOverhaul.Core;

namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 过热规则侧表：索引为建筑 prefab id，值为「过热阈值取熔点的比例」。
    ///
    /// 与温区侧表、要求硬度侧表同构：只对本 Mod 明确列出的建筑生效，
    /// 未匹配的建筑保持原版过热阈值与豁免设定，行为等同原版
    /// （《建筑分类与温区适用范围》§1.5：多数工业建筑当前不参与过热损伤）。
    ///
    /// V0.1.2 起这个比例同时决定**建筑最高工作温度**：上限与过热阈值是同一个数值，
    /// 温区判断通过 <c>MaterialOverhaulFab.TryGetHeatCeiling</c> 取同一个规则值。
    ///
    /// 侧表**不参与存档**，每次启动重建。比例换算规则见 <see cref="OverheatRule"/>。
    /// </summary>
    public static class OverheatRuleTable
    {
        private static readonly SideTable<float> Table = new SideTable<float>("过热阈值比例");

        public static int Count
        {
            get { return Table.Count; }
        }

        /// <param name="meltingPointFraction">熔点比例，取 (0,1] 区间；默认 0.75。</param>
        public static void Register(string buildingId, float meltingPointFraction)
        {
            if (float.IsNaN(meltingPointFraction) || float.IsInfinity(meltingPointFraction)
                || meltingPointFraction <= 0f || meltingPointFraction > 1f)
            {
                ModLog.Error("过热阈值比例：'" + buildingId + "' 的比例 " + meltingPointFraction
                    + " 不在 (0,1] 区间内，已忽略该条目。");
                return;
            }

            Table.Set(buildingId, meltingPointFraction);
        }

        public static bool TryGet(string buildingId, out float meltingPointFraction)
        {
            return Table.TryGet(buildingId, out meltingPointFraction);
        }
    }
}
