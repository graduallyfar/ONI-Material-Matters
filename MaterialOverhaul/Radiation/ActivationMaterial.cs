using System.Collections.Generic;

namespace MaterialOverhaul.Radiation
{
    /// <summary>主建造材料的活化分类；具体元素由游戏接入层按原版标签判定。</summary>
    public enum ActivationCategory
    {
        /// <summary>矿物、岩石、陶瓷、玻璃等非金属无机材料。</summary>
        Mineral = 0,

        /// <summary>金属与金属矿石。</summary>
        Metal = 1,

        /// <summary>塑料、木材等有机材料。</summary>
        Organic = 2
    }

    /// <summary>
    /// 材料活化系数：稳态时建筑自身放射占所受超额辐射的比例。
    /// 参考现实中"钢易活化、铅难活化"的倾向，不追求物理精确（设计方案 §8.2）。
    /// </summary>
    public static class ActivationMaterial
    {
        public const float MetalDefault = 0.03f;
        public const float MineralDefault = 0.015f;
        public const float OrganicDefault = 0.005f;

        private static readonly Dictionary<string, float> ByElement = new Dictionary<string, float>
        {
            { "Steel", 0.05f },
            { "Iron", 0.05f },
            { "Cobalt", 0.05f },
            { "Copper", 0.04f },
            { "Gold", 0.04f },
            { "Aluminum", 0.025f },
            { "Lead", 0.01f },
            { "DepletedUranium", 0.01f }
        };

        /// <summary>按元素 id 取活化系数；未单独列出的元素按分类取默认值。</summary>
        public static float FractionFor(string elementId, ActivationCategory category)
        {
            float value;
            if (!string.IsNullOrEmpty(elementId) && ByElement.TryGetValue(elementId, out value))
            {
                return value;
            }

            switch (category)
            {
                case ActivationCategory.Metal:
                    return MetalDefault;

                case ActivationCategory.Organic:
                    return OrganicDefault;

                default:
                    return MineralDefault;
            }
        }

        /// <summary>单独列出的元素数量（测试与日志用）。</summary>
        public static int ListedCount
        {
            get { return ByElement.Count; }
        }
    }
}
