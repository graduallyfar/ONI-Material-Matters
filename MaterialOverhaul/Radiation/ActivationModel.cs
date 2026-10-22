using System;

namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 建筑活化度的计算（设计方案 §8.2）。活化度 A 的单位直接取 rads/周期，即建筑自身的放射强度：
    ///
    ///   dA/dt = λ·f·E − λ·A，E = max(0, R − 感生估计 − 门槛)
    ///
    /// 稳态 A∞ = f·E；离开辐射源后按半衰期衰减。每次更新按区间内 E 不变做精确指数积分，
    /// 与更新间隔无关，不会因为采样步长变化而改变结果。
    /// </summary>
    public static class ActivationModel
    {
        /// <summary>按半衰期（周期）求每秒衰变常数。</summary>
        public static float DecayPerSecond(float halfLifeCycles)
        {
            if (!(halfLifeCycles > 0f))
            {
                return 0f;
            }

            return (float)(Math.Log(2.0) / (halfLifeCycles * RadiationBalance.SecondsPerCycle));
        }

        /// <summary>建筑默认半衰期对应的衰变常数。</summary>
        public static float BuildingDecayPerSecond
        {
            get { return DecayPerSecond(RadiationBalance.BuildingHalfLifeCycles); }
        }

        /// <summary>
        /// 能造成活化的超额辐射：从实测辐射中扣掉活化物自身贡献的上界，再扣门槛。
        /// 扣掉自身贡献使活化物之间不能互相维持，离开真正的辐射源后一定会衰减。
        /// </summary>
        public static float EffectiveExcess(float measuredRads, float inducedUpperBoundRads, float thresholdRads)
        {
            if (float.IsNaN(measuredRads) || float.IsInfinity(measuredRads))
            {
                return 0f;
            }

            float induced = float.IsNaN(inducedUpperBoundRads) || inducedUpperBoundRads < 0f ? 0f : inducedUpperBoundRads;
            float excess = measuredRads - induced - thresholdRads;
            return excess > 0f ? excess : 0f;
        }

        /// <summary>按默认门槛求超额辐射。</summary>
        public static float EffectiveExcess(float measuredRads, float inducedUpperBoundRads)
        {
            return EffectiveExcess(measuredRads, inducedUpperBoundRads, RadiationBalance.ActivationThresholdRads);
        }

        /// <summary>给定超额辐射时的稳态活化度。</summary>
        public static float SteadyState(float excessRads, float fraction)
        {
            if (!(excessRads > 0f) || !(fraction > 0f))
            {
                return 0f;
            }

            return excessRads * fraction;
        }

        /// <summary>经过 dt 秒后的活化度（区间内超额辐射视为不变）。</summary>
        public static float Step(float activation, float excessRads, float fraction, float dtSeconds, float decayPerSecond)
        {
            float current = float.IsNaN(activation) || activation < 0f ? 0f : activation;
            if (!(dtSeconds > 0f) || !(decayPerSecond > 0f))
            {
                return current;
            }

            float target = SteadyState(excessRads, fraction);
            float result = target + (current - target) * (float)Math.Exp(-decayPerSecond * dtSeconds);
            return result > 0f ? result : 0f;
        }

        /// <summary>按建筑默认半衰期步进。</summary>
        public static float Step(float activation, float excessRads, float fraction, float dtSeconds)
        {
            return Step(activation, excessRads, fraction, dtSeconds, BuildingDecayPerSecond);
        }

        /// <summary>自身放射强度：低于下限时不放射。</summary>
        public static float EmitRads(float activation)
        {
            return activation >= RadiationBalance.EmitMinRads ? activation : 0f;
        }

        /// <summary>是否需要把新的放射强度通知原生 Sim：开关变化或相对变化超过阈值。</summary>
        public static bool NeedsRefresh(float previousEmitRads, float nextEmitRads)
        {
            bool wasOn = previousEmitRads > 0f;
            bool isOn = nextEmitRads > 0f;
            if (wasOn != isOn)
            {
                return true;
            }

            if (!isOn)
            {
                return false;
            }

            return Math.Abs(nextEmitRads - previousEmitRads) > previousEmitRads * RadiationBalance.EmitRefreshRelativeChange;
        }

        /// <summary>未受照、活化度已很低时可以从活跃名单移除。</summary>
        public static bool CanDrop(float activation, float excessRads)
        {
            return !(excessRads > 0f) && !(activation >= RadiationBalance.ActivationDropRads);
        }
    }
}
