using System;
using System.Collections.Generic;

namespace MaterialOverhaul.Power
{
    public enum PowerReceiverKind { Consumer, Battery, TransformerInput }

    public readonly struct PowerTransferQuote
    {
        public double DeliveredJoules { get; }
        public double LossJoules { get; }
        public double SourceJoules => DeliveredJoules + LossJoules;
        internal PowerTransferQuote(double delivered, double loss) { DeliveredJoules = delivered; LossJoules = loss; }
    }

    /// <summary>
    /// 单个电网、单轮的独立账本。只计算，不触碰游戏能量或 wattsUsed。
    /// 同源同接收端先聚合焦耳再求损耗，调用切分不重置负载权重。
    /// </summary>
    public sealed class PowerTransferLedger
    {
        private sealed class Transfer
        {
            internal LineLossCoefficients Path;
            internal PowerReceiverKind Kind;
            internal double Delivered, Loss;
        }
        private readonly Dictionary<(int, int), Transfer> transfers = new Dictionary<(int, int), Transfer>();
        private readonly Dictionary<int, double> inputs = new Dictionary<int, double>();
        private readonly Dictionary<int, double> outputs = new Dictionary<int, double>();
        public double StepSeconds { get; private set; }
        public double DeliveredJoules { get; private set; }
        public double LossJoules { get; private set; }
        public double LossWatts => LossJoules / StepSeconds;
        public double SourceJoules => DeliveredJoules + LossJoules;

        public PowerTransferLedger(double stepSeconds) { BeginStep(stepSeconds); }
        public void BeginStep(double stepSeconds)
        {
            PowerNumbers.Positive(stepSeconds, nameof(stepSeconds));
            StepSeconds = stepSeconds;
            transfers.Clear(); inputs.Clear(); outputs.Clear(); DeliveredJoules = LossJoules = 0;
        }

        public double InputJoules(int receiver) => inputs.TryGetValue(receiver, out double value) ? value : 0;
        public double OutputJoules(int source) => outputs.TryGetValue(source, out double value) ? value : 0;

        /// <summary>求本次预算内可交付量；不修改账本。同源同接收端按累计负载计算边际损耗。</summary>
        public LineLossStatus Quote(int source, int receiver, PowerReceiverKind kind, double requested,
            double available, LineLossCoefficients path, out PowerTransferQuote quote)
        {
            quote = default;
            if (path == null || source == receiver || !PowerNumbers.Finite(requested) || requested < 0
                || !PowerNumbers.Finite(available) || available < 0
                || kind < PowerReceiverKind.Consumer || kind > PowerReceiverKind.TransformerInput)
                return LineLossStatus.InvalidInput;
            transfers.TryGetValue((source, receiver), out Transfer old);
            if (old != null && (!ReferenceEquals(old.Path, path) || old.Kind != kind)) return LineLossStatus.InvalidInput;
            double previous = old?.Delivered ?? 0, previousLoss = old?.Loss ?? 0;
            double low = 0, high = Math.Min(requested, available), bestLoss = 0;
            for (int iteration = 0; iteration < 64; iteration++)
            {
                double candidate = iteration == 0 ? high : low + (high - low) / 2;
                LineLossStatus status = path.Evaluate((previous + candidate) / StepSeconds, out double b, out double l);
                double loss = (b + l) * StepSeconds - previousLoss;
                // 非负系数下损耗单调；溢出只收缩上界，不能当成零损耗。
                if (status == LineLossStatus.NumericOverflow || !PowerNumbers.Finite(loss + candidate)) high = candidate;
                else if (status != LineLossStatus.Success) return status;
                else if (candidate + loss <= available)
                {
                    low = candidate; bestLoss = Math.Max(0, loss);
                    if (iteration == 0) break;
                }
                else high = candidate;
            }
            quote = new PowerTransferQuote(low, bestLoss);
            return LineLossStatus.Success;
        }

        public LineLossStatus Record(int source, int receiver, PowerReceiverKind kind, double deliveredJoules,
            LineLossCoefficients path)
        {
            if (path == null || source == receiver || !PowerNumbers.Finite(deliveredJoules) || deliveredJoules < 0
                || kind < PowerReceiverKind.Consumer || kind > PowerReceiverKind.TransformerInput) return LineLossStatus.InvalidInput;
            var key = (source, receiver);
            Transfer old;
            bool exists = transfers.TryGetValue(key, out old);
            if (exists && (!ReferenceEquals(old.Path, path) || old.Kind != kind)) return LineLossStatus.InvalidInput;
            double total = (exists ? old.Delivered : 0) + deliveredJoules;
            double watts = total / StepSeconds;
            if (!PowerNumbers.Finite(total) || !PowerNumbers.Finite(watts)) return LineLossStatus.NumericOverflow;
            double b, l;
            LineLossStatus status = path.Evaluate(watts, out b, out l);
            if (status != LineLossStatus.Success) return status;
            double loss = (b + l) * StepSeconds;
            double deltaLoss = loss - (exists ? old.Loss : 0);
            double newDelivered = DeliveredJoules + deliveredJoules, newLoss = LossJoules + deltaLoss;
            double newInput = InputJoules(receiver) + deliveredJoules;
            double newOutput = OutputJoules(source) + deliveredJoules + deltaLoss;
            if (!PowerNumbers.Finite(loss) || !PowerNumbers.Finite(newDelivered + newLoss)
                || !PowerNumbers.Finite(newInput) || !PowerNumbers.Finite(newOutput)) return LineLossStatus.NumericOverflow;
            if (!exists) { old = new Transfer { Path = path, Kind = kind }; transfers.Add(key, old); }
            old.Delivered = total; old.Loss = loss;
            DeliveredJoules = newDelivered; LossJoules = newLoss;
            inputs[receiver] = newInput; outputs[source] = newOutput;
            return LineLossStatus.Success;
        }
    }
}
