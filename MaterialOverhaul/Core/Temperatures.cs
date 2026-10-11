namespace MaterialOverhaul.Core
{
    /// <summary>
    /// 温度单位换算。判断层与游戏内部统一使用开尔文，设计文档用摄氏度，
    /// 因此所有以摄氏度给出的设计数值都经过这里换算，不散落 +273.15 的魔法数字。
    /// </summary>
    public static class Temperatures
    {
        /// <summary>摄氏度到开尔文的差值（0 ℃ = 273.15 K）。</summary>
        public const float KelvinOffset = 273.15f;

        public static float FromCelsius(float celsius)
        {
            return celsius + KelvinOffset;
        }

        public static float ToCelsius(float kelvin)
        {
            return kelvin - KelvinOffset;
        }
    }
}
