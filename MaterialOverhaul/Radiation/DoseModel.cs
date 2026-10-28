namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 复制人的三条剂量途径，全部折算成原版辐射剂量，后果相同（辐射病，致命时由医疗中心救回）。
    /// 以「同样 N 个污染物」比较：吸入约 2 倍于照射，接触约四分之一。
    ///
    /// - 照射：站在有辐射的格子里，原版按格子辐射累积剂量，不改；
    /// - 吸入：每吸入 1000 个污染物加 <see cref="RadiationBalance.InhaleRadsPerThousand"/> rads；
    /// - 接触：身上每 1000 个污染物每周期加 <see cref="RadiationBalance.ContactRadsPerThousandPerCycle"/> rads，
    ///   穿防护服时不计；去污淋浴、洗手池洗掉身上的污染物即可停止。
    /// </summary>
    public static class DoseModel
    {
        /// <summary>一次呼吸吸入 <paramref name="contaminants"/> 个污染物增加的剂量（rads）。</summary>
        public static float InhaledRads(int contaminants)
        {
            return contaminants > 0 ? contaminants / 1000f * RadiationBalance.InhaleRadsPerThousand : 0f;
        }

        /// <summary>身上带 <paramref name="contaminants"/> 个污染物，经过 dt 秒增加的剂量（rads）。</summary>
        public static float ContactRads(int contaminants, float dtSeconds)
        {
            if (contaminants <= 0 || !(dtSeconds > 0f))
            {
                return 0f;
            }

            return contaminants / 1000f * RadiationBalance.ContactRadsPerThousandPerCycle * dtSeconds / RadiationBalance.SecondsPerCycle;
        }

        /// <summary>同样个数的污染物留在格子里时，站在其中的照射剂量（rads/周期，原版规则）。</summary>
        public static float ExposureRadsPerCycle(int contaminants)
        {
            return contaminants > 0 ? contaminants / 1000f * RadiationBalance.ContaminantRadsPerThousand : 0f;
        }
    }
}
