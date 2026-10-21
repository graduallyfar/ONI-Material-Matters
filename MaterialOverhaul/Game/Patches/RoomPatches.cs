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
                RoomTraitRegistration.Register();
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

    /// <summary>
    /// 每次房间拓扑刷新前放宽探测器上限，让大型房间能建立 Room 对象；
    /// 刷新结束后丢弃本轮记下的旧房间类型（见 <see cref="RoomRetention"/>）。
    /// </summary>
    [HarmonyPatch(typeof(RoomProber), "Refresh")]
    internal static class Patch_RoomProber_Refresh
    {
        private static void Prefix()
        {
            RoomRegistration.EnsureRoomSize();
        }

        private static void Postfix()
        {
            RoomRetention.EndRebuild();
        }
    }

    /// <summary>
    /// 原版拓扑变化时先清除旧 Room、再新建 Room，新对象没有类型，保持回差会失效。
    /// 清除前按格子记下旧类型，新房间判定时据此决定是否按保持线判定。
    /// </summary>
    [HarmonyPatch(typeof(RoomProber), "ClearRoom")]
    internal static class Patch_Room_RoomProber_ClearRoom
    {
        private static void Prefix(object[] __args)
        {
            try
            {
                RoomRetention.Remember(__args != null && __args.Length > 0 ? __args[0] as Room : null);
            }
            catch (Exception e)
            {
                ModLog.Error("记录房间重建前类型失败：" + e);
            }
        }
    }

    /// <summary>环境重判触发的单个房间重建结束后，同样丢弃记下的旧类型。</summary>
    [HarmonyPatch(typeof(RoomProber), "UpdateRoom")]
    internal static class Patch_Room_RoomProber_UpdateRoom
    {
        private static void Postfix()
        {
            RoomRetention.EndRebuild();
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

                // 与原版 TriggerRoomEffects 同一主设施谓词：扫地、修理等其他工作不发放房间效果。
                RoomConstraints.Constraint primary = room.roomType.primary_constraint;
                if (primary == null || primary.building_criteria == null || !primary.building_criteria(prefab))
                {
                    return;
                }

                // 拥有对应娱乐偏好的复制人获得特质专用效果，不再叠加普通士气。
                if (RoomTraitEffects.TryApply(room.roomType, worker))
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
    /// 工业区内工业建筑的批次生产速度 +20%；任务控制中心、地热调谐实验室内对应设施的工作速度 +20%。
    ///
    /// 有人操作的批次建筑由 <c>ComplexFabricatorWorkable</c> 的工作速度决定；航天指挥站与
    /// 地质调谐仪按其自身工作组件结算。只对这几类设施乘算，避免把修理、清洁等其它工作也算成
    /// 生产加成；具体倍率仍由房间与 prefab 共同决定。配方投入产出比例不变。
    ///
    /// 整合版中工业补丁在同一方法后置记录最终倍率，供 <c>OnWorkTick</c> 把缩放后的 dt 还原成
    /// 真实生产秒数计磨损；本补丁须先于它执行，否则房间加速会被误算成更长的生产时间。
    /// </summary>
    [HarmonyPatch(typeof(Workable), "GetEfficiencyMultiplier")]
    [HarmonyPriority(Priority.HigherThanNormal)]
    internal static class Patch_Room_Workable_GetEfficiencyMultiplier
    {
        // 按类型名匹配，避免对未加载 DLC 的工作组件产生编译期依赖。
        private static readonly System.Collections.Generic.HashSet<string> RoomBonusWorkables =
            new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
            {
                "MissionControlWorkable",
                "MissionControlClusterWorkable",
                "GeoTunerWorkable"
            };

        private static void Postfix(Workable __instance, ref float __result)
        {
            try
            {
                if (__result <= 0f
                    || __instance == null
                    || !(__instance is ComplexFabricatorWorkable || RoomBonusWorkables.Contains(__instance.GetType().Name)))
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
    /// 连续转换设备见 <see cref="Patch_Room_ElementConverter_GetSpeedMultiplier"/>。
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

    /// <summary>
    /// 连续转换设备的生产速度 +20%（工业区）。
    ///
    /// 原版 <c>ElementConverter.GetSpeedMultiplier</c> 同时决定消耗与产出速率；工业温控补丁
    /// 也在此乘温度效率，两个后置补丁按乘法叠加（顺序无关）。不改变配方投入产出比例。
    /// </summary>
    [HarmonyPatch(typeof(ElementConverter), "GetSpeedMultiplier")]
    internal static class Patch_Room_ElementConverter_GetSpeedMultiplier
    {
        private static void Postfix(ElementConverter __instance, ref float __result)
        {
            try
            {
                if (__result <= 0f)
                {
                    return;
                }

                __result *= RoomProductionBonus.MultiplierFor(__instance);
            }
            catch (Exception e)
            {
                ModLog.Error("房间生产加成（连续转换）失败：" + e);
            }
        }
    }

    /// <summary>
    /// 本 Mod 房间顶替原版房间后，原版要求该房间的设施仍视为位于正确房间。
    ///
    /// 航天指挥站与地质调谐仪的 <c>RoomTracker</c> 要求「实验室」（<c>Requirement.Required</c>），
    /// 并以 <c>IsInCorrectRoom</c> 决定能否运作；任务控制中心、地热调谐实验室、数据机房会顶替
    /// 实验室，若不放行这些设施会停工。只放行 <see cref="RoomDefinition.Beats"/> 明列的原版房间。
    /// </summary>
    [HarmonyPatch(typeof(RoomTracker), "IsInCorrectRoom")]
    internal static class Patch_Room_RoomTracker_IsInCorrectRoom
    {
        private static void Postfix(RoomTracker __instance, ref bool __result)
        {
            try
            {
                if (!__result
                    && __instance != null
                    && RoomRegistration.CountsAsRoom(__instance.room, __instance.requiredRoomType))
                {
                    __result = true;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("房间顶替判定（IsInCorrectRoom）失败：" + e);
            }
        }
    }

    /// <summary>
    /// 与 <see cref="Patch_Room_RoomTracker_IsInCorrectRoom"/> 配套：原版 <c>OnUpdateRoom</c> 按房间 id
    /// 直接比较并挂上「不在所需房间」状态；顶替房间内移除这条状态，避免显示与实际运作不一致。
    /// </summary>
    [HarmonyPatch(typeof(RoomTracker), "OnUpdateRoom")]
    internal static class Patch_Room_RoomTracker_OnUpdateRoom
    {
        private static readonly AccessTools.FieldRef<RoomTracker, Guid> StatusItemGuid =
            AccessTools.FieldRefAccess<RoomTracker, Guid>("statusItemGuid");

        private static void Postfix(RoomTracker __instance)
        {
            try
            {
                if (__instance == null
                    || !RoomRegistration.CountsAsRoom(__instance.room, __instance.requiredRoomType))
                {
                    return;
                }

                KSelectable selectable = __instance.GetComponent<KSelectable>();
                if (selectable == null)
                {
                    return;
                }

                ref Guid guid = ref StatusItemGuid(__instance);
                guid = selectable.RemoveStatusItem(guid);
            }
            catch (Exception e)
            {
                ModLog.Error("房间顶替状态（OnUpdateRoom）失败：" + e);
            }
        }
    }

    /// <summary>
    /// 原版科研建筑的实验室工作效率 +10% 由 <c>Workable.OnUpdateRoom</c> 直接比较
    /// <c>roomType == Laboratory</c> 决定，不经过 <c>RoomTracker</c>。顶替实验室的房间
    /// 补上同一个标记与状态项，使其仍享有原版加成（本 Mod 房间加成另行乘算）。
    /// </summary>
    [HarmonyPatch(typeof(Workable), "OnUpdateRoom")]
    internal static class Patch_Room_Workable_OnUpdateRoom
    {
        private static readonly AccessTools.FieldRef<Workable, bool> UseLaboratoryBonus =
            AccessTools.FieldRefAccess<Workable, bool>("useLaboratoryEfficiencyBonus");

        private static readonly AccessTools.FieldRef<Workable, bool> InLaboratory =
            AccessTools.FieldRefAccess<Workable, bool>("currentlyInLaboratory");

        private static readonly AccessTools.FieldRef<Workable, Guid> LaboratoryStatusHandle =
            AccessTools.FieldRefAccess<Workable, Guid>("laboratoryEfficiencyBonusStatusItemHandle");

        private static void Postfix(Workable __instance, object[] __args)
        {
            try
            {
                if (__instance == null || __instance.worker == null
                    || !UseLaboratoryBonus(__instance) || InLaboratory(__instance))
                {
                    return;
                }

                Room room = __args != null && __args.Length > 0 ? __args[0] as Room : null;
                if (!RoomRegistration.CountsAsRoom(room, Db.Get().RoomTypes.Laboratory.Id))
                {
                    return;
                }

                InLaboratory(__instance) = true;
                ref Guid handle = ref LaboratoryStatusHandle(__instance);
                if (handle == Guid.Empty)
                {
                    handle = __instance.worker.OfferStatusItem(
                        Db.Get().DuplicantStatusItems.LaboratoryWorkEfficiencyBonus, __instance);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("顶替房间的实验室工作效率加成失败：" + e);
            }
        }
    }

    /// <summary>
    /// 原版只在娱乐室内生成复制人休息时的闲聊格（<c>MingleCellTracker</c> 直接比较
    /// <c>roomType == RecRoom</c>）。顶替娱乐室的房间按原版同一格子条件补上闲聊格。
    /// </summary>
    [HarmonyPatch(typeof(MingleCellTracker), "Sim1000ms")]
    internal static class Patch_Room_MingleCellTracker_Sim1000ms
    {
        private static void Postfix(MingleCellTracker __instance)
        {
            try
            {
                global::Game game = global::Game.Instance;
                RoomProber prober = game == null ? null : game.roomProber;
                MinionGroupProber reachability = MinionGroupProber.Get();
                if (__instance == null || prober == null || reachability == null)
                {
                    return;
                }

                RoomType recRoom = Db.Get().RoomTypes.RecRoom;
                foreach (Room room in prober.rooms)
                {
                    if (room == null || room.cavity == null || room.roomType == recRoom
                        || !RoomRegistration.CountsAsRoom(room, recRoom.Id))
                    {
                        continue;
                    }

                    for (int y = room.cavity.minY; y <= room.cavity.maxY; y++)
                    {
                        for (int x = room.cavity.minX; x <= room.cavity.maxX; x++)
                        {
                            int cell = Grid.XYToCell(x, y);
                            if (prober.GetCavityForCell(cell) == room.cavity
                                && reachability.IsReachable(cell)
                                && !Grid.HasLadder[cell]
                                && !Grid.HasTube[cell]
                                && !Grid.IsLiquid(cell)
                                && Grid.Element[cell].id == SimHashes.Oxygen)
                            {
                                __instance.mingleCells.Add(cell);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ModLog.Error("顶替娱乐室的闲聊格生成失败：" + e);
            }
        }
    }

    /// <summary>
    /// 患者进入诊疗床治疗状态时追加生命回复效果。
    /// 原版 <c>Clinic</c> 在治疗状态进入时对患者调用 <c>StartEffect</c>；这里按诊疗床所在
    /// 房间类型追加医院／医疗中心效果，并在医疗中心结算辐射拯救者。患者离床时由
    /// <see cref="Patch_Room_Clinic_OnStopWork"/> 移除。
    /// </summary>
    [HarmonyPatch(typeof(Clinic.ClinicSM.Instance), "StartEffect")]
    internal static class Patch_Room_Clinic_StartEffect
    {
        private static void Postfix(Clinic.ClinicSM.Instance __instance, string effect)
        {
            try
            {
                Clinic clinic = __instance == null ? null : __instance.master;
                if (clinic == null || clinic.worker == null)
                {
                    return;
                }

                string roomTypeId = RoomTypeIdOf(clinic.gameObject);

                // 医疗中心辐射拯救者（D12）：开始治疗时一次性结算，与生命回复效果相互独立。
                RoomMedicalEffects.TryRescueRadiation(clinic.worker.gameObject, roomTypeId);

                string id = RoomRecoveryEffect.ForRoomType(roomTypeId);
                if (id == null)
                {
                    return;
                }

                Effects effects = clinic.worker.GetComponent<Effects>();
                if (effects != null)
                {
                    effects.Add(id, false);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("房间治疗恢复效果失败：" + e);
            }
        }

        private static string RoomTypeIdOf(UnityEngine.GameObject go)
        {
            global::Game game = global::Game.Instance;
            RoomProber prober = game == null ? null : game.roomProber;
            if (prober == null)
            {
                return null;
            }

            Room room = prober.GetRoomOfGameObject(go);
            return room == null || room.roomType == null ? null : room.roomType.Id;
        }
    }

    /// <summary>
    /// 患者离开诊疗床时移除医院／医疗中心与护理员的恢复效果。
    ///
    /// 不能挂在 <c>StopEffect</c>：原版在「未诊疗 → 已诊疗」的内部切换时也会调用它，
    /// 患者仍在床上，挂在那里会把刚加上的护理员加成和房间恢复效果一起删掉。
    /// <c>Clinic.OnStopWork</c> 只在患者真正停止躺床（离床、被打断或治疗结束）时调用。
    /// </summary>
    [HarmonyPatch(typeof(Clinic), "OnStopWork")]
    internal static class Patch_Room_Clinic_OnStopWork
    {
        private static void Postfix(object[] __args)
        {
            try
            {
                WorkerBase patient = __args != null && __args.Length > 0 ? __args[0] as WorkerBase : null;
                if (patient == null)
                {
                    return;
                }

                RoomRecoveryEffect.RemoveAll(patient.GetComponent<Effects>());
            }
            catch (Exception e)
            {
                ModLog.Error("房间治疗恢复效果清理失败：" + e);
            }
        }
    }

    /// <summary>
    /// 护理员特质加成：医生完成一次诊疗时，若其拥有原版 「Caregiver」（<c>BedsideManner</c>）特质，
    /// 给正在该诊疗床接受治疗的患者追加生命回复效果。患者离床时由
    /// <see cref="Patch_Room_Clinic_OnStopWork"/> 一并移除。
    /// </summary>
    [HarmonyPatch(typeof(Workable), "CompleteWork")]
    internal static class Patch_Room_Caregiver_CompleteWork
    {
        private const string CaregiverTraitId = "BedsideManner";

        private static void Postfix(Workable __instance, object[] __args)
        {
            try
            {
                if (!(__instance is DoctorChoreWorkable) || __args == null || __args.Length == 0)
                {
                    return;
                }

                WorkerBase doctor = __args[0] as WorkerBase;
                Clinic clinic = __instance.GetComponent<Clinic>();
                if (doctor == null || clinic == null || clinic.worker == null)
                {
                    return;
                }

                Traits traits = doctor.GetComponent<Traits>();
                if (traits == null || !traits.HasTrait(CaregiverTraitId))
                {
                    return;
                }

                if (!RoomRecoveryEffect.IsRecoveryRoom(RoomTypeIdOf(clinic.gameObject)))
                {
                    return;
                }

                Effects effects = clinic.worker.GetComponent<Effects>();
                if (effects != null)
                {
                    effects.Add(RoomEffectIds.CaregiverBoost, false);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("护理员治疗加成失败：" + e);
            }
        }

        private static string RoomTypeIdOf(UnityEngine.GameObject go)
        {
            global::Game game = global::Game.Instance;
            RoomProber prober = game == null ? null : game.roomProber;
            if (prober == null)
            {
                return null;
            }

            Room room = prober.GetRoomOfGameObject(go);
            return room == null || room.roomType == null ? null : room.roomType.Id;
        }
    }

    /// <summary>
    /// 复制人进入房间时发放娱乐偏好特质加成。
    ///
    /// 原版 <c>RoomMonitor</c> 在路径推进时调用 <c>CavityInfo.OnEnter</c>；这里补一次
    /// 特质效果，使展览馆等没有可完成工作任务的房间也能触发。只处理特质持有者，
    /// 普通复制人的房间效果仍由设施使用完成时发放。
    /// </summary>
    [HarmonyPatch(typeof(CavityInfo), "OnEnter")]
    internal static class Patch_Room_CavityInfo_OnEnter
    {
        private static void Postfix(CavityInfo __instance, object data)
        {
            try
            {
                if (__instance == null || __instance.room == null)
                {
                    return;
                }

                UnityEngine.GameObject go = data as UnityEngine.GameObject;
                if (go == null)
                {
                    return;
                }

                WorkerBase worker = go.GetComponent<WorkerBase>();
                if (worker == null)
                {
                    return;
                }

                RoomTraitEffects.TryApply(__instance.room.roomType, worker);
            }
            catch (Exception e)
            {
                ModLog.Error("房间进入时发放特质效果失败：" + e);
            }
        }
    }
}
