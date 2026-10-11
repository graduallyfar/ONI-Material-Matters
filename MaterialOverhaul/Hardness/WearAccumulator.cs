namespace MaterialOverhaul.Hardness
{
    /// <summary>
    /// 机械磨损累计（V0.0.1 开发计划 §6.3）。纯逻辑，不依赖游戏对象。
    ///
    /// 只有**实际生产推进**时才调用 <see cref="AccumulateProduction"/>：
    /// 单独预热或补热、待机、等待材料、等待操作、暂停、禁用、断电、维修、损坏停机都不累计；
    /// 生产与补热同时发生也只累计一份（调用方只按生产推进量调用一次）。
    ///
    /// 累计值为浮点，保留尚未形成整数扣血的累计部分；
    /// 暂停、换订单、切换模式、取消订单、读档都不清除已累计磨损。
    /// </summary>
    public sealed class WearAccumulator
    {
        private float wear;

        /// <summary>当前累计磨损（含尚未成形的部分）。</summary>
        public float Wear
        {
            get { return wear; }
        }

        /// <summary>
        /// 按实际生产时间累计。
        /// </summary>
        /// <param name="productionSeconds">本次实际推进的生产时间（秒），来自生产进度增量，不含加热时间。</param>
        /// <param name="wearPerSecond">磨损速率。**占位参数**，磨损与伤害的转换系数待数值阶段确定（§6.4）。</param>
        public void AccumulateProduction(float productionSeconds, float wearPerSecond)
        {
            if (productionSeconds <= 0f || wearPerSecond <= 0f)
            {
                return;
            }

            wear += productionSeconds * wearPerSecond;
        }

        /// <summary>
        /// 取走已成形为整数的部分，保留小数部分继续累计。
        /// 返回值为磨损单位数，交给调用方按数值阶段的系数转换为生命值伤害。
        /// </summary>
        public int TakeWholeWear()
        {
            if (wear < 1f)
            {
                return 0;
            }

            int whole = (int)wear;
            wear -= whole;
            return whole;
        }

        /// <summary>
        /// 存档恢复用：直接写入累计值。
        /// 读档不清零（§8.3），因此只有载入既有存档数据时才调用。
        /// </summary>
        public void Restore(float value)
        {
            wear = value < 0f ? 0f : value;
        }

        /// <summary>
        /// 维修完成后的清零。V0.0.1 不接入维修流程，此方法留给后续版本使用。
        /// </summary>
        public void ResetAfterRepair()
        {
            wear = 0f;
        }
    }
}
