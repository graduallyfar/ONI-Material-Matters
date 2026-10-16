using System;

namespace MaterialOverhaul.Core
{
    /// <summary>连续转换每次结算1 s的原版工作量；还原真实生产秒数，不以温度等效产量计磨损。</summary>
    public static class ProductionAccounting
    {
        public static float ConversionSeconds(float consumedMass, float nominalRate, float combinedSpeed)
        {
            float rate = nominalRate * combinedSpeed;
            if (!(rate > 0f) || float.IsInfinity(rate) || !(consumedMass > 0f) || float.IsInfinity(consumedMass)) return 0f;
            return Math.Min(1f, consumedMass / rate);
        }
    }

    /// <summary>同栋多转换器生产时间取区间并集，避免脱盐器同时处理多种输入时重复磨损。</summary>
    public sealed class ProductionIntervalCredit
    {
        private double creditedThrough = double.NegativeInfinity;
        private double creditedStart = double.NegativeInfinity;
        public float Add(double now, float duration)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || !(duration > 0f) || float.IsInfinity(duration)) return 0f;
            if (now < creditedThrough) return 0f;
            double start = now-duration;
            double overlap = Math.Max(0d,Math.Min(now,creditedThrough)-Math.Max(start,creditedStart));
            float added = (float)(duration-overlap);
            creditedStart = start <= creditedThrough ? Math.Min(start,creditedStart) : start;
            creditedThrough = now;
            return added;
        }
    }
}
