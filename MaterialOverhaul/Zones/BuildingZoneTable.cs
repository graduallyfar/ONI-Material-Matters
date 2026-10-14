using System.Collections.Generic;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 已确认的建筑最低工作温度数值（V0.1.2 开发计划 §1.1、§2.1）。
    ///
    /// 最低工作温度 L **由建筑类型决定**：切配方、换建造材料都不改变它。
    /// 因此它按建筑 prefab id 登记，而不是按配方；配方只提供最佳温区
    /// （见 <see cref="RecipeZoneTable"/>），建筑上限由主建造材料熔点推出。
    ///
    /// 与配方温区表同构：**只放用户已经定下的数值**。未登记的建筑不启用本 Mod 温控，
    /// 行为等同原版；即使它的某个配方登记了最佳温区，也会因为缺少建筑下限而整体退化为原版行为。
    ///
    /// 侧表**不参与存档**，每次启动重建。
    ///
    /// 已确认条目：
    ///   - 金属精炼器：最低工作温度 80 ℃（用户 2026-10-14 给定）。
    /// </summary>
    public static class BuildingZoneTable
    {
        private static readonly SideTable<float> Table = new SideTable<float>("建筑最低工作温度");

        public static int Count
        {
            get { return Table.Count; }
        }

        /// <summary>登记一条。非有限值或非正值在此处拒绝并记录日志。</summary>
        public static void Register(string buildingId, float floorKelvin)
        {
            if (string.IsNullOrEmpty(buildingId))
            {
                ModLog.Error("建筑最低工作温度：建筑标识为空，已忽略该条目。");
                return;
            }

            if (float.IsNaN(floorKelvin) || float.IsInfinity(floorKelvin) || floorKelvin <= 0f)
            {
                ModLog.Error("建筑最低工作温度：'" + buildingId + "' 的下限 " + floorKelvin
                    + " K 不是有效温度，已忽略该条目。");
                return;
            }

            Table.Set(buildingId, floorKelvin);
        }

        public static bool TryGet(string buildingId, out float floorKelvin)
        {
            floorKelvin = 0f;
            return !string.IsNullOrEmpty(buildingId) && Table.TryGet(buildingId, out floorKelvin);
        }

        /// <summary>登记过的建筑 id；用于日志与接入层校验。</summary>
        public static IEnumerable<string> All
        {
            get { return Table.Keys; }
        }
    }
}
