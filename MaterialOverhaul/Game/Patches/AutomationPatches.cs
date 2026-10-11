using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 自动化红信号下的订单需求保留（建筑细节文档 §2.2.3、V0.0.3 开发计划 §3.1）。
    ///
    /// 已核实原版实现：
    /// <code>
    /// private void OnOperationalChanged(object data) {
    ///     if (data) queueDirty = true;
    ///     else CancelAllOpenOrders();   // 清空预取额度并取消全部取料
    ///     UpdateChore();
    /// }
    /// </code>
    /// 也就是说，只要总体不可运行，原版就会连**订单需求与取料**一起取消。设计要求「红色只暂停生产，
    /// 不影响配方准备」：因此当红信号的唯一来源是「允许／暂停生产」输入端口时，这里跳过
    /// <c>CancelAllOpenOrders</c>，只保留原版对操作任务的处理（红信号下 <c>UpdateChore</c> 会撤销任务）。
    ///
    /// 断电、手动禁用、损坏停机、输出堵塞等其它原因造成的不可运行仍走原版路径（取消取料），
    /// 避免在真正无法处理的建筑上堆积原料。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "OnOperationalChanged")]
    internal static class Patch_ComplexFabricator_OnOperationalChanged
    {
        private static bool Prefix(ComplexFabricator __instance)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null || !fab.BlockedByAutomationOnly)
                {
                    return true;
                }

                // 保留订单需求与取料：只让队列重新计算，并按原版口径处理操作任务。
                __instance.SetQueueDirty();
                FabricatorAccess.UpdateChore(__instance);
                return false;
            }
            catch (Exception e)
            {
                ModLog.Error("自动化红信号订单需求补丁异常：" + e);
                return true;
            }
        }
    }
}
