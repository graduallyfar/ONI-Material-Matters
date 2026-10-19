using System;

namespace MaterialOverhaul.Power
{
    /// <summary>原版线材容量与主建造材料标识；电导率由独立配置提供。</summary>
    public sealed class LineMaterial
    {
        public string MaterialId { get; }
        public double CapacityWatts { get; }

        public LineMaterial(string materialId, double capacityWatts)
        {
            if (string.IsNullOrWhiteSpace(materialId)) throw new ArgumentException("Material id is required.", nameof(materialId));
            PowerNumbers.Positive(capacityWatts, nameof(capacityWatts));
            MaterialId = materialId;
            CapacityWatts = capacityWatts;
        }
    }

    public readonly struct LineSegment
    {
        public double Length { get; }
        public LineMaterial Material { get; }

        public LineSegment(double length, LineMaterial material)
        {
            PowerNumbers.Positive(length, nameof(length));
            Material = material ?? throw new ArgumentNullException(nameof(material));
            Length = length;
        }
    }

    internal static class PowerNumbers
    {
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static void Nonnegative(double value, string name)
        {
            if (!Finite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
        }

        internal static void Positive(double value, string name)
        {
            if (!Finite(value) || value <= 0) throw new ArgumentOutOfRangeException(name);
        }
    }
}
