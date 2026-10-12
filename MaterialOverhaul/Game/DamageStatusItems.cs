using MaterialOverhaul.Core;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 本 Mod 的建筑状态项：损坏原因。
    ///
    /// 原版 <c>BuildingHP</c> 只保存**一份** <c>damageSourceInfo</c>，每次受伤都会覆盖，
    /// 因此机械与过热两种原因无法同时显示。这里用四个独立状态项按当前原因显示，
    /// 双重原因时显示合并的那一项（AGENTS 已确认约束：两种状态同时显示时不能被后一次伤害覆盖）。
    ///
    /// 类别用原版 <c>Main</c>：它本来就是原版显示「建筑受损」的位置，替换掉那条单一提示
    /// 正是本机制的目的（不能只保留最后一次伤害提示）；运行状态不写进 Main，
    /// 避免把原版断电、缺料等关键状态遮掉。
    ///
    /// 状态项文本沿用原版惯例：<c>prefix + ".NAME"</c> 与 <c>prefix + ".TOOLTIP"</c>。
    /// </summary>
    public static class DamageStatusItems
    {
        public const string MechanicalId = "MaterialOverhaulMechanicalDamage";
        public const string OverheatId = "MaterialOverhaulOverheatDamage";
        public const string BothId = "MaterialOverhaulMechanicalAndOverheatDamage";
        public const string UnknownId = "MaterialOverhaulUnknownDamage";

        private const string PrefixRoot = "STRINGS.BUILDING.STATUSITEMS.MATERIALOVERHAUL.";
        private const string MechanicalPrefix = PrefixRoot + "MECHANICAL";
        private const string OverheatPrefix = PrefixRoot + "OVERHEAT";
        private const string BothPrefix = PrefixRoot + "BOTH";
        private const string UnknownPrefix = PrefixRoot + "UNKNOWN";

        /// <summary>图标沿用原版已有的感叹号状态图标，不新增美术资源。</summary>
        private const string Icon = "status_item_exclamation";

        private static bool registered;

        public static StatusItem Mechanical { get; private set; }

        public static StatusItem Overheat { get; private set; }

        public static StatusItem Both { get; private set; }

        public static StatusItem Unknown { get; private set; }

        /// <summary>损坏原因使用的状态类别（原版「建筑受损」的位置）。</summary>
        public static StatusItemCategory Category
        {
            get { return Db.Get() != null ? Db.Get().StatusItemCategories.Main : null; }
        }

        /// <summary>在 Db 初始化之后登记状态项（Db.Initialize 会重建 BuildingStatusItems 集合）。</summary>
        public static void Register()
        {
            if (registered)
            {
                return;
            }

            registered = true;

            Mechanical = Create(MechanicalId, MechanicalPrefix);
            Overheat = Create(OverheatId, OverheatPrefix);
            Both = Create(BothId, BothPrefix);
            Unknown = Create(UnknownId, UnknownPrefix);

            Db.Get().BuildingStatusItems.Add(Mechanical);
            Db.Get().BuildingStatusItems.Add(Overheat);
            Db.Get().BuildingStatusItems.Add(Both);
            Db.Get().BuildingStatusItems.Add(Unknown);

            ModLog.Info("已注册损坏原因状态项 4 项。");
        }

        /// <summary>按原因取对应状态项；无损坏时返回 null。</summary>
        public static StatusItem For(Hardness.DamageCause cause)
        {
            switch (cause)
            {
                case Hardness.DamageCause.Mechanical:
                    return Mechanical;

                case Hardness.DamageCause.Overheat:
                    return Overheat;

                case Hardness.DamageCause.MechanicalAndOverheat:
                    return Both;

                case Hardness.DamageCause.Unknown:
                    return Unknown;

                default:
                    return null;
            }
        }

        private static StatusItem Create(string id, string prefix)
        {
            StatusItem item = new StatusItem(
                id,
                prefix,
                Icon,
                StatusItem.IconType.Custom,
                NotificationType.BadMinor,
                allow_multiples: false,
                OverlayModes.None.ID);
            item.resolveTooltipCallback = (text, data) => text + "\n" + (data as MaterialOverhaulFab)?.HardnessText();
            return item;
        }
    }
}
