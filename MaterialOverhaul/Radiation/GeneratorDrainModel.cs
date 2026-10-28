using System;

namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 辐射粒子发生器消耗辐射：发生器吸收的辐射由周围已显现的物质付账，原版来源保持永久。
    ///
    /// 原生辐射场不会告诉我们一格的辐射来自哪里，所以按发生器周围已知的显现源估算各自的份额
    /// （强度越大、越近，份额越大），按份额扣；估不到来源的那部分（原版来源、太空辐射、远处的反应堆）不扣。
    /// 原版发生器每吸收 10 rads 产生 1 点粒子，产量不变，只是多了「谁来付账」。
    /// </summary>
    public static class GeneratorDrainModel
    {
        /// <summary>
        /// 一次采样吸收的辐射（rads）：原版每次采样存入「每秒速率 × 采样间隔 × 0.1」点粒子，
        /// 但不超过粒子仓的剩余容量；每点粒子对应 <see cref="RadiationBalance.RadsPerParticle"/> rads。
        /// </summary>
        public static float AbsorbedRads(float perSecondRate, float sampleSeconds, float remainingCapacity)
        {
            if (!(perSecondRate > 0f) || !(sampleSeconds > 0f) || !(remainingCapacity > 0f))
            {
                return 0f;
            }

            float particles = Math.Min(perSecondRate * sampleSeconds * 0.1f, remainingCapacity);
            return AccumulationModel.ParticleRads(particles);
        }

        /// <summary>
        /// 估算一个显现源在发生器处贡献的辐射（rads/周期）：放射半径内按距离线性减弱，半径外为 0。
        /// 只用于分摊份额，不追求与原生射线计算一致。
        /// </summary>
        public static float EstimatedContribution(float emitRads, float distanceCells, int radiusCells)
        {
            if (!(emitRads > 0f) || float.IsNaN(distanceCells) || distanceCells < 0f || radiusCells < 0)
            {
                return 0f;
            }

            if (distanceCells > radiusCells + 0.5f)
            {
                return 0f;
            }

            float scale = 1f - distanceCells / (radiusCells + 1f);
            return scale > 0f ? emitRads * scale : 0f;
        }

        /// <summary>
        /// 一个源应付的账（rads）：吸收量 × 该源估计贡献 ÷ max(实测辐射, 全部估计之和)。
        /// 实测辐射大于估计之和时，多出的部分视为估不到的来源，不扣。
        /// </summary>
        public static float Charge(float absorbedRads, float sourceRads, float totalEstimatedRads, float measuredRads)
        {
            if (!(absorbedRads > 0f) || !(sourceRads > 0f))
            {
                return 0f;
            }

            float denominator = Math.Max(totalEstimatedRads > 0f ? totalEstimatedRads : 0f, measuredRads > 0f ? measuredRads : 0f);
            if (!(denominator > 0f))
            {
                return 0f;
            }

            float share = sourceRads / denominator;
            return absorbedRads * (share < 1f ? share : 1f);
        }
    }
}
