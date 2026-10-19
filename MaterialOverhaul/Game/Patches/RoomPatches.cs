using System;
using HarmonyLib;
using Klei.AI;
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
                RoomEffects.Register();
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

    /// <summary>
    /// 使用本 Mod 房间的专属设施后触发房间效果。
    ///
    /// 原版只有床、厕所、餐桌和公园标识等少数建筑显式调用
    /// <c>RoomType.TriggerRoomEffects</c>；娱乐设施与淋浴间没有。这里在
    /// <c>Workable.CompleteWork</c> 后统一补一次，并且只在房间类型由本 Mod 登记时执行，
    /// 不改变原版房间的效果触发时机。是否触发仍由原版
    /// <c>TriggerRoomEffects</c> 的主设施谓词判定，因此其他建筑不会误触发。
    ///
    /// 进入型触发（例如公园标识）仍走原版路径，不在此重复。
    /// </summary>
    [HarmonyPatch(typeof(Workable), "CompleteWork")]
    internal static class Patch_Workable_CompleteWork
    {
        private static void Postfix(Workable __instance, object[] __args)
        {
            try
            {
                if (__instance == null || __args == null || __args.Length == 0)
                {
                    return;
                }

                WorkerBase worker = __args[0] as WorkerBase;
                if (worker == null)
                {
                    return;
                }

                global::Game game = global::Game.Instance;
                RoomProber prober = game == null ? null : game.roomProber;
                if (prober == null)
                {
                    return;
                }

                Room room = prober.GetRoomOfGameObject(__instance.gameObject);
                if (room == null || !RoomRegistration.IsModRoom(room.roomType))
                {
                    return;
                }

                Effects effects = worker.GetComponent<Effects>();
                KPrefabID prefab = __instance.GetComponent<KPrefabID>();
                if (effects == null || prefab == null)
                {
                    return;
                }

                room.roomType.TriggerRoomEffects(prefab, effects);
            }
            catch (Exception e)
            {
                ModLog.Error("房间设施使用后触发效果失败：" + e);
            }
        }
    }

    /// <summary>
    /// 工业区内工业建筑的批次生产速度 +20%。
    ///
    /// 有人操作的批次建筑由 <c>ComplexFabricatorWorkable</c> 的工作速度决定；只对这类
    /// 设施乘算，避免把修理、清洁等其它工作也算成生产加成。配方投入产出比例不变。
    /// </summary>
    [HarmonyPatch(typeof(Workable), "GetEfficiencyMultiplier")]
    internal static class Patch_Room_Workable_GetEfficiencyMultiplier
    {
        private static void Postfix(Workable __instance, ref float __result)
        {
            try
            {
                if (__result <= 0f || !(__instance is ComplexFabricatorWorkable))
                {
                    return;
                }

                __result *= RoomProductionBonus.MultiplierFor(__instance);
            }
            catch (Exception e)
            {
                ModLog.Error("房间生产加成（工作速度）失败：" + e);
            }
        }
    }

    /// <summary>
    /// 无人批次与数据挖掘中心的进度 +20%。
    ///
    /// 原版 <c>ComplexFabricator.Sim200ms</c> 只在 <c>!duplicantOperated</c> 时调用本方法；
    /// 数据挖掘中心的覆写会再乘温度效率，因此这里只乘房间倍率、不重复处理温度。
    /// 连续转换设备（<c>ElementConverter</c> 等）尚未接入，见开发计划 P4 与 D09。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "ComputeWorkProgress")]
    internal static class Patch_Room_ComplexFabricator_ComputeWorkProgress
    {
        private static void Postfix(ComplexFabricator __instance, ref float __result)
        {
            try
            {
                if (__instance == null || __result <= 0f || __instance.duplicantOperated)
                {
                    return;
                }

                __result *= RoomProductionBonus.MultiplierFor(__instance);
            }
            catch (Exception e)
            {
                ModLog.Error("房间生产加成（无人进度）失败：" + e);
            }
        }
    }
}
