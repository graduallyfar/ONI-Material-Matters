namespace MaterialOverhaul.Hardness
{
    /// <summary>
    /// 磨损到生命值伤害的转换。
    ///
    /// **转换系数属于待定项**（开发计划 §6.4、§10 第 10 项），默认 0：
    /// 磨损照常累计，但暂不折算成伤害——既不扣血，也不丢弃已累计的磨损值。
    /// 填入数值后机制立即生效，不需要改动调用方。
    /// </summary>
    public static class DamageConversion
    {
        /// <summary>每个磨损单位折算的生命值伤害点数。数值阶段填写前为 0。</summary>
        public static float DamagePerWearUnit;

        public static bool IsConfigured
        {
            get { return DamagePerWearUnit > 0f; }
        }

        /// <summary>把整数磨损单位折算成伤害点数（向下取整）。未配置时返回 0。</summary>
        public static int DamageFor(int wearUnits)
        {
            if (wearUnits <= 0 || DamagePerWearUnit <= 0f)
            {
                return 0;
            }

            return (int)(wearUnits * DamagePerWearUnit);
        }
    }
}
