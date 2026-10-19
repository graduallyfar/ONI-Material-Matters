using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Power
{
    public enum LineLossStatus { Success, MissingConductivity, InvalidInput, NumericOverflow }

    /// <summary>一条完整路径的 C0、C1、Z；混合路径仅在最后计算一次指数项。</summary>
    public sealed class LineLossCoefficients
    {
        public double C0 { get; }
        public double C1 { get; }
        public double Z { get; }
        public LineLossParameters Parameters { get; }

        internal LineLossCoefficients(double c0, double c1, double z, LineLossParameters parameters)
        {
            C0 = c0; C1 = c1; Z = z; Parameters = parameters;
        }

        public LineLossStatus Evaluate(double watts, out double baseWatts, out double lengthWatts)
        {
            baseWatts = lengthWatts = 0;
            if (!PowerNumbers.Finite(watts) || watts < 0) return LineLossStatus.InvalidInput;
            if (watts == 0) return LineLossStatus.Success;
            // 避免 P + Pc 在两者有限时溢出；同时保留大负载下的小基础修正。
            double w = watts > Parameters.Pc ? 1 / (1 + Parameters.Pc / watts)
                : (watts / Parameters.Pc) / (1 + watts / Parameters.Pc);
            double complement = watts > Parameters.Pc ? (Parameters.Pc / watts) / (1 + Parameters.Pc / watts)
                : 1 / (1 + watts / Parameters.Pc);
            double b = watts * (C0 + C1 * complement);
            double l = 0;
            if (Parameters.A > 0 && w > 0 && Z > 0)
            {
                double exponential;
                if (Z < 0.01)
                {
                    // exp(Z)-1-Z 在极小 Z 时会相消，直接从二阶项求和。
                    double term = Z * Z / 2;
                    exponential = term;
                    for (int n = 3; n <= 16; n++)
                    {
                        term *= Z / n;
                        exponential += term;
                    }
                }
                else exponential = Math.Exp(Z) - 1 - Z;
                l = watts * Parameters.A * w * exponential;
            }
            if (!PowerNumbers.Finite(b) || !PowerNumbers.Finite(l) || !PowerNumbers.Finite(b + l))
                return LineLossStatus.NumericOverflow;
            baseWatts = b; lengthWatts = l;
            return LineLossStatus.Success;
        }
    }

    public static class LineLossCalculator
    {
        public static LineLossStatus Compile(IReadOnlyList<LineSegment> segments, LineLossParameters p,
            IReadOnlyDictionary<string, double> conductivities, out LineLossCoefficients coefficients,
            out string missingMaterial)
        {
            coefficients = null; missingMaterial = null;
            if (segments == null || segments.Count == 0 || p == null || conductivities == null) return LineLossStatus.InvalidInput;
            double c0 = 0, c1 = 0, z = 0;
            foreach (LineSegment segment in segments)
            {
                if (segment.Material == null || !PowerNumbers.Finite(segment.Length) || segment.Length <= 0)
                    return LineLossStatus.InvalidInput;
                double sigma;
                if (!conductivities.TryGetValue(segment.Material.MaterialId, out sigma))
                {
                    missingMaterial = segment.Material.MaterialId;
                    return LineLossStatus.MissingConductivity;
                }
                if (!PowerNumbers.Finite(sigma) || sigma <= 0) return LineLossStatus.InvalidInput;
                double normalizedLength = segment.Length / p.L0;
                double capacityRatio = segment.Material.CapacityWatts / p.PowerReference;
                double conductivityRatio = sigma / p.ConductivityReference;
                double materialFactor = Math.Pow(1 / conductivityRatio, p.Gamma);
                double capacityFactor = Math.Pow(capacityRatio, p.Nu);
                double g = Math.Pow(capacityRatio, p.Alpha) * Math.Pow(conductivityRatio, p.Beta);
                double denominator = 1 + p.K * g;
                if (!PowerNumbers.Finite(normalizedLength) || normalizedLength <= 0
                    || !PowerNumbers.Finite(capacityRatio) || capacityRatio <= 0
                    || !PowerNumbers.Finite(conductivityRatio) || conductivityRatio <= 0
                    || !PowerNumbers.Finite(materialFactor) || !PowerNumbers.Finite(capacityFactor)
                    || !PowerNumbers.Finite(g) || !PowerNumbers.Finite(denominator)) return LineLossStatus.NumericOverflow;
                c0 += normalizedLength * materialFactor * p.A0;
                c1 += normalizedLength * materialFactor * p.A1 * capacityFactor;
                z += normalizedLength * (p.LambdaMin + p.DeltaLambda / denominator);
                if (!PowerNumbers.Finite(c0) || !PowerNumbers.Finite(c1) || !PowerNumbers.Finite(z))
                    return LineLossStatus.NumericOverflow;
            }
            coefficients = new LineLossCoefficients(c0, c1, z, p);
            return LineLossStatus.Success;
        }
    }
}
