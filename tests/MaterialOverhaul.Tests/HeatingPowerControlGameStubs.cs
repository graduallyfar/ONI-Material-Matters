using System;

// 仅为运行实际 PowerControl partial 提供游戏接口，不代替 Unity 界面与存档验收。
namespace KSerialization
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeAttribute : Attribute { }
}

public interface ISingleSliderControl
{
    string SliderTitleKey { get; }
    string SliderUnits { get; }
    int SliderDecimalPlaces(int index);
    float GetSliderMin(int index);
    float GetSliderMax(int index);
    float GetSliderValue(int index);
    void SetSliderValue(float value, int index);
    string GetSliderTooltipKey(int index);
    string GetSliderTooltip(int index);
}

public static class Strings
{
    public static string Get(string key) { return "当前加热功率：{0} W"; }
}
