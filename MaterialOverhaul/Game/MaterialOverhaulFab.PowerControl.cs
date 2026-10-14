using KSerialization;
using MaterialOverhaul.Core;
using MaterialOverhaul.Heating;

namespace MaterialOverhaul.Game
{
    /// <summary>复用原版单滑条侧屏；只有已有内置加热的建筑显示，不扩大接入名单。</summary>
    public partial class MaterialOverhaulFab : ISingleSliderControl
    {
        private const string HeatingSliderPrefix = "STRINGS.UI.UISIDESCREENS.MATERIALOVERHAUL.HEATING_POWER.";

        [Serialize] private float configuredHeatingWatts = Balance.DefaultHeatingWatts;

        public float HeatingWatts { get { return HeatingPower.NormalizeWatts(configuredHeatingWatts); } }
        public string SliderTitleKey { get { return HeatingSliderPrefix + "TITLE"; } }
        public string SliderUnits { get { return " W"; } }
        public int SliderDecimalPlaces(int index) { return 0; }
        public float GetSliderMin(int index) { return Balance.MinHeatingWatts; }
        // 原版 SingleSliderSideScreen 通过 min == max 隐藏不适用目标。
        public float GetSliderMax(int index) { return SupportsBuiltInHeating ? Balance.MaxHeatingWatts : Balance.MinHeatingWatts; }
        public float GetSliderValue(int index) { return HeatingWatts; }
        public string GetSliderTooltipKey(int index) { return HeatingSliderPrefix + "TOOLTIP"; }
        public string GetSliderTooltip(int index) { return string.Format(Strings.Get(GetSliderTooltipKey(index)), HeatingWatts.ToString("F0")); }

        public void SetSliderValue(float watts, int index)
        {
            if (!SupportsBuiltInHeating) return;
            configuredHeatingWatts = HeatingPower.NormalizeWatts(watts);
            // 原版选择建筑也会调用此方法。只保存设置，在下一模拟步统一计算需求与注热，
            // 不在 UI 回调里推进生产、改温度或重置运行状态。
        }

        private void RestoreHeatingPower()
        {
            configuredHeatingWatts = HeatingPower.NormalizeWatts(configuredHeatingWatts);
        }
    }
}
