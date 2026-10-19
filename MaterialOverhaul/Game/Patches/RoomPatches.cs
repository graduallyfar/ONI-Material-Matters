using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Rooms;

namespace MaterialOverhaul.Patches
{
    /// <summary>在 Db.Initialize 之后登记房间类型；此时原版 RoomTypes 已建好、探测器尚未运行。</summary>
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class Patch_Room_Db_Initialize
    {
        private static void Postfix()
        {
            try
            {
                RoomRegistration.Register();
            }
            catch (Exception e)
            {
                ModLog.Error("房间类型登记失败：" + e);
            }
        }
    }

    /// <summary>
    /// 开始一轮房间判定时清空快照缓存；判定结束后按已确认的覆盖关系做后置仲裁。
    /// 参数用 <c>__args</c> 取，避免依赖原方法参数名。
    /// </summary>
    [HarmonyPatch(typeof(Database.RoomTypes), "GetRoomType")]
    internal static class Patch_RoomTypes_GetRoomType
    {
        private static void Prefix()
        {
            RoomSnapshotCache.BeginEvaluation();
        }

        private static void Postfix(Database.RoomTypes __instance, object[] __args, ref RoomType __result)
        {
            try
            {
                Room room = __args != null && __args.Length > 0 ? __args[0] as Room : null;
                RoomType replacement = RoomRegistration.Choose(__result, room);
                if (replacement != null)
                {
                    __result = replacement;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("房间类型仲裁失败：" + e);
            }
        }
    }

    /// <summary>
    /// 对私人卧室、宴会厅追加最低装饰度与温度条件。
    /// 主条件仍由原版判定；附加条件不足时返回「主条件满足」，按原版规则降级。
    /// </summary>
    [HarmonyPatch(typeof(RoomType), "isSatisfactory")]
    internal static class Patch_RoomType_IsSatisfactory
    {
        private static void Postfix(RoomType __instance, object[] __args, ref RoomType.RoomIdentificationResult __result)
        {
            try
            {
                Room room = __args != null && __args.Length > 0 ? __args[0] as Room : null;
                __result = RoomRegistration.ApplyModifications(__instance, room, __result);
            }
            catch (Exception e)
            {
                ModLog.Error("房间附加条件判定失败：" + e);
            }
        }
    }

    /// <summary>每次房间拓扑刷新前放宽探测器上限，让大型房间能建立 Room 对象。</summary>
    [HarmonyPatch(typeof(RoomProber), "Refresh")]
    internal static class Patch_RoomProber_Refresh
    {
        private static void Prefix()
        {
            RoomRegistration.EnsureRoomSize();
        }
    }

    /// <summary>按固定间隔重判平均温度／装饰度，只在类型变化时重建房间。</summary>
    [HarmonyPatch(typeof(RoomProber), "Sim1000ms")]
    internal static class Patch_RoomProber_Sim1000ms
    {
        private static void Postfix(object[] __args)
        {
            try
            {
                float dt = __args != null && __args.Length > 0 ? (float)__args[0] : 1f;
                RoomEnvironmentMonitor.Tick(dt);
            }
            catch (Exception e)
            {
                ModLog.Error("房间环境重判失败：" + e);
            }
        }
    }

    /// <summary>界面读取房间条件／详情前刷新快照，避免显示上一轮判定的环境值。</summary>
    [HarmonyPatch(typeof(RoomConstraints), "RoomCriteriaString")]
    internal static class Patch_RoomConstraints_RoomCriteriaString
    {
        private static void Prefix()
        {
            RoomSnapshotCache.BeginEvaluation();
        }
    }

    [HarmonyPatch(typeof(RoomDetails), "RoomDetailString")]
    internal static class Patch_RoomDetails_RoomDetailString
    {
        private static void Prefix()
        {
            RoomSnapshotCache.BeginEvaluation();
        }
    }
}
