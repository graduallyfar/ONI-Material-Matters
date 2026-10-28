using MaterialOverhaul.Core;
using MaterialOverhaul.Radiation;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 建筑的辐射状态栏条目：「辐射累积」在累积量达到显现限度一半、尚未放射时显示；
    /// 「显现放射」在建筑自身放射时显示，强度随累积量刷新。
    /// 用完整键读取 NAME／TOOLTIP，再调用显式文本构造重载。
    /// </summary>
    public static class RadiationStatusItems
    {
        public const string ManifestId = "MaterialOverhaulInducedRadiation";
        public const string AccumulatingId = "MaterialOverhaulRadiationAccumulating";

        private const string ManifestPrefix = "STRINGS.RADIATION.MATERIALOVERHAUL.STATUS.MANIFEST";
        private const string AccumulatingPrefix = "STRINGS.RADIATION.MATERIALOVERHAUL.STATUS.ACCUMULATING";

        public static StatusItem Manifest { get; private set; }

        public static StatusItem Accumulating { get; private set; }

        /// <summary>在 Db 初始化之后登记（Db.Initialize 会重建状态项集合）。</summary>
        public static void Register()
        {
            Db db = Db.Get();
            if (db == null || Manifest != null && db.BuildingStatusItems.TryGet(Manifest.Id) != null)
            {
                return;
            }

            Manifest = Create(db, ManifestId, ManifestPrefix);
            Accumulating = Create(db, AccumulatingId, AccumulatingPrefix);
            ModLog.Info("已注册辐射累积与显现状态项。");
        }

        private static StatusItem Create(Db db, string id, string prefix)
        {
            StatusItem item = new StatusItem(id, Strings.Get(prefix + ".NAME"), Strings.Get(prefix + ".TOOLTIP"),
                string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: false,
                OverlayModes.None.ID, showWorldIcon: false);
            item.resolveStringCallback = (text, data) => Resolve(text, data);
            item.resolveTooltipCallback = (text, data) => Resolve(text, data);
            db.BuildingStatusItems.Add(item);
            return item;
        }

        private static string Resolve(string text, object data)
        {
            RadiationActivation activation = data as RadiationActivation;
            float emit = activation == null ? 0f : activation.CurrentEmitRads;
            float accumulation = activation == null ? 0f : activation.Accumulation;
            float limit = activation == null ? 0f : activation.Limit;
            return text.Replace("{0}", emit.ToString("0.#"))
                .Replace("{1}", accumulation.ToString("0"))
                .Replace("{2}", limit.ToString("0"))
                .Replace("{3}", RadiationBalance.AccumulationHalfLifeCycles.ToString("0"));
        }
    }
}
