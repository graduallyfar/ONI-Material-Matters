using System.Collections.Generic;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 已确认的**建筑最低工作温度**数值（V0.1.2 开发计划 §1.1、§2.1）。
    ///
    /// 与 <see cref="RecipeZoneCatalogue"/> 同构：**只放用户已经定下的数值**，
    /// 未登记的建筑不在这里占位，保持原版行为。
    ///
    /// 为什么与配方分成两个目录：最低工作温度由**建筑类型**决定，切配方、换建造材料都不改变；
    /// 如果把它写进配方条目，就会重新变成「配方拥有四个边界」的旧模型（V0.1.2 §2.1）。
    ///
    /// 已确认条目：
    ///   - 金属精炼器：80 ℃（用户 2026-10-14 给定）。
    /// </summary>
    public static class BuildingZoneCatalogue
    {
        private static readonly Dictionary<string, float> Entries = BuildEntries();

        /// <summary>建筑 prefab id → 最低工作温度（K）。</summary>
        public static IReadOnlyDictionary<string, float> All
        {
            get { return Entries; }
        }

        public static int Count
        {
            get { return Entries.Count; }
        }

        public static bool TryGetFloorKelvin(string prefabId, out float floorKelvin)
        {
            floorKelvin = 0f;
            return !string.IsNullOrEmpty(prefabId) && Entries.TryGetValue(prefabId, out floorKelvin);
        }

        private static Dictionary<string, float> BuildEntries()
        {
            Dictionary<string, float> entries = new Dictionary<string, float>();
            foreach (KeyValuePair<string, BuildingProfile> entry in BuildingProfiles.All)
                entries.Add(entry.Key, Temperatures.FromCelsius(entry.Value.WorkMinCelsius));
            return entries;
        }
    }
}
