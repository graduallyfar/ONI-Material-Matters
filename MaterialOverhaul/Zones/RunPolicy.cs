namespace MaterialOverhaul.Zones
{
    /// <summary>
    /// 建筑运行模式与强制运行开关（建筑细节文档 §2.1、§2.1.4）。
    ///
    /// 模式选择与强制运行是两件事：模式决定温区限制取哪一套，强制开关独立覆盖两者的停机限制。
    /// 建筑细节文档 §2.1.4：强制运行**不免除**材料、供电、自动化生产许可、操作和建筑完好条件。
    /// </summary>
    public struct RunPolicy
    {
        /// <summary>false = 默认模式（整个可工作温区都能生产）；true = 控制模式（只在最佳温区生产）。</summary>
        public bool ControlMode;

        /// <summary>强制运行开关。开启后不因温区越界停止生产，并持续加热。</summary>
        public bool Forced;

        public static RunPolicy DefaultMode
        {
            get { return new RunPolicy { ControlMode = false, Forced = false }; }
        }

        public static RunPolicy ControlModePolicy
        {
            get { return new RunPolicy { ControlMode = true, Forced = false }; }
        }

        public RunPolicy WithForced(bool forced)
        {
            RunPolicy copy = this;
            copy.Forced = forced;
            return copy;
        }

        public override string ToString()
        {
            return (ControlMode ? "控制模式" : "默认模式") + (Forced ? " · 强制运行" : string.Empty);
        }
    }
}
