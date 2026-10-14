using MaterialOverhaul.Core;

namespace MaterialOverhaul.Game
{
    public partial class MaterialOverhaulFab
    {
        public BuildingProfile NumericProfile
        {
            get
            {
                BuildingProfile profile;
                return BuildingProfiles.TryGet(PrefabId, out profile) ? profile : null;
            }
        }

        public float MechanicalDamagePerWearUnit
        {
            get { return NumericProfile != null ? NumericProfile.DamagePerWearUnit : 0f; }
        }
    }
}
