using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Rooms;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    // 科研、航天、货运与医疗中心入院的房间效果补丁（V3.0.0 §4.9）。
    // 参数见 RoomAdvancedEffects；所有加成都是原版效果之外的新增量，DLC 未启用时目标对象不存在、补丁不触发。

    /// <summary>离开存档时清空房间效果的运行期派生状态。</summary>
    [HarmonyPatch(typeof(global::Game), "OnCleanUp")]
    internal static class Patch_RoomAdvanced_Game_OnCleanUp
    {
        private static void Postfix()
        {
            RoomSpaceEffects.Clear();
            RoomGeoTunerEffects.Clear();
        }
    }

    // ---------------- 医疗中心 ----------------

    /// <summary>
    /// 优先入院（D11）：自动分配诊疗床时先选医疗中心内的空床；没有可用床位时交回原版。
    /// 患病（<c>SicknessMonitor.AutoAssignClinic</c>）与失能（<c>BeIncapacitatedChore</c>）都经过此方法。
    /// </summary>
    [HarmonyPatch(typeof(Assignables), "AutoAssignSlot")]
    internal static class Patch_Room_Assignables_AutoAssignSlot
    {
        private static bool Prefix(Assignables __instance, AssignableSlot slot, ref Assignable __result)
        {
            try
            {
                Assignable cot = RoomMedicalEffects.TryAssignMedicalCenterCot(__instance, slot);
                if (cot != null)
                {
                    __result = cot;
                    return false;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("医疗中心优先入院失败，回退原版分配：" + e);
            }

            return true;
        }
    }

    // ---------------- 任务控制室 ----------------

    /// <summary>本体版指挥站完成助推：位于任务控制室时给目标火箭登记新增推进比例。</summary>
    [HarmonyPatch(typeof(MissionControlWorkable), "OnCompleteWork")]
    internal static class Patch_Room_MissionControl_OnCompleteWork
    {
        private static void Prefix(MissionControlWorkable __instance, out Spacecraft __state)
        {
            __state = __instance == null ? null : __instance.TargetSpacecraft;
        }

        private static void Postfix(MissionControlWorkable __instance, object[] __args, Spacecraft __state)
        {
            try
            {
                WorkerBase worker = __args != null && __args.Length > 0 ? __args[0] as WorkerBase : null;
                float bonus = RoomAdvancedEffects.MissionControlSpeed(
                    RoomQuery.RoomTypeIdOf(__instance),
                    RoomQuery.ScienceOf(worker));
                RoomSpaceEffects.RegisterSpacecraftBoost(__state, bonus);
            }
            catch (Exception e)
            {
                ModLog.Error("任务控制室助推登记失败：" + e);
            }
        }
    }

    /// <summary>本体版任务推进：助推期间按登记的新增比例额外推进（原版另有 +20%）。</summary>
    [HarmonyPatch(typeof(Spacecraft), "ProgressMission")]
    internal static class Patch_Room_Spacecraft_ProgressMission
    {
        private static readonly AccessTools.FieldRef<Spacecraft, float> MissionElapsed =
            AccessTools.FieldRefAccess<Spacecraft, float>("missionElapsed");

        private static void Prefix(Spacecraft __instance, object[] __args)
        {
            try
            {
                float dt = __args != null && __args.Length > 0 ? (float)__args[0] : 0f;
                float extra = RoomSpaceEffects.ExtraMissionProgress(__instance, dt);
                if (extra > 0f)
                {
                    MissionElapsed(__instance) += extra;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("任务控制室本体版推进失败：" + e);
            }
        }
    }

    /// <summary>群星版指挥站完成助推：登记新增速度，并记录操作者科学值供扩展范围使用。</summary>
    [HarmonyPatch(typeof(MissionControlClusterWorkable), "OnCompleteWork")]
    internal static class Patch_Room_MissionControlCluster_OnCompleteWork
    {
        private static void Prefix(MissionControlClusterWorkable __instance, out Clustercraft __state)
        {
            __state = __instance == null ? null : __instance.TargetClustercraft;
        }

        private static void Postfix(MissionControlClusterWorkable __instance, object[] __args, Clustercraft __state)
        {
            try
            {
                WorkerBase worker = __args != null && __args.Length > 0 ? __args[0] as WorkerBase : null;
                float science = RoomQuery.ScienceOf(worker);
                RoomSpaceEffects.RememberStationScience(__instance, science);
                float bonus = RoomAdvancedEffects.MissionControlSpeed(RoomQuery.RoomTypeIdOf(__instance), science);
                RoomSpaceEffects.RegisterClustercraftBoost(__state, bonus);
            }
            catch (Exception e)
            {
                ModLog.Error("任务控制室群星助推登记失败：" + e);
            }
        }
    }

    /// <summary>
    /// 群星火箭速度：指挥站新增量与航天发射场加成按基础速度（引擎功率 ÷ 负载）的比例相加。
    /// 只在原版结果大于 0 时追加，不让无人驾驶、停飞的火箭因房间加成移动。
    /// </summary>
    [HarmonyPatch(typeof(Clustercraft), "Speed", MethodType.Getter)]
    internal static class Patch_Room_Clustercraft_Speed
    {
        private static void Postfix(Clustercraft __instance, ref float __result)
        {
            try
            {
                if (__result <= 0f)
                {
                    return;
                }

                float bonus = RoomSpaceEffects.ClustercraftSpeedBonus(__instance);
                if (bonus <= 0f)
                {
                    return;
                }

                float burden = __instance.TotalBurden;
                if (burden > 0f)
                {
                    __result += __instance.EnginePower / burden * bonus;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("房间火箭速度加成失败：" + e);
            }
        }
    }

    /// <summary>群星指挥站候选火箭：位于任务控制室时按扩展范围补充原版 2 格以外的候选。</summary>
    [HarmonyPatch(typeof(MissionControlCluster.Instance), "UpdateWorkableRocketsInRange")]
    internal static class Patch_Room_MissionControlCluster_UpdateRange
    {
        private static readonly AccessTools.FieldRef<MissionControlCluster.Instance, List<Clustercraft>> Boostable =
            AccessTools.FieldRefAccess<MissionControlCluster.Instance, List<Clustercraft>>("boostableClustercraft");

        private static readonly Func<MissionControlCluster.Instance, Clustercraft, bool> CanBeBoosted =
            AccessTools.MethodDelegate<Func<MissionControlCluster.Instance, Clustercraft, bool>>(
                AccessTools.Method(typeof(MissionControlCluster.Instance), "CanBeBoosted"));

        private static readonly Func<MissionControlCluster.Instance, Clustercraft, bool> IsOwnWorld =
            AccessTools.MethodDelegate<Func<MissionControlCluster.Instance, Clustercraft, bool>>(
                AccessTools.Method(typeof(MissionControlCluster.Instance), "IsOwnWorld"));

        private static void Postfix(MissionControlCluster.Instance __instance)
        {
            try
            {
                if (__instance == null || __instance.gameObject == null || ClusterGrid.Instance == null)
                {
                    return;
                }

                MissionControlClusterWorkable station = __instance.gameObject.GetComponent<MissionControlClusterWorkable>();
                int range = RoomSpaceEffects.StationRange(station);
                if (range <= RoomAdvancedEffects.VanillaMissionControlRange)
                {
                    return;
                }

                List<Clustercraft> boostable = Boostable(__instance);
                AxialI location = __instance.gameObject.GetMyWorldLocation();
                for (int i = 0; i < Components.Clustercrafts.Count; i++)
                {
                    Clustercraft craft = Components.Clustercrafts[i];
                    if (craft == null || boostable.Contains(craft)
                        || !ClusterGrid.Instance.IsInRange(craft.Location, location, range)
                        || IsOwnWorld(__instance, craft) || !CanBeBoosted(__instance, craft)
                        || IsTargetedByOtherStation(station, craft))
                    {
                        continue;
                    }

                    boostable.Add(craft);
                }

                __instance.sm.WorkableRocketsAreInRange.Set(boostable.Count > 0, __instance);
            }
            catch (Exception e)
            {
                ModLog.Error("任务控制室扩展范围失败：" + e);
            }
        }

        private static bool IsTargetedByOtherStation(MissionControlClusterWorkable station, Clustercraft craft)
        {
            foreach (MissionControlClusterWorkable other in Components.MissionControlClusterWorkables)
            {
                if (other != null && other != station && other.TargetClustercraft == craft)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>群星指挥站工作期间按扩展范围判定目标是否仍在范围内，避免超出原版 2 格即中断。</summary>
    [HarmonyPatch(typeof(MissionControlClusterWorkable), "OnWorkTick")]
    internal static class Patch_Room_MissionControlCluster_OnWorkTick
    {
        private static void Prefix(MissionControlClusterWorkable __instance)
        {
            try
            {
                RoomSpaceEffects.ExtendedRangeContext = RoomSpaceEffects.StationRange(__instance);
            }
            catch (Exception e)
            {
                RoomSpaceEffects.ExtendedRangeContext = 0;
                ModLog.Error("任务控制室工作范围判定失败：" + e);
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            RoomSpaceEffects.ExtendedRangeContext = 0;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(MissionControlClusterWorkable), "IsRocketInRange")]
    internal static class Patch_Room_MissionControlCluster_IsRocketInRange
    {
        private static void Postfix(object[] __args, ref bool __result)
        {
            int range = RoomSpaceEffects.ExtendedRangeContext;
            if (__result || range <= RoomAdvancedEffects.VanillaMissionControlRange || __args == null || __args.Length < 2)
            {
                return;
            }

            __result = AxialUtil.GetDistance((AxialI)__args[0], (AxialI)__args[1]) <= range;
        }
    }

    // ---------------- 地质研究室 ----------------

    /// <summary>调谐研究完成前记录研究者科学值；原版完成后会立即进入广播并施加调谐，因此用前缀。</summary>
    [HarmonyPatch(typeof(Workable), "CompleteWork")]
    internal static class Patch_Room_GeoTunerResearch_CompleteWork
    {
        private static void Prefix(Workable __instance, object[] __args)
        {
            try
            {
                if (!(__instance is GeoTunerWorkable) || __args == null || __args.Length == 0)
                {
                    return;
                }

                RoomGeoTunerEffects.RememberResearcher(__instance, RoomQuery.ScienceOf(__args[0] as WorkerBase));
            }
            catch (Exception e)
            {
                ModLog.Error("地质研究室记录研究者失败：" + e);
            }
        }
    }

    [HarmonyPatch(typeof(GeoTuner), "ApplyTuning")]
    internal static class Patch_Room_GeoTuner_ApplyTuning
    {
        private static void Postfix(object[] __args)
        {
            try
            {
                RoomGeoTunerEffects.Apply(__args != null && __args.Length > 0 ? __args[0] as GeoTuner.Instance : null);
            }
            catch (Exception e)
            {
                ModLog.Error("地质研究室调谐产量追加失败：" + e);
            }
        }
    }

    [HarmonyPatch(typeof(GeoTuner), "RemoveTuning")]
    internal static class Patch_Room_GeoTuner_RemoveTuning
    {
        private static void Postfix(object[] __args)
        {
            try
            {
                RoomGeoTunerEffects.Remove(__args != null && __args.Length > 0 ? __args[0] as GeoTuner.Instance : null);
            }
            catch (Exception e)
            {
                ModLog.Error("地质研究室调谐产量撤销失败：" + e);
            }
        }
    }

    /// <summary>
    /// 调谐仪侧栏：位于地质研究室的调谐仪按扩展上限判定能否再指向一座喷泉。
    /// 原版上限是点击回调与提示里的字面量 5；这里只替换有喷泉的行，空行（「无」）保持原版。
    /// </summary>
    [HarmonyPatch(typeof(GeoTunerSideScreen), "SetRow")]
    internal static class Patch_Room_GeoTunerSideScreen_SetRow
    {
        private static readonly AccessTools.FieldRef<GeoTunerSideScreen, GeoTuner.Instance> Target =
            AccessTools.FieldRefAccess<GeoTunerSideScreen, GeoTuner.Instance>("targetGeotuner");

        private static readonly MethodInfo RefreshOptions = AccessTools.Method(typeof(GeoTunerSideScreen), "RefreshOptions");

        private static void Postfix(GeoTunerSideScreen __instance, int idx, Geyser geyser)
        {
            try
            {
                GeoTuner.Instance target = Target(__instance);
                if (target == null || geyser == null || __instance.rowContainer == null
                    || idx < 0 || idx >= __instance.rowContainer.childCount)
                {
                    return;
                }

                if (RoomGeoTunerEffects.LimitFor(target) <= RoomAdvancedEffects.VanillaGeotunerLimit)
                {
                    return;
                }

                GameObject row = __instance.rowContainer.GetChild(idx).gameObject;
                MultiToggle toggle = row.GetComponent<MultiToggle>();
                if (toggle != null)
                {
                    toggle.onClick = () => OnClick(__instance, target, geyser);
                }

                ToolTip[] tips = row.GetComponentsInChildren<ToolTip>();
                if (tips != null && tips.Length > 0 && tips[0] != null)
                {
                    Func<string> original = tips[0].OnToolTip;
                    tips[0].OnToolTip = () => Tooltip(original, target, geyser);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("地质研究室调谐上限显示失败：" + e);
            }
        }

        private static void OnClick(GeoTunerSideScreen screen, GeoTuner.Instance target, Geyser geyser)
        {
            Studyable studyable = geyser.GetComponent<Studyable>();
            if ((studyable != null && !studyable.Studied) || geyser == target.GetFutureGeyser())
            {
                return;
            }

            if (RoomAdvancedEffects.CanAddGeotuner(CountPointing(target, geyser), RoomGeoTunerEffects.LimitFor(target)))
            {
                target.AssignFutureGeyser(geyser);
                if (RefreshOptions != null)
                {
                    // 原版签名为 RefreshOptions(object data = null)。
                    RefreshOptions.Invoke(screen, new object[] { null });
                }
            }
        }

        private static string Tooltip(Func<string> original, GeoTuner.Instance target, Geyser geyser)
        {
            string text = original == null ? string.Empty : original();
            if (geyser == target.GetFutureGeyser())
            {
                return text;
            }

            int count = CountPointing(target, geyser);
            int limit = RoomGeoTunerEffects.LimitFor(target);
            if (count >= RoomAdvancedEffects.VanillaGeotunerLimit && RoomAdvancedEffects.CanAddGeotuner(count, limit))
            {
                return string.Format(Strings.Get("STRINGS.ROOMS.MATERIALOVERHAUL.DETAILS.GEOTUNER_EXTRA_SLOT"), count, limit);
            }

            return text;
        }

        private static int CountPointing(GeoTuner.Instance target, Geyser geyser)
        {
            int count = 0;
            foreach (GeoTuner.Instance tuner in Components.GeoTuners.GetItems(target.GetMyWorldId()))
            {
                if (tuner != null && (tuner.GetFutureGeyser() == geyser || tuner.GetAssignedGeyser() == geyser))
                {
                    count++;
                }
            }

            return count;
        }
    }

    // ---------------- 航天发射场 ----------------

    /// <summary>群星火箭实体挂上可保存的航天发射场飞行标记；DLC 未启用时本配置不加载。</summary>
    [HarmonyPatch(typeof(ClustercraftConfig), "CreatePrefab")]
    internal static class Patch_Room_ClustercraftConfig_CreatePrefab
    {
        private static void Postfix(GameObject __result)
        {
            if (__result != null)
            {
                __result.AddOrGet<RoomSpaceportFlight>();
            }
        }
    }

    /// <summary>
    /// 起飞时判定平台所在房间：平台位于航天发射场时，本次飞行获得速度与燃料加成。
    ///
    /// 原版 <c>Clustercraft.Launch</c> 只在确认起飞后调用 <c>DoLaunch</c>，随后才扣除发射那一格燃料；
    /// 此时状态为 <c>Launching</c>，<c>CurrentPad</c> 仍指向起飞平台。在这里写入标记，发射那一格也按折扣扣除。
    /// </summary>
    [HarmonyPatch(typeof(CraftModuleInterface), "DoLaunch")]
    internal static class Patch_Room_CraftModuleInterface_DoLaunch
    {
        private static void Prefix(CraftModuleInterface __instance)
        {
            try
            {
                if (__instance != null)
                {
                    RoomSpaceEffects.BeginFlight(__instance.GetComponent<Clustercraft>(), __instance.CurrentPad);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("航天发射场发射判定失败：" + e);
            }
        }
    }

    /// <summary>火箭落地时结束本次飞行的航天发射场加成。</summary>
    [HarmonyPatch(typeof(Clustercraft), "SetCraftStatus")]
    internal static class Patch_Room_Clustercraft_SetCraftStatus
    {
        private static void Postfix(Clustercraft __instance, Clustercraft.CraftStatus craft_status)
        {
            if (craft_status == Clustercraft.CraftStatus.Grounded)
            {
                RoomSpaceEffects.EndFlight(__instance);
            }
        }
    }

    /// <summary>航程与「能否移动」判定使用的每格燃料，与实际扣除保持一致。</summary>
    [HarmonyPatch(typeof(Clustercraft), "FuelPerDistance", MethodType.Getter)]
    internal static class Patch_Room_Clustercraft_FuelPerDistance
    {
        private static void Postfix(Clustercraft __instance, ref float __result)
        {
            __result *= RoomSpaceEffects.FuelFactor(__instance);
        }
    }

    /// <summary>
    /// 实际扣燃料：尝试航程 ×系数、返回航程 ÷系数，等价于每格燃料与氧化剂 ×系数，
    /// 并保留原版按库存与氧化剂截断的逻辑。
    /// </summary>
    [HarmonyPatch(typeof(Clustercraft), "BurnFromTank")]
    internal static class Patch_Room_Clustercraft_BurnFromTank
    {
        private static void Prefix(Clustercraft __instance, ref float attemptTravelAmount, out float __state)
        {
            __state = RoomSpaceEffects.FuelFactor(__instance);
            if (__state < 1f)
            {
                attemptTravelAmount = RoomAdvancedEffects.FuelAttempt(attemptTravelAmount, __state);
            }
        }

        private static void Postfix(ref float __result, float __state)
        {
            if (__state < 1f)
            {
                __result = RoomAdvancedEffects.FuelTravelled(__result, __state);
            }
        }
    }

    // ---------------- 星际货运站 ----------------

    /// <summary>
    /// 发射：标记发射上下文供载荷登记；位于星际货运站时按连发规则回拨本轮计数（每轮多发 2 次）。
    /// 原版在本方法返回后才把计数 +1 并判断是否进入冷却。
    /// </summary>
    [HarmonyPatch(typeof(RailGun), "LaunchProjectile")]
    internal static class Patch_Room_RailGun_LaunchProjectile
    {
        private static void Prefix(RailGun __instance)
        {
            try
            {
                RoomSpaceEffects.LaunchingFromCargoStation =
                    RoomAdvancedEffects.IsCargoStation(RoomQuery.RoomTypeIdOf(__instance));
            }
            catch (Exception e)
            {
                RoomSpaceEffects.LaunchingFromCargoStation = false;
                ModLog.Error("星际货运站发射判定失败：" + e);
            }
        }

        private static void Postfix(RailGun __instance)
        {
            try
            {
                if (!RoomSpaceEffects.LaunchingFromCargoStation || __instance == null || __instance.smi == null)
                {
                    return;
                }

                RailGun.StatesInstance smi = __instance.smi;
                int count = smi.sm.payloadsFiredSinceCooldown.Get(smi);
                int adjusted = RoomSpaceEffects.CargoCountAfterLaunch(__instance, count);
                if (adjusted >= 0)
                {
                    smi.sm.payloadsFiredSinceCooldown.Set(adjusted, smi);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("星际货运站连发计数失败：" + e);
            }
            finally
            {
                RoomSpaceEffects.LaunchingFromCargoStation = false;
            }
        }
    }

    /// <summary>载荷出发：来自星际货运站的载荷登记为加速载荷。</summary>
    [HarmonyPatch(typeof(RailGunPayload.StatesInstance), "Launch")]
    internal static class Patch_Room_RailGunPayload_Launch
    {
        private static void Postfix(RailGunPayload.StatesInstance __instance)
        {
            try
            {
                if (!RoomSpaceEffects.LaunchingFromCargoStation || __instance == null || __instance.gameObject == null)
                {
                    return;
                }

                RoomSpaceEffects.MarkCargoPayload(__instance.gameObject.GetComponent<BallisticClusterGridEntity>());
            }
            catch (Exception e)
            {
                ModLog.Error("星际货运站载荷登记失败：" + e);
            }
        }
    }

    [HarmonyPatch(typeof(BallisticClusterGridEntity), "GetSpeed")]
    internal static class Patch_Room_BallisticClusterGridEntity_GetSpeed
    {
        private static void Postfix(BallisticClusterGridEntity __instance, ref float __result)
        {
            if (RoomSpaceEffects.IsCargoPayload(__instance))
            {
                __result *= RoomAdvancedEffects.CargoPayloadSpeedFactor;
            }
        }
    }

    /// <summary>冷却：位于星际货运站时每次模拟额外递减冷却计时，使冷却按 1.5 倍速进行。</summary>
    [HarmonyPatch(typeof(RailGun), "Sim200ms")]
    internal static class Patch_Room_RailGun_Sim200ms
    {
        private static void Postfix(RailGun __instance, float dt)
        {
            try
            {
                if (__instance == null || __instance.smi == null)
                {
                    return;
                }

                // 只在冷却状态内加速，与原版只在 on.cooldown.loop 递减计时一致（断电、关闭时不推进）。
                RailGun.StatesInstance smi = __instance.smi;
                if (smi.sm.cooldownTimer.Get(smi) <= 0f
                    || !smi.IsInsideState(smi.sm.on.cooldown)
                    || !RoomAdvancedEffects.IsCargoStation(RoomQuery.RoomTypeIdOf(__instance)))
                {
                    return;
                }

                smi.sm.cooldownTimer.Delta(-RoomAdvancedEffects.CargoExtraCooldown(dt), smi);
            }
            catch (Exception e)
            {
                ModLog.Error("星际货运站冷却加速失败：" + e);
            }
        }
    }
}
