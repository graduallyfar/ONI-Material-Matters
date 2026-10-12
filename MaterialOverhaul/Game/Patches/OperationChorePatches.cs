using System;
using HarmonyLib;
using MaterialOverhaul.Core;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 原版 UpdateChore 只检查 Operational 与当前批次，不知道本 Mod 的温控许可。
    /// 在创建／重建入口拦截，比先派出复制人再等 Sim200ms 取消更早。
    /// 白名单组件之外保持原样，不修改取料、投料或订单取消流程。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "UpdateChore")]
    internal static class Patch_ComplexFabricator_UpdateChore
    {
        internal static bool Prefix(ComplexFabricator __instance)
        {
            try
            {
                return !(PatchHelpers.FabOf(__instance)?.TryBlockOperationChore() ?? false);
            }
            catch (Exception e)
            {
                ModLog.Error("操作任务温度门控补丁异常：" + e);
                return true;
            }
        }
    }
}
