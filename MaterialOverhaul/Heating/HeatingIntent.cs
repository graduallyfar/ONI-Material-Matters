namespace MaterialOverhaul.Heating
{
    /// <summary>加热意图。与生产许可分开判断：自动化红信号只停止生产，不阻止预热与补热。</summary>
    public enum HeatingIntent
    {
        /// <summary>不加热。没有有效批次、断电、手动禁用、损坏停机，或处于高温保护。</summary>
        None = 0,

        /// <summary>预热：尚未达到启动阈值，向启动阈值加热。</summary>
        Preheat = 1,

        /// <summary>补热：控制模式下已在生产，但温度低于补热线，边生产边补热。</summary>
        TopUp = 2,

        /// <summary>持续加热：强制运行开启，不因达到最佳温区、越过最高可工作温度或过热而停止。</summary>
        Sustained = 3
    }
}
