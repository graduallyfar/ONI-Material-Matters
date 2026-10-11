namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 加热判断的输入条件（V0.0.1 开发计划 §5.2）。纯数据，便于脱离游戏检查。
    /// </summary>
    public struct HeatingContext
    {
        /// <summary>是否存在已锁定批次。下一生产预告不等于已锁定批次，不能仅凭预告加热。</summary>
        public bool HasLockedBatch;

        /// <summary>该批次材料是否齐备。**已投料同样视为齐备**：原料从输入存储转入加工存储后不再要求输入存储重新备齐。</summary>
        public bool BatchMaterialsReady;

        /// <summary>是否有实际供电。断电不加热。</summary>
        public bool Powered;

        /// <summary>是否被手动禁用。</summary>
        public bool ManuallyDisabled;

        /// <summary>是否因损坏停机。</summary>
        public bool BrokenDown;
    }

    /// <summary>
    /// 预热与补热的许可判断。与生产许可分开判断：自动化红信号只停止生产，
    /// 不阻止配方准备、批次锁定与加热（建筑细节文档 §2.2.1、AGENTS 已确认约束）。
    /// </summary>
    public static class HeatingConditions
    {
        /// <summary>
        /// 是否可以**提出**加热需求（不含供电）。
        ///
        /// 电网需求必须能在未获供电时存在：否则断电时需求归零，来电后建筑不会被重新请求功率，
        /// 表现为「免费注热」或「来电不启动」（V0.0.3 开发计划 §4.2）。
        /// </summary>
        public static bool CanRequestHeat(HeatingContext context)
        {
            return context.HasLockedBatch
                && context.BatchMaterialsReady
                && !context.ManuallyDisabled
                && !context.BrokenDown;
        }

        /// <summary>是否可以**实际**加热：在需求条件之上再要求已确认供电。</summary>
        public static bool CanHeat(HeatingContext context)
        {
            return CanRequestHeat(context) && context.Powered;
        }
    }
}
