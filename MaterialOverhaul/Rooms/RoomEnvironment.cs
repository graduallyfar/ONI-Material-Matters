using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Rooms
{
    /// <summary>房间内一格的采样输入。只保留纯数值，便于离线测试。</summary>
    public struct RoomCellSample
    {
        /// <summary>该格元素温度（K）。</summary>
        public float TemperatureK;

        /// <summary>该格装饰度（已含建筑与光照贡献；实心格按 0 计）。</summary>
        public float Decor;

        /// <summary>该格是否有非真空元素。真空格不参与平均温度。</summary>
        public bool HasElement;
    }

    /// <summary>
    /// 房间平均环境计算（房间系统设计 §4.2.2、§4.3）。
    ///
    /// 工作口径（分设计 §4.4、开发计划 D04／D05 尚未定案前的实现基准，确认后须回写分设计）：
    /// - 平均装饰度：对房间全部实际格子取算术平均；实心格按 0 计，光照装饰计入。
    ///   不使用外接矩形，也不把「下方一格建筑的装饰度」再加一次——建筑的贡献已经由原版
    ///   <c>DecorProvider</c> 写入各格装饰度，重复相加会双计。
    /// - 平均温度：只对含非真空元素的格子取算术平均（等格平均，不按质量加权）；
    ///   真空格跳过。没有任何有效格时返回 <see cref="float.NaN"/>，条件判为不满足。
    /// - 不做取整、迟滞或封顶；比较使用原始浮点值，显示才做单位换算。
    /// </summary>
    public static class RoomEnvironment
    {
        public static float AverageTemperature(IReadOnlyList<RoomCellSample> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return float.NaN;
            }

            double sum = 0d;
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (!cells[i].HasElement)
                {
                    continue;
                }

                float value = cells[i].TemperatureK;
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                sum += value;
                count++;
            }

            return count == 0 ? float.NaN : (float)(sum / count);
        }

        public static float AverageDecor(IReadOnlyList<RoomCellSample> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return float.NaN;
            }

            double sum = 0d;
            int count = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                float value = cells[i].Decor;
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    continue;
                }

                sum += value;
                count++;
            }

            return count == 0 ? float.NaN : (float)(sum / count);
        }
    }
}
