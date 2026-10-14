using System;
using System.Collections.Generic;
using System.Reflection;
using MaterialOverhaul.Game;
using MaterialOverhaul.Hardness;
using MaterialOverhaul.Heating;
using MaterialOverhaul.Orders;

// 目标 DLL 数量 API 的计数/刷新顺序，以及伤害前后血量读取替身。
// 实际补丁、数量同步、导入与加热安全 partial 均参与编译，不证明游戏内 Harmony 装配。
public sealed partial class ComplexFabricator
{
    public const int QUEUE_INFINITE = -1;
    public const int MAX_QUEUE_SIZE = 99;
    public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
    public ComplexRecipe[] RecipeList = Array.Empty<ComplexRecipe>();
    public Action RefreshCallback;
    public ComplexRecipe[] GetRecipes() { return RecipeList; }
    public int GetRecipeQueueCount(ComplexRecipe recipe) { return Counts[recipe.id]; }
    public void SetQueueDirty() { }

    public void EditQueue(string method, ComplexRecipe recipe, int count = 0, bool respectInfinite = false)
    {
        Type patch = typeof(MaterialOverhaul.Patches.Patch_ComplexFabricator_SetRecipeQueueCount).Assembly
            .GetType("MaterialOverhaul.Patches.Patch_ComplexFabricator_" + method);
        MethodInfo prefix = patch.GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo postfix = patch.GetMethod("Postfix", BindingFlags.NonPublic | BindingFlags.Static);
        bool set = method == "SetRecipeQueueCount";
        object[] args = set ? new object[] { this, recipe } : new object[] { this, recipe, 0 };
        prefix.Invoke(null, args);
        int before = Counts[recipe.id];
        if (set) Counts[recipe.id] = count;
        else if (method == "IncrementRecipeQueueCount")
            Counts[recipe.id] = before == QUEUE_INFINITE ? 0 : before >= MAX_QUEUE_SIZE ? QUEUE_INFINITE : before + 1;
        else if (!respectInfinite || before != QUEUE_INFINITE)
            Counts[recipe.id] = before == QUEUE_INFINITE ? MAX_QUEUE_SIZE : before == 0 ? QUEUE_INFINITE : before - 1;
        // 原版在后置之前刷新；有旧订单时 AdvanceNextOrder 会回到 Mod 的迁移入口。
        RefreshCallback?.Invoke();
        postfix.Invoke(null, new object[] { this, recipe, set ? count : args[2] });
    }
}

public sealed class BuildingHP
{
    public MaterialOverhaulFab Mod;
    public int HitPoints = 100;
    public bool IsBroken { get { return HitPoints == 0; } }
    public bool NeedsRepairs { get { return HitPoints < 100; } }
}

public sealed class Overheatable
{
    public MaterialOverhaulFab Mod;
    public float OverheatTemperature;
    public T GetComponent<T>() where T : class { return Mod as T; }
}

namespace MaterialOverhaul.Game
{
    internal static partial class FabricatorAccess
    {
        public static bool IsInfinite(int count) { return count == ComplexFabricator.QUEUE_INFINITE; }
        public static Dictionary<string, int> QueueCounts(ComplexFabricator machine) { return machine.Counts; }
    }

    public partial class MaterialOverhaulFab
    {
        private readonly OrderQueue orders = new OrderQueue();
        private readonly BuildingHP buildingHP = new BuildingHP();
        private DamageCauseState damageState = DamageCauseState.Fresh;
        private bool forcedRun;
        private bool mechanicalDamageMark;
        public bool IsPowered = true;
        public bool IsManuallyDisabled;
        public bool IsAboveOverheatThreshold;
        public bool HasLockedBatch { get { return fabricator.CurrentWorkingOrder != null; } }
        public bool IsBrokenDown { get { return buildingHP.IsBroken; } }
        public bool HasOrderData { get { return orders.Count > 0; } }
        public bool RepairResetsTemperature { get { return DamageCauseEvaluator.ResetsTemperatureOnRepair(DamageCauseEvaluator.Evaluate(damageState)); } }
        public OrderQueue Orders { get { return orders; } }
        public BuildingHP HP { get { buildingHP.Mod = this; return buildingHP; } }
        public bool Forced { get { return forcedRun; } set { forcedRun = value; } }
        public DamageCauseState SavedDamage;
        public PowerDemand SafetyPower;
        public HeatingIntent SafetyIntent = HeatingIntent.Sustained;
        public bool ConsumeMechanicalDamageMark() { bool result = mechanicalDamageMark; mechanicalDamageMark = false; return result; }
        public void MarkMechanicalDamage() { mechanicalDamageMark = true; }
        public void RestoreDamage(DamageCauseState state) { damageState = state; }
        private void SyncDamageFields() { SavedDamage = damageState; }
        private void SyncOrderFields() { }
        private void ForceVanillaCount(string recipeId, int count) { fabricator.Counts[recipeId] = count; }
        private void AfterOrderChange()
        {
            foreach (ComplexRecipe recipe in fabricator.RecipeList)
            {
                RecipeTotal total = orders.TotalFor(recipe.id);
                ForceVanillaCount(recipe.id, total.Infinite ? ComplexFabricator.QUEUE_INFINITE : total.Batches);
            }
        }
        public void RefreshNextOrder() { EnsureOrdersImported(); }
        // 下面三个与本次 UI 测试无关的补丁入口仅满足链接，不用于验证批次结算。
        public void LockOrderForStart(string id, int index) { }
        public void OnBatchCompleting(string id) { }
        public void OnBatchCompleted(string id) { }
        private void UpdatePowerAndHeating(float dt)
        {
            HeatingContext heat = CreateHeatingContext();
            SafetyPower = PowerDemand.Evaluate(new PowerDemandContext
            {
                HasLockedBatch = HasLockedBatch, PowerAvailable = IsPowered,
                ManuallyDisabled = IsManuallyDisabled, BrokenDown = IsBrokenDown,
                OverheatDamaged = HasOverheatDamage,
                ProductionWatts = NumericProfile == null ? 0f : NumericProfile.ProductionWatts,
                DisallowBuiltInHeating = !SupportsBuiltInHeating,
                TemperatureAllowsProduction = true, AutomationAllowsProduction = true, HasWorker = true,
                CommandedHeating = HeatingConditions.CanRequestHeat(heat) ? SafetyIntent : HeatingIntent.None
            });
        }
        public void RefreshSafety() { ClearDamageWhenFullyRepaired(); UpdatePowerAndHeating(0f); }
        public void CheckDamageWithoutRepair() { UpdatePowerAndHeating(0f); }
    }
}
