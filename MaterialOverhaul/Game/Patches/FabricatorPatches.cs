using System;
using HarmonyLib;
using MaterialOverhaul.Core;
using MaterialOverhaul.Game;
using MaterialOverhaul.Zones;
using UnityEngine;

namespace MaterialOverhaul.Patches
{
    /// <summary>
    /// 补丁接入点均取自目标版本程序集核实结果（Assembly-CSharp.decompiled.cs 行号只作核对记录）：
    ///   ComplexFabricator.OnPrefabInit              protected override       180727
    ///   ComplexFabricator.ComputeWorkProgress       protected virtual        180832
    ///   ComplexFabricator.Sim200ms                  public，无子类覆写        180837
    ///   ComplexFabricator.RefreshAndStartNextOrder  private                  180855
    ///   ComplexFabricator.SetQueueDirty             public                   180894
    ///   ComplexFabricator.StartWorkingOrder         private，唯一开始点       180909
    ///   ComplexFabricator.CancelWorkingOrder        private                  180926
    ///   ComplexFabricator.CompleteWorkingOrder      public virtual           180935
    ///   ComplexFabricator.AdvanceNextOrder          private，唯一推进点       180989
    ///   ComplexFabricator.IncrementRecipeQueueCount public                   181402
    ///   ComplexFabricator.SetRecipeQueueCount       public                   181391
    ///   ComplexFabricator.DecrementRecipeQueueCount public                   181419
    ///   ComplexFabricatorWorkable.OnWorkTick        protected override       126157
    ///   Workable.GetEfficiencyMultiplier            public virtual           157412
    ///   EnergyConsumer.get_WattsUsed                public 属性              266544
    ///   EnergyConsumer.get_WattsNeededWhenActive    public 属性              266556
    ///   Overheatable.ResetTemperature               public                   142560
    ///
    /// 补丁只作用于白名单建筑：组件只在白名单建筑的 prefab 上挂载，其余建筑取不到组件即原样放行；
    /// 加热与电网需求再进一步限定在 <see cref="HeatingScope"/> 名单内。
    /// </summary>
    internal static class PatchHelpers
    {
        public static string PrefabIdOf(GameObject go)
        {
            if (go == null)
            {
                return string.Empty;
            }

            KPrefabID prefabId = go.GetComponent<KPrefabID>();
            return prefabId != null ? prefabId.PrefabTag.ToString() : go.name;
        }

        public static MaterialOverhaulFab FabOf(Component component)
        {
            return component != null ? component.GetComponent<MaterialOverhaulFab>() : null;
        }
    }

    /// <summary>在建筑 prefab 阶段挂载本 Mod 组件，使组件随存档序列化（而非运行时临时添加）。</summary>
    [HarmonyPatch(typeof(ComplexFabricator), "OnPrefabInit")]
    internal static class Patch_ComplexFabricator_OnPrefabInit
    {
        private static void Postfix(ComplexFabricator __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                string prefabId = PatchHelpers.PrefabIdOf(__instance.gameObject);
                if (!BuildingWhitelist.Contains(prefabId))
                {
                    return;
                }

                if (__instance.GetComponent<MaterialOverhaulFab>() == null)
                {
                    __instance.gameObject.AddComponent<MaterialOverhaulFab>();
                }

                // 「允许／暂停生产」自动化输入的执行者。复用原版组件而不是自己写一套：
                // 它的「未接线按 unNetworkedValue = 1 处理」正是设计要的默认放行语义，
                // 并且自带原版的「逻辑控制：已启用／已暂停」状态项。
                if (AutomationControlScope.IsEnabled(prefabId)
                    && __instance.GetComponent<LogicOperationalController>() == null)
                {
                    __instance.gameObject.AddComponent<LogicOperationalController>();
                }
            }
            catch (Exception e)
            {
                ModLog.Error("挂载 MaterialOverhaulFab 失败：" + e);
            }
        }
    }

    /// <summary>
    /// 非复制人路径：把温度效率乘进生产进度，越界时停止推进（返回值 0），
    /// 并按真实 dt 累计机械磨损（原版这条路径传入的就是真实时间）。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "ComputeWorkProgress")]
    internal static class Patch_ComplexFabricator_ComputeWorkProgress
    {
        private static void Postfix(ComplexFabricator __instance, ComplexRecipe recipe, float dt, ref float __result)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab == null)
                {
                    return;
                }

                ProductionDecision gate = fab.EvaluateFor(recipe);
                if (!gate.AllowsProduction)
                {
                    __result = 0f;
                    return;
                }

                __result *= gate.Efficiency;
                fab.OnNonDuplicantWorkProgress(dt);
            }
            catch (Exception e)
            {
                ModLog.Error("温度效率补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 复制人路径：操作效率乘数，与 ComputeWorkProgress 共用同一份温区评估。
    /// 同时记录最终倍率，供 <c>OnWorkTick</c> 把已缩放的时间还原成真实生产秒数。
    /// </summary>
    [HarmonyPatch(typeof(Workable), "GetEfficiencyMultiplier")]
    internal static class Patch_Workable_GetEfficiencyMultiplier
    {
        private static void Postfix(Workable __instance, ref float __result)
        {
            try
            {
                ComplexFabricatorWorkable workable = __instance as ComplexFabricatorWorkable;
                if (workable == null)
                {
                    return;
                }

                MaterialOverhaulFab fab = PatchHelpers.FabOf(workable);
                if (fab == null)
                {
                    return;
                }

                ComplexRecipe recipe = fab.CurrentWorkingRecipe;
                if (recipe == null)
                {
                    return;
                }

                ProductionDecision gate = fab.EvaluateFor(recipe);
                __result *= gate.AllowsProduction ? gate.Efficiency : 0f;
                fab.NoteEfficiencyMultiplier(__result);
            }
            catch (Exception e)
            {
                ModLog.Error("复制人操作效率补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 复制人路径的生产时间：只有实际推进工作才累计磨损，并把已缩放的 dt 还原成真实秒数。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricatorWorkable), "OnWorkTick")]
    internal static class Patch_ComplexFabricatorWorkable_OnWorkTick
    {
        private static void Postfix(ComplexFabricatorWorkable __instance, float dt)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.OnDuplicantWorkTick(dt);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("生产时间累计补丁异常：" + e);
            }
        }
    }

    /// <summary>
    /// 状态刷新。挂在 Sim200ms 而不是 Sim1000ms：Sim200ms 非 virtual 且无子类覆写，
    /// 不会因为子类 override 而漏掉（DataMiner 覆写了 Sim1000ms）。
    ///
    /// 后置补丁即使原版提前 return（总体不可运行）也会执行，因此断电、红信号与损坏停机时
    /// 仍能维护需求、原因与界面文字。只在状态变化时记录日志，不在每次模拟更新刷屏。
    /// </summary>
    [HarmonyPatch(typeof(ComplexFabricator), "Sim200ms")]
    internal static class Patch_ComplexFabricator_Sim200ms
    {
        private static void Prefix(ComplexFabricator __instance)
        {
            try
            {
                PatchHelpers.FabOf(__instance)?.PrepareRunStep();
            }
            catch (Exception e)
            {
                ModLog.Error("生产前模式与温控刷新异常：" + e);
            }
        }

        private static void Postfix(ComplexFabricator __instance, float dt)
        {
            try
            {
                MaterialOverhaulFab fab = PatchHelpers.FabOf(__instance);
                if (fab != null)
                {
                    fab.SimStep(dt);
                }
            }
            catch (Exception e)
            {
                ModLog.Error("状态刷新补丁异常：" + e);
            }
        }
    }
}
