namespace MaterialOverhaul.Hardness
{
    /// <summary>
    /// 机械磨损与伤害余额的累计（V0.0.1 开发计划 §6.3、V0.0.3 开发计划 §2.3、§6.1）。
    /// 纯逻辑，不依赖游戏对象。
    ///
    /// 内部余额直接以**生命值**为单位累计，而不是「整数磨损单位 + 转换取整」：
    /// 后者在 0.05 HP/磨损单位下会丢小数，0.2 s 分步累计时可能永远差一点凑不满 1 HP。
    /// 以 HP 累计后，取整只是把已经攒够的整点交付给调用方，剩余小数原样保留，
    /// 每次扣除都是「减去一个整数」，浮点不会因反复加减而漂移。
    ///
    /// 只有**实际生产推进**时才调用 <see cref="AccumulateProduction"/>：
    /// 单独预热或补热、待机、等待材料、等待操作、暂停、禁用、断电、维修、损坏停机都不累计；
    /// 生产与补热同时发生也只累计一份（调用方只按生产推进量调用一次）。
    ///
    /// 完全修复**不清除**这里的余额（§6.3：机修只清原因，不清磨损小数余额）。
    /// </summary>
    public sealed class WearAccumulator
    {
        /// <summary>
        /// 取整时的浮点容差。分批累计（例如每 0.2 s 一步）会在末位产生误差，
        /// 不能因为差 1e-7 就让「应扣的 1 HP」永远差一点；容差只影响这一步是否算够，
        /// 扣除仍是精确减整数，不会累积漂移。
        /// </summary>
        private const double RoundingTolerance = 1e-6;

        private double damageBalance;

        /// <summary>当前累计、尚未形成整点扣血的伤害余额（HP，含小数）。</summary>
        public float DamageBalance
        {
            get { return (float)damageBalance; }
        }

        /// <summary>
        /// 按实际生产时间累计磨损。实际生产秒数是**确实推进工作的模拟时间**，
        /// 不是「等效满效率工作量」：效率 0.3 下实际工作 20 s 仍扣 1 HP，低效完成同一批磨损更多。
        /// </summary>
        /// <param name="productionSeconds">本次确实推进工作的真实时间（秒），不含预热与补热时间。</param>
        /// <param name="wearPerSecond">磨损速率（磨损单位／生产秒）。</param>
        /// <param name="damagePerWearUnit">每磨损单位折算的生命值（HP）。</param>
        public void AccumulateProduction(float productionSeconds, float wearPerSecond, float damagePerWearUnit)
        {
            if (productionSeconds <= 0f || wearPerSecond <= 0f || damagePerWearUnit <= 0f)
            {
                return;
            }

            damageBalance += (double)productionSeconds * wearPerSecond * damagePerWearUnit;
        }

        /// <summary>
        /// 取走已经攒够的整数生命值，保留小数余额。返回 0 表示还不够扣 1 HP。
        /// </summary>
        public int TakeWholeDamage()
        {
            if (damageBalance + RoundingTolerance < 1.0)
            {
                return 0;
            }

            int whole = (int)(damageBalance + RoundingTolerance);
            damageBalance -= whole;
            if (damageBalance < 0.0)
            {
                damageBalance = 0.0;
            }

            return whole;
        }

        /// <summary>
        /// 剩余未扣的磨损单位数（换算显示用）。转换系数未配置时返回 0。
        /// </summary>
        public float WearUnits(float damagePerWearUnit)
        {
            if (damagePerWearUnit <= 0f)
            {
                return 0f;
            }

            return (float)damageBalance / damagePerWearUnit;
        }

        /// <summary>
        /// 存档恢复用：直接写入伤害余额。读档不清零（§5.3），因此只有载入既有存档数据时才调用。
        /// </summary>
        public void Restore(float value)
        {
            damageBalance = value < 0f ? 0.0 : value;
        }

        /// <summary>迁移用：旧存档把磨损记成「磨损单位数」，按给定系数换算成伤害余额。</summary>
        public void RestoreWearUnits(float wearUnits, float damagePerWearUnit)
        {
            Restore(wearUnits * (damagePerWearUnit > 0f ? damagePerWearUnit : 0f));
        }
    }
}
