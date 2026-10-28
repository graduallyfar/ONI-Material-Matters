using System;

namespace MaterialOverhaul.Radiation
{
    /// <summary>
    /// 累积与显现的计算。累积量 D 的单位是 rads，受照强度 R 的单位是 rads/周期：
    ///
    ///   dD/dt = R − λ·D，E = f·λ·max(0, D − L)，λ = ln2 / 10 周期
    ///
    /// 持续受照时 D 趋向 R/λ（约 14.4 R）；稳态自身放射 f·(R − λ·L)，所以每种材料都有一个
    /// 「能让它显现的最低持续照射」λ·L。每次更新按区间内 R 不变做精确指数积分，结果与更新间隔无关。
    /// 物质自身的放射不计入它自己的累积，由调用方在传入 R 之前扣掉。
    /// </summary>
    public static class AccumulationModel
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

        /// <summary>累积量默认半衰期对应的每秒衰变常数。</summary>
        public static float DefaultDecayPerSecond
        {
            get { return DecayPerSecond(RadiationBalance.AccumulationHalfLifeCycles); }
        }

        /// <summary>累积量默认半衰期对应的每周期衰变常数 λ（约 0.069）。</summary>
        public static float DecayPerCycle
        {
            get { return DefaultDecayPerSecond * RadiationBalance.SecondsPerCycle; }
        }

        /// <summary>持续受照 <paramref name="rads"/>（rads/周期）时的稳态累积量。</summary>
        public static float SteadyState(float rads)
        {
            if (!(rads > 0f) || float.IsInfinity(rads))
            {
                return 0f;
            }

            return Clamp(rads / DecayPerCycle);
        }

        /// <summary>经过 dt 秒后的累积量（区间内受照强度视为不变）。</summary>
        public static float Step(float accumulation, float rads, float dtSeconds, float decayPerSecond)
        {
            float current = Clamp(accumulation);
            if (!(dtSeconds > 0f) || !(decayPerSecond > 0f))
            {
                return current;
            }

            float received = rads > 0f && !float.IsInfinity(rads) ? rads : 0f;
            float target = received / (decayPerSecond * RadiationBalance.SecondsPerCycle);
            float result = target + (current - target) * (float)Math.Exp(-decayPerSecond * dtSeconds);
            return Clamp(result);
        }

        /// <summary>按默认半衰期步进。</summary>
        public static float Step(float accumulation, float rads, float dtSeconds)
        {
            return Step(accumulation, rads, dtSeconds, DefaultDecayPerSecond);
        }

        /// <summary>超出显现限度部分的衰减速率 λ·(D − L)（rads/周期）；污染物按它写入。</summary>
        public static float ExcessRate(float accumulation, float limit)
        {
            if (float.IsNaN(accumulation) || float.IsNaN(limit))
            {
                return 0f;
            }

            float over = accumulation - (limit > 0f ? limit : 0f);
            return over > 0f ? over * DecayPerCycle : 0f;
        }

        /// <summary>自身放射强度 f·λ·(D − L)（rads/周期）；不到下限时忽略不计。</summary>
        public static float EmitRads(float accumulation, float limit, float fraction)
        {
            if (!(fraction > 0f))
            {
                return 0f;
            }

            float rads = fraction * ExcessRate(accumulation, limit);
            return rads >= RadiationBalance.EmitMinRads ? rads : 0f;
        }

        /// <summary>能让材料显现的最低持续照射 λ·L（rads/周期）。</summary>
        public static float MinSustainedRads(float limit)
        {
            return limit > 0f ? limit * DecayPerCycle : 0f;
        }

        /// <summary>持续受照时的稳态自身放射 f·(R − λ·L)（rads/周期），不计下限。</summary>
        public static float SteadyEmitRads(float rads, float limit, float fraction)
        {
            if (!(fraction > 0f))
            {
                return 0f;
            }

            float over = rads - MinSustainedRads(limit);
            return over > 0f ? fraction * over : 0f;
        }

        /// <summary>
        /// 自身放射为 <paramref name="emitRads"/> 时对应的累积量 L + E/(f·λ)。
        /// 用于把旧存档里的活化度（直接以自身放射表示）折算成累积量。
        /// </summary>
        public static float AccumulationForEmission(float emitRads, float limit, float fraction)
        {
            if (!(emitRads > 0f) || !(fraction > 0f))
            {
                return 0f;
            }

            return Clamp((limit > 0f ? limit : 0f) + emitRads / (fraction * DecayPerCycle));
        }

        /// <summary>一次性存入累积量（辐射粒子撞击）。</summary>
        public static float Deposit(float accumulation, float rads)
        {
            if (!(rads > 0f))
            {
                return Clamp(accumulation);
            }

            return Clamp(Clamp(accumulation) + rads);
        }

        /// <summary>
        /// 辐射粒子发生器付账：吸收 <paramref name="absorbedRads"/> 的放射，累积量按「吸收量 ÷ 放射比例」扣减。
        /// 显现物余下的全部放射是 f·(D − L)，扣掉 X/f 正好少放出 X。
        /// </summary>
        public static float Drain(float accumulation, float absorbedRads, float fraction)
        {
            float current = Clamp(accumulation);
            if (!(absorbedRads > 0f) || !(fraction > 0f))
            {
                return current;
            }

            float next = current - absorbedRads / fraction;
            return next > 0f ? next : 0f;
        }

        /// <summary>辐射粒子能量换成累积量（rads）。</summary>
        public static float ParticleRads(float payload)
        {
            if (!(payload > 0f) || float.IsInfinity(payload))
            {
                return 0f;
            }

            return payload * RadiationBalance.RadsPerParticle;
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

        /// <summary>未受照、累积量已很低时可以清零、不再跟踪。</summary>
        public static bool CanDrop(float accumulation, float rads)
        {
            return !(rads > 0f) && !(accumulation >= RadiationBalance.AccumulationDropRads);
        }

        /// <summary>把累积量限制在 [0, 上限]；无效值按 0。</summary>
        public static float Clamp(float accumulation)
        {
            if (float.IsNaN(accumulation) || !(accumulation > 0f))
            {
                return 0f;
            }

            return accumulation > RadiationBalance.MaxAccumulationRads ? RadiationBalance.MaxAccumulationRads : accumulation;
        }
    }
}
