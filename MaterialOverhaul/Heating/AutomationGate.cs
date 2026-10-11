namespace MaterialOverhaul.Heating
{
    /// <summary>
    /// 「允许／暂停生产」自动化输入的判定（建筑细节文档 §2.2.3、V0.0.3 开发计划 §10.4）。
    /// 纯逻辑，便于离线核对默认放行与「红信号是否只由自动化造成」这两条容易搞错的口径。
    /// </summary>
    public static class AutomationGate
    {
        /// <summary>
        /// 自动化是否允许生产。
        ///
        /// 绿色允许、红色暂停；**没有输入端口、或标记尚未初始化时按允许处理**——
        /// 这与原版 <c>LogicOperationalController.unNetworkedValue = 1</c> 的语义一致：
        /// 新建筑没接线时默认放行，不会因为新增端口而变成默认停产。
        /// </summary>
        public static bool AllowsProduction(bool hasInputPort, bool flagKnown, bool flagValue)
        {
            if (!hasInputPort || !flagKnown)
            {
                return true;
            }

            return flagValue;
        }

        /// <summary>
        /// 红信号是否**只**由自动化造成：机器本身可用（供电、未禁用、未损坏、功能正常）时才成立。
        ///
        /// 只有这一种情况才保留订单需求与取料（原版「不可运行就取消全部取料」对它不适用）；
        /// 断电、手动禁用、损坏停机等仍走原版路径。
        /// </summary>
        public static bool IsAutomationOnlyBlock(
            bool hasInputPort,
            bool flagKnown,
            bool flagValue,
            bool powered,
            bool manuallyDisabled,
            bool brokenDown,
            bool functional)
        {
            if (!hasInputPort || AllowsProduction(hasInputPort, flagKnown, flagValue))
            {
                return false;
            }

            return powered && !manuallyDisabled && !brokenDown && functional;
        }
    }
}
