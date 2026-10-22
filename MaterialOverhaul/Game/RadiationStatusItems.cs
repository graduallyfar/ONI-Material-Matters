using MaterialOverhaul.Core;
using MaterialOverhaul.Radiation;

namespace MaterialOverhaul.Game
{
    /// <summary>
    /// 活化建筑的状态栏条目「感生放射」。只在建筑自身放射时显示，强度随活化度刷新。
    /// 用完整键读取 NAME／TOOLTIP，再调用显式文本构造重载。
    /// </summary>
    public static class RadiationStatusItems
    {
        public const string InducedId = "MaterialOverhaulInducedRadiation";

        private const string Prefix = "STRINGS.RADIATION.MATERIALOVERHAUL.STATUS.INDUCED";

        public static StatusItem Induced { get; private set; }

        /// <summary>在 Db 初始化之后登记（Db.Initialize 会重建状态项集合）。</summary>
        public static void Register()
        {
            Db db = Db.Get();
            if (db == null || Induced != null && db.BuildingStatusItems.TryGet(Induced.Id) != null)
            {
                return;
            }

            Induced = new StatusItem(InducedId, Strings.Get(Prefix + ".NAME"), Strings.Get(Prefix + ".TOOLTIP"),
                string.Empty, StatusItem.IconType.Info, NotificationType.Neutral, allow_multiples: false,
                OverlayModes.None.ID, showWorldIcon: false);
            Induced.resolveStringCallback = (text, data) => Resolve(text, data);
            Induced.resolveTooltipCallback = (text, data) => Resolve(text, data);
            db.BuildingStatusItems.Add(Induced);
            ModLog.Info("已注册感生放射状态项。");
        }

        private static string Resolve(string text, object data)
        {
            RadiationActivation activation = data as RadiationActivation;
            float rads = activation == null ? 0f : activation.CurrentEmitRads;
            return text.Replace("{0}", rads.ToString("0"))
                .Replace("{1}", RadiationBalance.BuildingHalfLifeCycles.ToString("0"));
        }
    }
}
