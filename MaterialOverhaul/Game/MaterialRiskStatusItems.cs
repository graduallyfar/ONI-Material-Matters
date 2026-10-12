namespace MaterialOverhaul.Game
{
    /// <summary>实时风险与已经锁存的损坏原因分开显示，不追加伤害。</summary>
    public static class MaterialRiskStatusItems
    {
        public static StatusItem Wear { get; private set; }
        public static StatusItem Heat { get; private set; }
        public static StatusItemCategory WearCategory { get; private set; }
        public static StatusItemCategory HeatCategory { get; private set; }

        public static void Register()
        {
            if (Wear != null) return;
            Db db = Db.Get();
            WearCategory = new StatusItemCategory("MaterialOverhaulWearRiskCategory", db.StatusItemCategories, "Mechanical wear risk");
            HeatCategory = new StatusItemCategory("MaterialOverhaulHeatRiskCategory", db.StatusItemCategories, "Overheat risk");
            db.StatusItemCategories.Add(WearCategory);
            db.StatusItemCategories.Add(HeatCategory);
            Wear = Create("MaterialOverhaulWearRisk", "WEARRISK");
            Heat = Create("MaterialOverhaulHeatRisk", "HEATRISK");
            Wear.resolveTooltipCallback = (text, data) => text + "\n" + (data as MaterialOverhaulFab)?.HardnessText();
            Heat.resolveTooltipCallback = (text, data) => text + "\n" + (data as MaterialOverhaulFab)?.HardnessText();
            db.BuildingStatusItems.Add(Wear);
            db.BuildingStatusItems.Add(Heat);
        }

        private static StatusItem Create(string id, string suffix)
        {
            return new StatusItem(id, "STRINGS.BUILDING.STATUSITEMS.MATERIALOVERHAUL." + suffix,
                "status_item_exclamation", StatusItem.IconType.Custom, NotificationType.BadMinor,
                false, OverlayModes.None.ID);
        }
    }

    public partial class MaterialOverhaulFab
    {
        private bool risksApplied;
        private bool lastWearRisk;
        private bool lastHeatRisk;

        private void UpdateMaterialRiskStatusItems()
        {
            if (selectable == null || MaterialRiskStatusItems.Wear == null) return;
            bool wearRisk = HasHardnessDeficit() && !HasMechanicalWear;
            bool heatRisk = IsAboveOverheatThreshold && !HasOverheatDamage;
            if (!risksApplied || wearRisk != lastWearRisk)
                selectable.SetStatusItem(MaterialRiskStatusItems.WearCategory, wearRisk ? MaterialRiskStatusItems.Wear : null, this);
            if (!risksApplied || heatRisk != lastHeatRisk)
                selectable.SetStatusItem(MaterialRiskStatusItems.HeatCategory, heatRisk ? MaterialRiskStatusItems.Heat : null, this);
            lastWearRisk = wearRisk;
            lastHeatRisk = heatRisk;
            risksApplied = true;
        }
    }
}
