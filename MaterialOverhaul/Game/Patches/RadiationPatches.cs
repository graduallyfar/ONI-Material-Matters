using System;
using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Radiation;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    /// <summary>在 Db 初始化之后登记感生放射状态项（Db.Initialize 会重建状态项集合）。</summary>
    [Core.SystemPart(Core.ModSystem.Radiation)]
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
    [Core.SystemPart(Core.ModSystem.Radiation)]
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
    [Core.SystemPart(Core.ModSystem.Radiation)]
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
    [Core.SystemPart(Core.ModSystem.Radiation)]
    [HarmonyPatch(typeof(global::Game), "OnCleanUp")]
    internal static class Patch_Radiation_Game_OnCleanUp
    {
        private static void Postfix()
        {
            RadiationInducedMonitor.Clear();
        }
    }

    /// <summary>
    /// 拆除活化建筑：主建造材料中按活化度的一部分不再原样掉落，改为带放射性污染物的核废料。
    /// 原版拆除（含砖块）都经由这个私有方法逐种生成建材，这里只记下结算上下文，
    /// 实际扣减在 <see cref="Patch_Radiation_Deconstructable_SpawnItem"/> 里完成。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Radiation)]
    [HarmonyPatch(typeof(Deconstructable), "SpawnItemsFromConstruction",
        new[] { typeof(float), typeof(byte), typeof(int), typeof(WorkerBase) })]
    internal static class Patch_Radiation_Deconstructable_SpawnItems
    {
        internal static Deconstructable Owner;
        internal static Tag PrimaryTag;
        internal static float Activation;
        internal static float WasteMass;
        internal static bool Applied;

        private static void Prefix(Deconstructable __instance)
        {
            Owner = null;
            Applied = false;
            WasteMass = 0f;
            try
            {
                RadiationActivation activation = __instance.GetComponent<RadiationActivation>();
                if (activation == null || DecommissionModel.WasteFraction(activation.Activation) <= 0f
                    || __instance.constructionElements == null || __instance.constructionElements.Length == 0)
                {
                    return;
                }

                Owner = __instance;
                PrimaryTag = __instance.constructionElements[0];
                Activation = activation.Activation;
            }
            catch (Exception e)
            {
                Owner = null;
                ModLog.WarnOnce("radiation|decommission-prefix", "拆除活化建筑结算失败：" + e);
            }
        }

        private static void Postfix(Deconstructable __instance, float temperature, WorkerBase construction_worker, List<GameObject> __result)
        {
            if (!ReferenceEquals(Owner, __instance) || !Applied || !(WasteMass > 0f))
            {
                return;
            }

            try
            {
                Klei.AI.Disease contaminant = Db.Get().Diseases.RadiationPoisoning;
                byte idx = Db.Get().Diseases.GetIndex(contaminant.id);
                int count = DecommissionModel.WasteContaminants(Activation);
                Owner = null;
                GameObject waste = __instance.SpawnItem(__instance.transform.GetPosition(), SimHashes.NuclearWaste.CreateTag(),
                    WasteMass, temperature, idx, count, construction_worker);
                if (waste != null)
                {
                    if (GameComps.Fallers.Has(waste))
                    {
                        GameComps.Fallers.Remove(waste);
                    }
                    GameComps.Fallers.Add(waste, Vector2.zero);
                    __result?.Add(waste);
                }
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|decommission-spawn", "生成拆除核废料失败：" + e);
            }
        }

        private static Exception Finalizer(Exception __exception)
        {
            Owner = null;
            Applied = false;
            WasteMass = 0f;
            return __exception;
        }
    }

    /// <summary>拆除活化建筑时，从主建造材料里扣出转成核废料的质量（只扣一次）。</summary>
    [Core.SystemPart(Core.ModSystem.Radiation)]
    [HarmonyPatch(typeof(Deconstructable), nameof(Deconstructable.SpawnItem))]
    internal static class Patch_Radiation_Deconstructable_SpawnItem
    {
        private static void Prefix(Deconstructable __instance, Tag src_element, ref float src_mass)
        {
            if (Patch_Radiation_Deconstructable_SpawnItems.Applied
                || !ReferenceEquals(Patch_Radiation_Deconstructable_SpawnItems.Owner, __instance)
                || src_element != Patch_Radiation_Deconstructable_SpawnItems.PrimaryTag)
            {
                return;
            }

            float waste = DecommissionModel.WasteMass(src_mass, Patch_Radiation_Deconstructable_SpawnItems.Activation);
            Patch_Radiation_Deconstructable_SpawnItems.Applied = true;
            if (waste > 0f)
            {
                Patch_Radiation_Deconstructable_SpawnItems.WasteMass = waste;
                src_mass -= waste;
            }
        }
    }
}
