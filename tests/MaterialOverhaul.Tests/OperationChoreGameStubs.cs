using System;
using MaterialOverhaul.Game;
using MaterialOverhaul.Zones;

// 只供实际 Chores partial / UpdateChore 前缀的源码级测试使用。
// 不加载游戏或 Harmony；不能证明游戏内补丁装配、复制人寻路或动画表现。
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
    }
}

public sealed class ComplexRecipe
{
    public int consumedHEP;
    public string id;
    public System.Collections.Generic.List<Tag> fabricators;
    public RecipeElement[] ingredients;
    public RecipeElement[] results;
    public sealed class RecipeElement
    {
        public Tag material;
        public Tag[] possibleMaterials;
        public float[] possibleMaterialAmounts;
        public float amount;
    }
}

public sealed class Tag
{
    private readonly string value;
    public Tag(string text) { value = text; }
    public override string ToString() { return value; }
}

public sealed class ComplexRecipeManager
{
    public static readonly ComplexRecipeManager Instance = new ComplexRecipeManager();
    public System.Collections.Generic.List<ComplexRecipe> recipes = new System.Collections.Generic.List<ComplexRecipe>();
    public static ComplexRecipeManager Get() { return Instance; }
}

public sealed class Chore { }

public sealed partial class ComplexFabricator
{
    public bool duplicantOperated = true;
    public bool Operational = true;
    public ComplexRecipe CurrentWorkingOrder;
    public MaterialOverhaulFab Mod;
    public Chore CurrentChore;
    public bool WorkerAssigned;
    public int Created;
    public int Cancelled;
    public float OrderProgress = 0.42f;
    public float BatchMaterialMass = 100f;

    public void UpdateChore()
    {
        if (!MaterialOverhaul.Patches.Patch_ComplexFabricator_UpdateChore.Prefix(this)) return;
        // 与目标 DLL UpdateChore 的门槛一致：原版没有温度判断。
        if (!duplicantOperated) return;
        bool ready = Operational && CurrentWorkingOrder != null;
        if (ready && CurrentChore == null)
        {
            CurrentChore = new Chore();
            Created++;
        }
        else if (!ready && CurrentChore != null)
        {
            MaterialOverhaul.Game.FabricatorAccess.CancelChore(this);
        }
    }
}

namespace MaterialOverhaul.Patches
{
    internal static class PatchHelpers
    {
        public static MaterialOverhaulFab FabOf(ComplexFabricator fabricator) { return fabricator.Mod; }
        public static MaterialOverhaulFab FabOf(BuildingHP hp) { return hp.Mod; }
        public static MaterialOverhaulFab FabOf(Overheatable heat) { return heat.Mod; }
    }
}

namespace MaterialOverhaul.Game
{
    internal static partial class FabricatorAccess
    {
        public static Chore ChoreField(ComplexFabricator fabricator) { return fabricator.CurrentChore; }
        public static void CancelChore(ComplexFabricator fabricator)
        {
            if (fabricator.CurrentChore == null) return;
            fabricator.Cancelled++;
            fabricator.CurrentChore = null;
            fabricator.WorkerAssigned = false;
        }
    }

    public partial class MaterialOverhaulFab
    {
        private readonly ComplexFabricator fabricator;
        private bool productionPausedByTemperature;
        public bool Spawned = true;
        public bool Active;
        public string PrefabId { get; set; } = "MetalRefinery";
        public bool SupportsBuiltInHeating { get { return HeatingScope.IsEnabled(PrefabId); } }
        public ComplexRecipe CurrentWorkingRecipe { get { return fabricator.CurrentWorkingOrder; } }
        public Func<ComplexRecipe, ProductionDecision> Evaluation;
        public bool Paused { get { return productionPausedByTemperature; } }

        public MaterialOverhaulFab(ComplexFabricator target, Func<ComplexRecipe, ProductionDecision> evaluate)
        {
            fabricator = target;
            fabricator.Mod = this;
            Evaluation = evaluate;
        }

        public ProductionDecision EvaluateFor(ComplexRecipe recipe) { return Evaluation(recipe); }
        private void SetOperationalActive(bool value) { Active = value; }
    }
}
