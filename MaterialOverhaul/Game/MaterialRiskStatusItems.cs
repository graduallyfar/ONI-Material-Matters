namespace MaterialOverhaul.Game
{
    /// <summary>实时风险、温区受限提示与已经锁存的损坏原因分开显示，不追加伤害。</summary>
    public static class MaterialRiskStatusItems
    {
        public static StatusItem Wear { get; private set; }
        public static StatusItem Heat { get; private set; }
        public static StatusItem ZoneLimit { get; private set; }
        public static StatusItemCategory WearCategory { get; private set; }
        public static StatusItemCategory HeatCategory { get; private set; }
        public static StatusItemCategory ZoneCategory { get; private set; }

        public static void Register()
        {
            if (Wear != null) return;
            Db db = Db.Get();
            WearCategory = new StatusItemCategory("MaterialOverhaulWearRiskCategory", db.StatusItemCategories, "Mechanical wear risk");
            HeatCategory = new StatusItemCategory("MaterialOverhaulHeatRiskCategory", db.StatusItemCategories, "Overheat risk");
            ZoneCategory = new StatusItemCategory("MaterialOverhaulZoneLimitCategory", db.StatusItemCategories, "Temperature band limit");
            db.StatusItemCategories.Add(WearCategory);
            db.StatusItemCategories.Add(HeatCategory);
            db.StatusItemCategories.Add(ZoneCategory);
            Wear = Create("MaterialOverhaulWearRisk", "WEARRISK", NotificationType.BadMinor);
            Heat = Create("MaterialOverhaulHeatRisk", "HEATRISK", NotificationType.BadMinor);

            // 温区受限不是伤害，用中性通知级别，避免与损坏风险混淆（V0.1.2 §4.1 第 3 条）。
            ZoneLimit = Create("MaterialOverhaulZoneLimit", "ZONELIMIT", NotificationType.Neutral);
            Wear.resolveTooltipCallback = (text, data) => text + "\n" + (data as MaterialOverhaulFab)?.HardnessText();
            Heat.resolveTooltipCallback = (text, data) => text + "\n" + (data as MaterialOverhaulFab)?.HardnessText();
            ZoneLimit.resolveTooltipCallback = (text, data) =>
            {
                MaterialOverhaulFab fab = data as MaterialOverhaulFab;
                return fab == null ? text : text + "\n" + RunStatusText.ZoneDetailText(fab);
            };
            db.BuildingStatusItems.Add(Wear);
            db.BuildingStatusItems.Add(Heat);
            db.BuildingStatusItems.Add(ZoneLimit);
        }

        private static StatusItem Create(string id, string suffix, NotificationType notification)
        {
            return new StatusItem(id, "STRINGS.BUILDING.STATUSITEMS.MATERIALOVERHAUL." + suffix,
                "status_item_exclamation", StatusItem.IconType.Custom, notification,
                false, OverlayModes.None.ID);
        }
    }

    public partial class MaterialOverhaulFab
    {
        private bool risksApplied;
        private bool lastWearRisk;
        private bool lastHeatRisk;
        private bool lastZoneLimit;

        private void UpdateMaterialRiskStatusItems()
        {
            if (selectable == null || MaterialRiskStatusItems.Wear == null) return;
            bool wearRisk = HasHardnessDeficit() && !HasMechanicalWear;
            bool heatRisk = IsAboveOverheatThreshold && !HasOverheatDamage;
            bool zoneLimit = HasLockedBatch && RunStatusText.ZoneLimitText(this).Length > 0;

            if (!risksApplied || wearRisk != lastWearRisk)
                selectable.SetStatusItem(MaterialRiskStatusItems.WearCategory, wearRisk ? MaterialRiskStatusItems.Wear : null, this);
            if (!risksApplied || heatRisk != lastHeatRisk)
                selectable.SetStatusItem(MaterialRiskStatusItems.HeatCategory, heatRisk ? MaterialRiskStatusItems.Heat : null, this);
            if (!risksApplied || zoneLimit != lastZoneLimit)
                selectable.SetStatusItem(MaterialRiskStatusItems.ZoneCategory, zoneLimit ? MaterialRiskStatusItems.ZoneLimit : null, this);

            lastWearRisk = wearRisk;
            lastHeatRisk = heatRisk;
            lastZoneLimit = zoneLimit;
            risksApplied = true;
        }
    }
}
