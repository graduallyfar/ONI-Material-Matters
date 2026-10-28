using System.Collections.Generic;

namespace MaterialOverhaul.Radiation
{
    /// <summary>材料的显现分类；具体元素由游戏接入层按原版标签与相态判定。</summary>
    public enum MaterialCategory
    {
        /// <summary>矿物、岩石、陶瓷、玻璃等非金属无机材料。</summary>
        Mineral = 0,

        /// <summary>金属与金属矿石。</summary>
        Metal = 1,

        /// <summary>塑料、木材等有机材料。</summary>
        Organic = 2,

        /// <summary>液体、气体：显现后按放射性污染物放射。</summary>
        Fluid = 3
    }

    /// <summary>一种材料的显现参数。</summary>
    public struct ManifestProfile
    {
        /// <summary>显现限度 L（rads）：累积量超过它才开始放射。</summary>
        public float Limit;

        /// <summary>显现后放射比例 f：自身放射 = f·λ·(D − L)。</summary>
        public float Fraction;

        public ManifestProfile(float limit, float fraction)
        {
            Limit = limit;
            Fraction = fraction;
        }
    }

    /// <summary>
    /// 材料的显现限度与放射比例。参考现实中「钢易活化、铅难活化」的倾向，不追求物理精确；
    /// 铅依然是最不容易显现的屏蔽材料。
    /// </summary>
    public static class ManifestMaterial
    {
        public static readonly ManifestProfile MetalDefault = new ManifestProfile(3000f, 0.03f);
        public static readonly ManifestProfile MineralDefault = new ManifestProfile(5000f, 0.015f);
        public static readonly ManifestProfile OrganicDefault = new ManifestProfile(6000f, 0.005f);
        public static readonly ManifestProfile FluidDefault = new ManifestProfile(RadiationBalance.FluidLimitRads, RadiationBalance.CellDrainFraction);

        private static readonly Dictionary<string, ManifestProfile> ByElement = new Dictionary<string, ManifestProfile>
        {
            { "Steel", new ManifestProfile(2000f, 0.05f) },
            { "Iron", new ManifestProfile(2000f, 0.05f) },
            { "Cobalt", new ManifestProfile(2000f, 0.05f) },
            { "Copper", new ManifestProfile(2500f, 0.04f) },
            { "Gold", new ManifestProfile(2500f, 0.04f) },
            { "Aluminum", new ManifestProfile(4000f, 0.025f) },
            { "Lead", new ManifestProfile(10000f, 0.01f) },
            { "DepletedUranium", new ManifestProfile(10000f, 0.01f) }
        };

        /// <summary>按元素 id 取显现参数；流体一律按流体，未单独列出的固体按分类取默认值。</summary>
        public static ManifestProfile For(string elementId, MaterialCategory category)
        {
            if (category == MaterialCategory.Fluid)
            {
                return FluidDefault;
            }

            ManifestProfile value;
            if (!string.IsNullOrEmpty(elementId) && ByElement.TryGetValue(elementId, out value))
            {
                return value;
            }

            switch (category)
            {
                case MaterialCategory.Metal:
                    return MetalDefault;

                case MaterialCategory.Organic:
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
