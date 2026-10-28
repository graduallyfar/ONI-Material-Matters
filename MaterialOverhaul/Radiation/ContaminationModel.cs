using System;

namespace MaterialOverhaul.Radiation
{
    /// <summary>格子中物质的相态（游戏接入层由原版元素判定）。</summary>
    public enum CellPhase
    {
        Vacuum = 0,
        Gas = 1,
        Liquid = 2,
        Solid = 3
    }

    /// <summary>一次往格子写入污染物的计划：先清除其他病菌，再添加放射性污染物。</summary>
    public struct ContaminationPlan
    {
        /// <summary>需要清除的其他病菌编号；<see cref="ContaminationModel.NoDisease"/> 表示无需清除。</summary>
        public byte ClearIdx;

        /// <summary>需要清除的数量。</summary>
        public int ClearCount;

        /// <summary>需要添加的放射性污染物数量。</summary>
        public int AddCount;

        public bool HasWork
        {
            get { return AddCount > 0; }
        }
    }

    /// <summary>
    /// 格子里的物质、管道里的流体、地上的物品显现后的放射：按超出显现限度的部分添加原版放射性污染物，
    /// 此后的流动、混合、衰减和放射都交给原版病菌系统。
    /// 放射性污染物优先级最高：已有其他病菌时先清除再写入。
    /// </summary>
    public static class ContaminationModel
    {
        /// <summary>原版表示"无病菌"的编号。</summary>
        public const byte NoDisease = byte.MaxValue;

        /// <summary>参与计算的有效质量（kg）。真空不污染；气体按质量放大。</summary>
        public static float EffectiveMassKg(float massKg, CellPhase phase)
        {
            if (!(massKg > 0f) || float.IsInfinity(massKg))
            {
                return 0f;
            }

            switch (phase)
            {
                case CellPhase.Gas:
                    return massKg * RadiationBalance.GasMassMultiplier;

                case CellPhase.Liquid:
                case CellPhase.Solid:
                    return massKg;

                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 物质能否累积和被污染。核废料、铀矿等原版放射物质本身就是来源，不再累积、不添加污染物；
        /// 它们照射到的其他物质照常累积。
        /// </summary>
        public static bool CanContaminate(float elementRadsPer1000Mass)
        {
            return !(elementRadsPer1000Mass > 0f);
        }

        /// <summary>
        /// 经过 dt 秒应添加的污染物个数（四舍五入）。<paramref name="excessRads"/> 是超出显现限度部分的
        /// 衰减速率 λ·(D − L)（rads/周期），见 <see cref="AccumulationModel.ExcessRate"/>。
        /// </summary>
        public static int ContaminantsToAdd(float effectiveMassKg, float excessRads, float dtSeconds)
        {
            if (!(effectiveMassKg > 0f) || !(excessRads > 0f) || !(dtSeconds > 0f))
            {
                return 0;
            }

            double value = (double)RadiationBalance.ContaminantsPerTonRadSecond
                * (effectiveMassKg / 1000.0) * excessRads * dtSeconds;
            if (value >= RadiationBalance.MaxCellContaminants)
            {
                return RadiationBalance.MaxCellContaminants;
            }

            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 按当前写入速度，污染物达到稳态时的自身放射（rads/周期）。原版污染物半衰期 1 周期，
        /// 稳态个数 = 每秒添加量 ÷ 每秒衰变常数。低于自身放射下限时调用方不写入（忽略不计）。
        /// </summary>
        public static float SteadySelfRads(float effectiveMassKg, float excessRads)
        {
            if (!(effectiveMassKg > 0f) || !(excessRads > 0f) || float.IsInfinity(effectiveMassKg) || float.IsInfinity(excessRads))
            {
                return 0f;
            }

            double perSecond = (double)RadiationBalance.ContaminantsPerTonRadSecond * (effectiveMassKg / 1000.0) * excessRads;
            double decay = Math.Log(2.0) / (RadiationBalance.ContaminantHalfLifeCycles * RadiationBalance.SecondsPerCycle);
            return (float)(perSecond / decay / 1000.0 * RadiationBalance.ContaminantRadsPerThousand);
        }

        /// <summary>格子中放射性污染物自身的放射（rads/周期），只作用于本格。</summary>
        public static float SelfRads(byte currentIdx, int currentCount, byte contaminantIdx)
        {
            if (currentIdx != contaminantIdx || currentIdx == NoDisease || currentCount <= 0)
            {
                return 0f;
            }

            return currentCount / 1000f * RadiationBalance.ContaminantRadsPerThousand;
        }

        /// <summary>
        /// 根据格子现有病菌决定写入计划：
        /// 无病菌或已是污染物时直接添加（不超过上限）；有其他病菌时先全部清除，再添加。
        /// 没有可添加的量时什么都不做，也不清除其他病菌。
        /// </summary>
        public static ContaminationPlan Plan(byte currentIdx, int currentCount, byte contaminantIdx, int toAdd)
        {
            ContaminationPlan plan = new ContaminationPlan { ClearIdx = NoDisease, ClearCount = 0, AddCount = 0 };
            if (toAdd <= 0 || contaminantIdx == NoDisease)
            {
                return plan;
            }

            int existing = 0;
            if (currentIdx == contaminantIdx)
            {
                existing = currentCount > 0 ? currentCount : 0;
            }
            else if (currentIdx != NoDisease && currentCount > 0)
            {
                plan.ClearIdx = currentIdx;
                plan.ClearCount = currentCount;
            }

            int room = RadiationBalance.MaxCellContaminants - existing;
            if (room <= 0)
            {
                plan.ClearIdx = NoDisease;
                plan.ClearCount = 0;
                return plan;
            }

            plan.AddCount = toAdd < room ? toAdd : room;
            return plan;
        }
    }
}
