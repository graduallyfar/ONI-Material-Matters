using System;
using System.Collections.Generic;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Radiation;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    /// <summary>在 Db 初始化之后登记辐射累积与显现的状态项（Db.Initialize 会重建状态项集合）。</summary>
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
                ModLog.Error("辐射状态项登记失败：" + e);
            }
        }
    }

    /// <summary>
    /// 给所有完成建筑（含建造砖块）挂上累积量组件。原版自带辐射源在本方法之后的
    /// <c>DoPostConfigureComplete</c> 才添加，所以是否参与累积在组件生成时判断。
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

    /// <summary>
    /// 把每格累积表的存档组件挂到原版 SaveGame 对象上：累积表随存档保存，
    /// 组件每 0.2 秒的模拟更新驱动扫描；暂停时原版不调用，扫描随之暂停。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Radiation)]
    [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
    internal static class Patch_Radiation_SaveGame_OnPrefabInit
    {
        private static void Postfix(SaveGame __instance)
        {
            try
            {
                __instance.gameObject.AddOrGet<RadiationAccumulationStore>();
            }
            catch (Exception e)
            {
                ModLog.Error("辐射累积表存档组件挂载失败：" + e);
            }
        }
    }

    /// <summary>退出或读档前清空扫描与累积表的静态状态。</summary>
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
    /// 拆除显现建筑：主建造材料中按自身放射的一部分不再原样掉落，改为带放射性污染物的核废料。
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
        internal static float EmitRads;
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
                if (activation == null || DecommissionModel.WasteFraction(activation.CurrentEmitRads) <= 0f
                    || __instance.constructionElements == null || __instance.constructionElements.Length == 0)
                {
                    return;
                }

                Owner = __instance;
                PrimaryTag = __instance.constructionElements[0];
                EmitRads = activation.CurrentEmitRads;
            }
            catch (Exception e)
            {
                Owner = null;
                ModLog.WarnOnce("radiation|decommission-prefix", "拆除显现建筑结算失败：" + e);
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
                int count = DecommissionModel.WasteContaminants(EmitRads);
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

    /// <summary>拆除显现建筑时，从主建造材料里扣出转成核废料的质量（只扣一次）。</summary>
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

            float waste = DecommissionModel.WasteMass(src_mass, Patch_Radiation_Deconstructable_SpawnItems.EmitRads);
            Patch_Radiation_Deconstructable_SpawnItems.Applied = true;
            if (waste > 0f)
            {
                Patch_Radiation_Deconstructable_SpawnItems.WasteMass = waste;
                src_mass -= waste;
            }
        }
    }

    /// <summary>
    /// 辐射粒子撞到固体时，把剩余能量按 1 点 = 10 rads 存进被撞的砖块、建筑或天然格。
    /// 撞到复制人、动物，被接收口吸收，以及飞行途中都保持原版。
    /// 在原版处理之前读取能量与位置：原版随后会触发爆炸状态。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Radiation)]
    [HarmonyPatch(typeof(HighEnergyParticle), nameof(HighEnergyParticle.Collide))]
    internal static class Patch_Radiation_HighEnergyParticle_Collide
    {
        private static void Prefix(HighEnergyParticle __instance, HighEnergyParticle.CollisionType collisionType)
        {
            if (collisionType != HighEnergyParticle.CollisionType.Solid || __instance == null)
            {
                return;
            }

            try
            {
                RadiationInducedMonitor.DepositParticle(Grid.PosToCell(__instance.transform.GetPosition()), __instance.payload);
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|particle", "辐射粒子撞击结算失败：" + e);
            }
        }
    }

    /// <summary>
    /// 辐射粒子发生器消耗辐射：原版每 0.2 秒采样一次所在格的辐射并存入粒子（10 rads = 1 点）。
    /// 在采样前记下粒子仓剩余容量，采样后按实际存入量记账，等每秒结算时由周围显现物付账；
    /// 原版来源不付账，发生器的产量不变。
    /// </summary>
    [Core.SystemPart(Core.ModSystem.Radiation)]
    [HarmonyPatch(typeof(HighEnergyParticleSpawner), nameof(HighEnergyParticleSpawner.LauncherUpdate))]
    internal static class Patch_Radiation_ParticleSpawner_LauncherUpdate
    {
        private static void Prefix(HighEnergyParticleSpawner __instance, float dt, float ___radiationSampleTimer,
            HighEnergyParticleStorage ___particleStorage, out float __state)
        {
            __state = -1f;
            if (__instance != null && ___particleStorage != null && ___radiationSampleTimer + dt >= __instance.radiationSampleRate)
            {
                __state = ___particleStorage.RemainingCapacity();
            }
        }

        private static void Postfix(HighEnergyParticleSpawner __instance, float ___recentPerSecondConsumptionRate, float __state)
        {
            if (!(__state > 0f) || !(___recentPerSecondConsumptionRate > 0f))
            {
                return;
            }

            try
            {
                float absorbed = GeneratorDrainModel.AbsorbedRads(___recentPerSecondConsumptionRate, __instance.radiationSampleRate, __state);
                RadiationInducedMonitor.QueueGeneratorDrain(Grid.PosToCell(__instance), absorbed);
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|spawner", "辐射粒子发生器吸收记账失败：" + e);
            }
        }
    }

    /// <summary>吸入：原版每次呼吸报告吸入的病菌种类与个数，吸入放射性污染物时增加辐射剂量。</summary>
    [Core.SystemPart(Core.ModSystem.Radiation)]
    [HarmonyPatch(typeof(GermExposureMonitor.Instance), nameof(GermExposureMonitor.Instance.OnAirConsumed))]
    internal static class Patch_Radiation_GermExposure_OnAirConsumed
    {
        private static void Postfix(GermExposureMonitor.Instance __instance, byte disseaseIDX, int disseaseCount)
        {
            try
            {
                RadiationInducedMonitor.OnInhaled(__instance == null ? null : __instance.gameObject, disseaseIDX, disseaseCount);
            }
            catch (Exception e)
            {
                ModLog.WarnOnce("radiation|inhale", "辐射吸入剂量结算失败：" + e);
            }
        }
    }
}
