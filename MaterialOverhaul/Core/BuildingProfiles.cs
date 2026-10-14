using System.Collections.Generic;

namespace MaterialOverhaul.Core
{
    /// <summary>首发三栋的集中数值。运行许可仍由温控、供电和原版条件判断。</summary>
    public sealed class BuildingProfile
    {
        public readonly float WorkMinCelsius;
        public readonly float ProductionWatts;
        public readonly float LowEfficiency;
        public readonly float HighEfficiency;
        public readonly float DamagePerWearUnit;
        public readonly int RequiredHardness;
        public readonly bool BuiltInHeating;

        internal BuildingProfile(float floor, float watts, float low, float high, float damage, bool heating)
        {
            WorkMinCelsius = floor;
            ProductionWatts = watts;
            LowEfficiency = low;
            HighEfficiency = high;
            DamagePerWearUnit = damage;
            RequiredHardness = 25;
            BuiltInHeating = heating;
        }
    }

    public static class BuildingProfiles
    {
        private static readonly Dictionary<string, BuildingProfile> Entries =
            new Dictionary<string, BuildingProfile>
            {
                { "MetalRefinery", new BuildingProfile(80f, 1200f, 0.30f, 0.80f, 0.05f, true) },
                { "RockCrusher", new BuildingProfile(-20f, 240f, 0.80f, 0.90f, 0.02f, false) },
                { "GlassForge", new BuildingProfile(80f, 1200f, 0.25f, 0.75f, 0.025f, true) }
            };

        public static IReadOnlyDictionary<string, BuildingProfile> All { get { return Entries; } }

        public static bool TryGet(string prefabId, out BuildingProfile profile)
        {
            profile = null;
            return !string.IsNullOrEmpty(prefabId) && Entries.TryGetValue(prefabId, out profile);
        }
    }
}
