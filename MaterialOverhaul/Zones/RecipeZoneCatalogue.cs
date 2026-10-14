using System.Collections.Generic;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 一条配方温区条目。用「建筑 + 输入材料 + 输出材料」描述，而不是直接写原版配方标识：
    /// 配方标识由建筑名与材料名拼接而成（<c>ComplexRecipeManager.MakeRecipeID</c>），
    /// 直接写字符串容易因为标识格式或材料名变化而静默失配。接入层按本描述在原版配方表里解析出标识，
    /// 解析失败会记录可定位日志（V0.0.1 开发计划 §4.2、§9）。
    ///
    /// V0.1.2 起 `Zone` 只携带**配方最佳温区**两个边界；建筑下限与材料上限不在这里。
    /// </summary>
    public sealed class RecipeZoneSpec
    {
        /// <summary>原版建筑 prefab id，例如 MetalRefinery。</summary>
        public string FabricatorId;

        /// <summary>输入材料标识（元素 tag）。多输入时按原版配方书写顺序给出，但匹配与顺序无关。</summary>
        public string[] Inputs;

        /// <summary>输出材料标识（元素 tag）。匹配与顺序无关。</summary>
        public string[] Outputs;

        /// <summary>配方最佳温区两个边界，单位开尔文。</summary>
        public TempZone Zone;

        /// <summary>数值来源说明，只用于日志与文档对照，不参与运算。</summary>
        public string Source;

        /// <summary>
        /// 给定的输入输出材料标识是否与本条目一致（按多重集合比较，与顺序无关）。
        /// 纯字符串逻辑，可在判断层单独检查。
        /// </summary>
        public bool MatchesMaterials(IList<string> inputs, IList<string> outputs)
        {
            return SameElements(Inputs, inputs) && SameElements(Outputs, outputs);
        }

        private static bool SameElements(string[] expected, IList<string> actual)
        {
            if (expected == null || actual == null || expected.Length != actual.Count)
            {
                return false;
            }

            List<string> pool = new List<string>(actual);
            foreach (string item in expected)
            {
                int index = pool.IndexOf(item);
                if (index < 0)
                {
                    return false;
                }

                pool.RemoveAt(index);
            }

            return pool.Count == 0;
        }
    }

    /// <summary>
    /// 已确认的配方最佳温区数值。**只放用户已经定下的数值**，未定配方不在这里占位：
    /// 未登记的配方保持原版行为，并由接入层记录一条待定日志（避免「名单内配方静默落空」）。
    ///
    /// 已确认条目（建筑下限见 <see cref="BuildingZoneTable"/>，建筑上限由主建造材料推出）：
    ///   - 金属精炼器 铝矿 → 铝：最佳温区 450–650 ℃。
    ///
    /// 旧版「300／450／650／800 ℃ 四边界」中的 300 ℃ 已改由建筑下限承担（本版 80 ℃），
    /// 800 ℃ 不再是配方属性：它由主建造材料熔点 × 0.75 决定，因此同一配方可以用不同建材跑出
    /// 不同的最高工作温度（建筑细节文档 §2.1.5）。
    /// </summary>
    public static class RecipeZoneCatalogue
    {
        private static readonly RecipeZoneSpec[] Entries = BuildEntries();

        public static IList<RecipeZoneSpec> All
        {
            get { return Entries; }
        }

        public static int Count
        {
            get { return Entries.Length; }
        }

        private static RecipeZoneSpec[] BuildEntries()
        {
            return new[]
            {
                new RecipeZoneSpec
                {
                    FabricatorId = "MetalRefinery",
                    Inputs = new[] { "AluminumOre" },
                    Outputs = new[] { "Aluminum" },
                    Zone = TempZone.Celsius(450f, 650f),
                    Source = "用户给定：铝矿生成铝最佳温区 450–650 ℃（V0.1.2 起建筑下限与材料上限独立）"
                }
            };
        }
    }
}
