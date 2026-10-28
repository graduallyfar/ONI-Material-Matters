using System;

namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 拆除显现建筑的结算（设计评审 2026-10-23）：建筑的放射性不随拆除消失，
    /// 而是集中到一部分建材里变成原版核废料；其余建材正常掉落、不带放射性。
    ///
    /// - 转换比例 = 自身放射 ÷ <see cref="RadiationBalance.DecommissionRadsPerFullConversion"/>，
    ///   上限 <see cref="RadiationBalance.DecommissionMaxFraction"/>；
    /// - 自身放射低于 <see cref="RadiationBalance.DecommissionMinRads"/> 时不转换；
    /// - 核废料带上放射性污染物，个数按「每 1000 个 = 1 rads/周期」等于建筑当前的自身放射，
    ///   让拆下来的放射性仍然可见、可检测，之后按污染物与核废料的原版规则衰减和处理。
    /// </summary>
    public static class DecommissionModel
    {
        /// <summary>主建造材料中转成核废料的比例，0 表示不转换。</summary>
        public static float WasteFraction(float emitRads)
        {
            if (float.IsNaN(emitRads) || emitRads < RadiationBalance.DecommissionMinRads)
            {
                return 0f;
            }

            float fraction = emitRads / RadiationBalance.DecommissionRadsPerFullConversion;
            return fraction > RadiationBalance.DecommissionMaxFraction ? RadiationBalance.DecommissionMaxFraction : fraction;
        }

        /// <summary>主建造材料的核废料质量（kg）。</summary>
        public static float WasteMass(float primaryMass, float emitRads)
        {
            if (float.IsNaN(primaryMass) || !(primaryMass > 0f))
            {
                return 0f;
            }

            return primaryMass * WasteFraction(emitRads);
        }

        /// <summary>核废料携带的放射性污染物个数，上限与单格污染物上限一致。</summary>
        public static int WasteContaminants(float emitRads)
        {
            if (WasteFraction(emitRads) <= 0f)
            {
                return 0;
            }

            double count = Math.Round((double)emitRads * 1000.0 / RadiationBalance.ContaminantRadsPerThousand);
            return count >= RadiationBalance.MaxCellContaminants ? RadiationBalance.MaxCellContaminants : (int)count;
        }
    }
}
