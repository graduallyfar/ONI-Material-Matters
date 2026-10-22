using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    /// <summary>在 Db 初始化之后登记感生放射状态项（Db.Initialize 会重建状态项集合）。</summary>
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class Patch_Radiation_Db_Initialize
    {
        private static void Postfix()
        {
            try
            {
                RadiationStatusItems.Register();
            }
            catch (Exception e)
            {
                ModLog.Error("感生放射状态项登记失败：" + e);
            }
        }
    }

    /// <summary>
    /// 给所有完成建筑（含建造砖块）挂上活化度组件。原版自带辐射源在本方法之后的
    /// <c>DoPostConfigureComplete</c> 才添加，所以是否参与活化在组件生成时判断。
    /// </summary>
    [HarmonyPatch(typeof(BuildingLoader), "CreateBuildingComplete")]
    internal static class Patch_Radiation_BuildingComplete
    {
        private static void Postfix(GameObject __result)
        {
            if (__result != null)
            {
                __result.AddOrGet<RadiationActivation>();
            }
        }
    }

    /// <summary>借用原版房间探测的 1 秒模拟更新驱动扫描；暂停时原版不调用，扫描随之暂停。</summary>
    [HarmonyPatch(typeof(RoomProber), "Sim1000ms")]
    internal static class Patch_Radiation_RoomProber_Sim1000ms
    {
        private static void Postfix(object[] __args)
        {
            try
            {
                float dt = __args != null && __args.Length > 0 ? (float)__args[0] : 1f;
                RadiationInducedMonitor.Tick(dt);
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|tick", "感生放射扫描失败：" + e);
            }
        }
    }

    /// <summary>退出或读档前清空扫描的静态状态。</summary>
    [HarmonyPatch(typeof(global::Game), "OnCleanUp")]
    internal static class Patch_Radiation_Game_OnCleanUp
    {
        private static void Postfix()
        {
            RadiationInducedMonitor.Clear();
        }
    }
}
