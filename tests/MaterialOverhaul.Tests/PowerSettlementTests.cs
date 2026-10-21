using System;
using System.Collections.Generic;
using MaterialOverhaul.Game;
using MaterialOverhaul.Power;

namespace MaterialOverhaul.Tests
{
    public static class PowerSettlementTests
    {
        private static void Close(double expected, double actual, string name)
            => TestRunner.Check(Math.Abs(expected - actual) < 0.002, name + ": " + expected + " / " + actual);

        public static void Run()
        {
            Budgets(); WorkingBalance(); NearestAndCharging(); TiesAndSelfCharge(); Transformers();
            MultiStepCircuits(); TransformerChain(); FallbackAndReload();
            PowerSettlement.Reset(); PowerTopologyMonitor.Reset(); PowerSettlement.PatchReady = false;
        }

        private static void Budgets()
        {
            TestRunner.Section("独立算术核对预算反解、分笔累加、边界和不变性");
            var path = PowerFoundationTests.Compile(new[] { new LineSegment(200, new LineMaterial("test-copper", 1000)) });
            var ledger = new PowerTransferLedger(.2);
            for (int i = 0; i < 100; i++)
            {
                double budget = i * 13.7, requested = 400;
                var status = ledger.Quote(1, 2, PowerReceiverKind.Consumer, requested, budget, path, out var quote);
                TestRunner.Check(status == LineLossStatus.Success && quote.SourceJoules <= budget, "never exceed budget " + i);
                path.Evaluate(quote.DeliveredJoules / .2, out double b, out double l);
                Close((b + l) * .2, quote.LossJoules, "quote matches independent formula " + i);
                TestRunner.Check(quote.DeliveredJoules >= 0 && quote.DeliveredJoules <= requested, "bounded delivery " + i);
                if (quote.DeliveredJoules < requested) Close(budget, quote.SourceJoules, "tight inverse " + i);
            }
            Close(0, ledger.SourceJoules, "quoting never debits ledger");
            ledger.Quote(1, 2, PowerReceiverKind.Consumer, 30, 10000, path, out var first);
            ledger.Record(1, 2, PowerReceiverKind.Consumer, first.DeliveredJoules, path);
            ledger.Quote(1, 2, PowerReceiverKind.Consumer, 40, 10000, path, out var second);
            ledger.Record(1, 2, PowerReceiverKind.Consumer, second.DeliveredJoules, path);
            path.Evaluate(70 / .2, out double baseLoss, out double lengthLoss);
            Close((baseLoss + lengthLoss) * .2, first.LossJoules + second.LossJoules, "split calls retain cumulative nonlinear load");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput, ledger.Quote(1, 1, PowerReceiverKind.Consumer, 1, 2, path, out _), "self transfer rejected");
            TestRunner.CheckEqual(LineLossStatus.InvalidInput, ledger.Quote(1, 3, PowerReceiverKind.Consumer, 1, double.NaN, path, out _), "invalid budget rejected");
            ledger.BeginStep(.2); Close(0, ledger.SourceJoules, "new step clears transient state");
        }

        private static UtilityNetworkManager<ElectricalUtilityNetwork, Wire> Start()
        {
            PowerSettlement.Reset(); PowerTopologyMonitor.Reset(); PowerSettlement.PatchReady = true;
            var manager = new UtilityNetworkManager<ElectricalUtilityNetwork, Wire>();
            global::Game.Instance = new global::Game { electricalConduitSystem = manager };
            PowerTopologyMonitor.Attach(global::Game.Instance); ReportManager.Instance.Entries.Clear();
            return manager;
        }

        private static void WorkingBalance()
        {
            TestRunner.Section("获准首轮初值与独立算例");
            TestRunner.CheckEqual(29, PowerBalance.Conductivities.Count, "native metal and ore table has 29 explicit IDs");
            foreach (var item in PowerBalance.Conductivities)
                TestRunner.Check(item.Value > 0 && !double.IsNaN(item.Value) && !double.IsInfinity(item.Value), "positive conductivity " + item.Key);
            var cases = new[]
            {
                ("Copper", 1000, 20, 120, 2.150), ("Copper", 1000, 100, 1000, 84.761),
                ("Copper", 1000, 300, 1000, 552.937), ("Cuprite", 1000, 100, 1000, 125.943),
                ("IronOre", 1000, 100, 1000, 232.324), ("Copper", 50000, 100, 120, 65.548),
                ("Copper", 50000, 100, 20000, 1133.846)
            };
            var values = (IReadOnlyDictionary<string, double>)PowerBalance.Conductivities;
            foreach (var entry in cases)
            {
                TestRunner.CheckEqual(LineLossStatus.Success, LineLossCalculator.Compile(
                    new[] { new LineSegment(entry.Item3, new LineMaterial(entry.Item1, entry.Item2)) },
                    PowerBalance.Parameters, values, out var coefficients, out _), "working path compiles");
                coefficients.Evaluate(entry.Item4, out double b, out double l);
                Close(entry.Item5, b + l, "documented independently evaluated scenario " + entry.Item1 + "/" + entry.Item3);
            }
        }

        private static void TiesAndSelfCharge()
        {
            TestRunner.Section("等距、源额度更新、同格接电与同建筑储能自充排除");
            var manager = Start(); Line(manager, 0, 3);
            var lowerCell = new Generator { PowerCell = 0, Joules = 1000 };
            var higherCell = new Generator { PowerCell = 2, Joules = 1000 };
            var consumer = new EnergyConsumer { PowerCell = 1 };
            Rebuild(manager, new[] { higherCell, lowerCell }, new IEnergyConsumer[] { consumer });
            PowerSettlement.Begin(.2f, 0);
            float remaining = MaterialOverhaul.Patches.PowerSettlementCalls.PowerFromGenerator(global::Game.Instance.circuitManager, 100, higherCell, consumer);
            Close(0, remaining, "actual helper wrapper handles supported circuit");
            TestRunner.Check(lowerCell.Joules < 900 && higherCell.Joules == 1000, "tie follows power cell rather than list insertion order");
            PowerSettlement.End(); lowerCell.Joules = 0;
            PowerSettlement.Begin(.2f, 0); PowerSettlement.TryConsumer(consumer, 100, out remaining);
            TestRunner.Check(remaining == 0 && higherCell.Joules < 900, "availability rechecked without rebuilding topology"); PowerSettlement.End();
            lowerCell.Joules = 1000; consumer.PowerCell = 0; consumer.gameObject = lowerCell.gameObject;
            Rebuild(manager, new[] { lowerCell }, new IEnergyConsumer[] { consumer });
            PowerSettlement.Begin(.2f, 0); PowerSettlement.TryConsumer(consumer, 100, out remaining);
            TestRunner.Check(remaining == 0 && lowerCell.Joules < 900, "ordinary consumer may draw from same building at length one"); PowerSettlement.End();
            var input = new Battery { PowerCell = 0, Joules = 0, gameObject = lowerCell.gameObject, ChargeCapacity = 20 };
            Rebuild(manager, new[] { lowerCell }, new IEnergyConsumer[] { input });
            PowerSettlement.Begin(.2f, 0); PowerSettlement.TryCharge(input, false, out float charged);
            Close(0, charged, "storage cannot charge from its own building output"); PowerSettlement.End();
        }
        private static void Line(UtilityNetworkManager<ElectricalUtilityNetwork, Wire> manager, int start, int length, int network = 1, SimHashes material = SimHashes.Copper)
        {
            for (int i = 0; i < length; i++)
            {
                var wire = new Wire(); wire.SetComponent(new PrimaryElement { Element = new Element { id = material } });
                manager.Put(start + i, wire, (i > 0 ? UtilityConnections.Left : 0) | (i + 1 < length ? UtilityConnections.Right : 0), network);
            }
        }
        private static void Rebuild(UtilityNetworkManager<ElectricalUtilityNetwork, Wire> manager, Generator[] generators, IEnergyConsumer[] receivers)
        {
            manager.SimulateRebuild();
            PowerTopologyMonitor.AfterCircuitRebuild(generators, receivers, Array.Empty<WireUtilityNetworkLink>());
            PowerSettlement.Rebuild(generators, receivers);
        }

        private static void NearestAndCharging()
        {
            TestRunner.Section("实际接入：最近电池放电、欠供接续、同轮充电和信息卡");
            var manager = Start(); Line(manager, 0, 5);
            var far = new Generator { PowerCell = 0, Joules = 1000 };
            var battery = new Battery { PowerCell = 3, Joules = 1 };
            var consumer = new EnergyConsumer { PowerCell = 3 };
            Rebuild(manager, new[] { far }, new IEnergyConsumer[] { consumer, battery });
            PowerSettlement.Begin(.1f, 0);
            TestRunner.Check(!PowerSettlement.TryConsumer(consumer, 100, out _), "no settlement before native 0.2 second threshold");
            PowerSettlement.Begin(.2f, 0);
            TestRunner.Check(PowerSettlement.TryConsumer(consumer, 100, out float remaining), "consumer handled");
            Close(0, remaining, "far source covers nearest battery shortfall");
            Close(0, battery.Joules, "nearest battery really debited ahead of far generator");
            TestRunner.Check(far.Joules < 901 && far.Joules > 899, "far source covers remaining delivery plus its path loss");
            float afterConsumer = far.Joules;
            PowerSettlement.TryConsumer(consumer, remaining, out _);
            Close(afterConsumer, far.Joules, "native retry helpers never debit consumer twice");
            TestRunner.Check(PowerSettlement.TryCharge(battery, false, out float charge) && charge > 0, "same battery can receive later in same step");
            TestRunner.Check(far.Joules < .001f && battery.Joules > 0, "charging uses remaining generator budget");
            Close(1001 - far.Joules - battery.Joules, 100 + ReadLoss(1) * .2, "entire circuit conserves energy including both battery directions");
            PowerSettlement.TryCharge(battery, false, out float duplicate);
            Close(0, duplicate, "second native charging stage cannot duplicate transfer");
            PowerSettlement.End();
            var target = new UnityEngine.GameObject(); target.Components.Add(consumer);
            var panel = new CollapsibleDetailContentPanel();
            MaterialOverhaul.Patches.Patch_PowerInfoCard_WireCount.Prefix(panel, target);
            TestRunner.Check(panel.Rows["MaterialOverhaul.PowerLoss"].Text.Contains(" W"), "real loss shown in info card");
            TestRunner.Check(panel.Rows["MaterialOverhaul.PowerDelivered"].Text.Contains(" W"), "all receiving transfers shown");

            TestRunner.Section("普通电池不相互充电；闲置电网不产生待机线路损耗");
            var other = new Battery { PowerCell = 4, Joules = 0 };
            Rebuild(manager, Array.Empty<Generator>(), new IEnergyConsumer[] { battery, other, consumer });
            PowerSettlement.Begin(.2f, 0);
            PowerSettlement.TryCharge(other, false, out float batteryCharge);
            Close(0, batteryCharge, "battery-to-battery charging excluded");
            Close(0, ReadLoss(1), "idle route has no loss");
            PowerSettlement.End();

            far.Joules = .5f;
            Rebuild(manager, new[] { far }, new IEnergyConsumer[] { other });
            PowerSettlement.Begin(.2f, 0); PowerSettlement.TryCharge(other, false, out batteryCharge);
            TestRunner.Check(batteryCharge == 0 && far.Joules == .5f, "ordinary charging preserves native 1J source threshold"); PowerSettlement.End();
            far.Joules = 1;
            PowerSettlement.Begin(.2f, 0); PowerSettlement.TryCharge(other, false, out batteryCharge);
            TestRunner.Check(batteryCharge > 0 && far.Joules >= 0, "ordinary charging starts at exactly 1J"); PowerSettlement.End();

            TestRunner.Section("真实欠供：只交付预算内电量，不补发能量或扣到负数");
            far.Joules = 1; battery.Joules = 0;
            Rebuild(manager, new[] { far }, new IEnergyConsumer[] { consumer });
            PowerSettlement.Begin(.2f, 0); PowerSettlement.TryConsumer(consumer, 100, out remaining);
            TestRunner.Check(remaining > 99 && far.Joules >= 0 && far.Joules < .001f, "partial delivery preserves native unpowered remainder");
            PowerSettlement.End();
        }

        private static double ReadLoss(ushort id)
        {
            TestRunner.Check(PowerSettlement.TryRead(id, out double loss, out _, out _), "circuit has a real settlement snapshot");
            return loss;
        }

        private static void Transformers()
        {
            TestRunner.Section("变压器上下游独立功率、输入限额、输出内部电池只扣一次");
            var manager = Start(); Line(manager, 0, 3); Line(manager, 20, 3, 2);
            var upstream = new Generator { PowerCell = 0, Joules = 2000 };
            var input = new Battery { PowerCell = 2, Joules = 1000, ChargeCapacity = 200 };
            var output = new PowerTransformer { PowerCell = 20, Joules = 1000, Input = input };
            input.powerTransformer = output; input.gameObject = output.gameObject;
            var consumer = new EnergyConsumer { PowerCell = 22 };
            Rebuild(manager, new Generator[] { upstream, output }, new IEnergyConsumer[] { input, consumer });
            PowerSettlement.Begin(.2f, 0); PowerSettlement.TryConsumer(consumer, 100, out float remaining);
            Close(0, remaining, "downstream delivered"); Close(output.Joules, input.Joules, "transformer debit updates internal battery once");
            float inputBefore = input.Joules;
            PowerSettlement.TryCharge(input, true, out float charged);
            Close(200, charged, "input respects original per-step ChargeCapacity");
            Close(inputBefore + 200, input.Joules, "input credited delivered energy only");
            PowerSettlement.TryRead(1, out double upperLoss, out double upperDelivered, out _);
            PowerSettlement.TryRead(2, out double lowerLoss, out double lowerDelivered, out _);
            Close(1000, upperDelivered, "input actual power is 1000W"); Close(500, lowerDelivered, "output actual power independently 500W");
            Close(2000 - upstream.Joules, 200 + upperLoss * .2, "upstream conservation");
            Close(1000 - output.Joules, 100 + lowerLoss * .2, "downstream conservation");
            PowerSettlement.End();
        }

        private static void MultiStepCircuits()
        {
            TestRunner.Section("200轮多源多设备：真实转接入口、跨电网隔离、换材与能量收支");
            var manager = Start(); Line(manager, 0, 5); Line(manager, 20, 3, 2);
            var first = new Generator { PowerCell = 0, Joules = 100 };
            var second = new Generator { PowerCell = 4, Joules = 100 };
            var battery = new Battery { PowerCell = 2, Joules = 300, Capacity = 1000 };
            var left = new EnergyConsumer { PowerCell = 1 };
            var right = new EnergyConsumer { PowerCell = 3 };
            var separate = new Generator { PowerCell = 20, Joules = 500 };
            var separateConsumer = new EnergyConsumer { PowerCell = 22 };
            Generator[] sources = { second, separate, first };
            IEnergyConsumer[] receivers = { right, battery, separateConsumer, left };
            Rebuild(manager, sources, receivers);
            var cm = global::Game.Instance.circuitManager;
            var sinks = new List<Battery> { battery };
            var generators = new List<Generator> { first, second };
            var noGenerators = new List<Generator>();
            var batteries = new List<Battery> { battery };
            for (int round = 0; round < 200; round++)
            {
                // 在测试中模拟本轮发电与原版EnergySim准备；不会运行真实游戏模拟器。
                first.Joules += round % 7 == 0 ? 0 : 90;
                second.Joules += round % 11 == 0 ? 0 : 60;
                separate.Joules = 500; battery.ChargeCapacity = float.PositiveInfinity;
                if (round % 17 == 0)
                {
                    Line(manager, 0, 5, material: round % 34 == 0 ? SimHashes.Copper : SimHashes.GoldAmalgam);
                    Rebuild(manager, sources, receivers);
                }
                double before = (double)first.Joules + second.Joules + battery.Joules;
                ReportManager.Instance.Entries.Clear();
                PowerSettlement.Begin(.2f, 0);
                float requestLeft = round % 9 == 0 ? 500 : round % 3 == 0 ? 0 : 60;
                float requestRight = round % 13 == 0 ? 500 : 80;
                float leftRemainder = MaterialOverhaul.Patches.PowerSettlementCalls.PowerFromGenerator(cm, requestLeft, first, left);
                float rightRemainder = MaterialOverhaul.Patches.PowerSettlementCalls.PowerFromGenerator(cm, requestRight, second, right);
                float paidLeft = requestLeft - leftRemainder, paidRight = requestRight - rightRemainder;
                float firstAfter = first.Joules, secondAfter = second.Joules, batteryAfter = battery.Joules;
                MaterialOverhaul.Patches.PowerSettlementCalls.PowerFromBatteries(cm, leftRemainder, batteries, left);
                MaterialOverhaul.Patches.PowerSettlementCalls.PowerFromBatteries(cm, rightRemainder, batteries, right);
                TestRunner.Check(first.Joules == firstAfter && second.Joules == secondAfter && battery.Joules == batteryAfter,
                    "native consumer retries cannot debit twice, round " + round);
                float charge = 0;
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeBatteries(cm, sinks, generators, ref charge);
                float firstCharge = charge;
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeBatteries(cm, sinks, noGenerators, ref charge);
                TestRunner.Check(charge == firstCharge, "second charging category cannot add duplicate delivery, round " + round);
                TestRunner.Check(PowerSettlement.TryRead(1, out double loss, out double delivered, out _), "active circuit snapshot " + round);
                double after = (double)first.Joules + second.Joules + battery.Joules;
                Close(before - after, paidLeft + paidRight + loss * .2, "world energy balance " + round);
                Close(paidLeft + paidRight + charge, delivered * .2, "delivery includes charge separately " + round);
                double reported = 0;
                foreach (var entry in ReportManager.Instance.Entries) reported -= entry.Joules;
                Close(before - after, reported, "report contains consumer use and loss without counting stored charge " + round);
                TestRunner.Check(first.Joules >= 0 && second.Joules >= 0 && battery.Joules >= 0 && battery.Joules <= battery.Capacity,
                    "storage remains within native bounds " + round);
                float separateRemainder = MaterialOverhaul.Patches.PowerSettlementCalls.PowerFromGenerator(cm, 20, separate, separateConsumer);
                Close(0, separateRemainder, "separate circuit has its own budget " + round);
                Close(after, (double)first.Joules + second.Joules + battery.Joules, "other circuit never spends first circuit energy " + round);
                TestRunner.Check(PowerSettlement.TryRead(2, out double separateLoss, out _, out _), "separate circuit snapshot " + round);
                Close(500 - separate.Joules, 20 + separateLoss * .2, "separate circuit conservation " + round);
                PowerSettlement.End();
            }
            TestRunner.Check(cm.NativeGeneratorCalls == 0 && cm.NativeBatteryCalls == 0 && cm.NativeChargeCalls == 0,
                "all supported routes use settlement rather than silently falling back");
        }

        private static void TransformerChain()
        {
            TestRunner.Section("两级变压器：上下游充电入口、输出限额、储能同步与全链守恒");
            var manager = Start(); Line(manager, 0, 3); Line(manager, 20, 3, 2); Line(manager, 40, 3, 3);
            var generator = new Generator { PowerCell = 0, Joules = 2000 };
            var inputA = new Battery { PowerCell = 2, Joules = 1000, Capacity = 4000 };
            var outputA = new PowerTransformer { PowerCell = 20, Input = inputA };
            inputA.powerTransformer = outputA; inputA.gameObject = outputA.gameObject;
            var inputB = new Battery { PowerCell = 22, Joules = 1000, Capacity = 4000 };
            var outputB = new PowerTransformer { PowerCell = 40, Input = inputB };
            inputB.powerTransformer = outputB; inputB.gameObject = outputB.gameObject;
            var consumer = new EnergyConsumer { PowerCell = 42 };
            Rebuild(manager, new Generator[] { outputB, generator, outputA }, new IEnergyConsumer[] { inputA, consumer, inputB });
            var cm = global::Game.Instance.circuitManager;
            var sinkA = new List<Battery> { inputA }; var sinkB = new List<Battery> { inputB };
            var emptyGenerators = new List<Generator>(); var emptyBatteries = new List<Battery>();
            for (int round = 0; round < 30; round++)
            {
                generator.Joules = 2000;
                outputA.Joules = Math.Min(inputA.Joules, 200); outputB.Joules = Math.Min(inputB.Joules, 200);
                inputA.ChargeCapacity = inputB.ChargeCapacity = 200;
                double before = (double)generator.Joules + inputA.Joules + inputB.Joules;
                ReportManager.Instance.Entries.Clear(); PowerSettlement.Begin(.2f, 0);
                float request = round % 5 == 0 ? 0 : 100;
                float remainder = MaterialOverhaul.Patches.PowerSettlementCalls.PowerFromGenerator(cm, request, outputB, consumer);
                float deliveredToConsumer = request - remainder;
                float aCharged = 0, bCharged = 0;
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeFromGenerators(cm, sinkA, new List<Generator> { generator }, ref aCharged);
                // 中间电网的普通发电机列表为空，仍须按真实变压器输出候选供电。
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeFromGenerators(cm, sinkB, emptyGenerators, ref bCharged);
                float aOnce = aCharged, bOnce = bCharged;
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeFromGenerators(cm, sinkA, emptyGenerators, ref aCharged);
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeFromGenerators(cm, sinkB, new List<Generator> { outputA }, ref bCharged);
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeFromBatteries(cm, sinkA, emptyBatteries, ref aCharged);
                MaterialOverhaul.Patches.PowerSettlementCalls.ChargeFromBatteries(cm, sinkB, emptyBatteries, ref bCharged);
                TestRunner.Check(aOnce == aCharged && bOnce == bCharged, "native later charging stages do not re-charge chain " + round);
                TestRunner.Check(aCharged <= 200 && bCharged <= 200 && outputA.Joules >= 0 && outputB.Joules >= 0,
                    "both original 1000W output and input budgets hold " + round);
                double allLoss = 0;
                for (ushort circuit = 1; circuit <= 3; circuit++)
                {
                    TestRunner.Check(PowerSettlement.TryRead(circuit, out double loss, out double sent, out _), "chain circuit ready " + circuit);
                    allLoss += loss * .2;
                    double expected = circuit == 1 ? aCharged : circuit == 2 ? bCharged : deliveredToConsumer;
                    Close(expected, sent * .2, "each circuit records actual receiving power " + circuit);
                }
                double after = (double)generator.Joules + inputA.Joules + inputB.Joules;
                // outputA/B只是本轮输出额度，不能把它们当第二份真实储能再计一次。
                Close(before - after, deliveredToConsumer + allLoss, "two-stage total physical storage conservation " + round);
                double reported = 0; foreach (var entry in ReportManager.Instance.Entries) reported -= entry.Joules;
                Close(before - after, reported, "chain energy report conservation " + round);
                PowerSettlement.End();
            }
            TestRunner.Check(cm.NativeTransformerCalls == 0, "supported transformer charging never falls back to free native transfer");
        }

        private static void FallbackAndReload()
        {
            TestRunner.Section("兼容回退、断线、换材和读档失效");
            var manager = Start(); Line(manager, 0, 3);
            var source = new Generator { PowerCell = 0 };
            var consumer = new EnergyConsumer { PowerCell = 2, IsVirtual = true };
            global::Game.Instance.circuitManager.VirtualIds[consumer] = 1;
            Rebuild(manager, new[] { source }, new IEnergyConsumer[] { consumer });
            PowerSettlement.Begin(.2f, 0);
            TestRunner.Check(!PowerSettlement.TryConsumer(consumer, 100, out _), "virtual endpoint leaves whole circuit native");
            Close(1000, source.Joules, "fallback never partially debits"); PowerSettlement.End();
            consumer.IsVirtual = false;
            Line(manager, 0, 3, material: SimHashes.TestUnknown);
            Rebuild(manager, new[] { source }, new IEnergyConsumer[] { consumer }); PowerSettlement.Begin(.2f, 0);
            TestRunner.Check(!PowerSettlement.TryConsumer(consumer, 100, out _), "unknown material never receives zero loss");
            TestRunner.Check(!PowerSettlement.TryRead(1, out _, out _, out string reason) && reason.Contains("TestUnknown"), "UI names unknown material");
            PowerSettlement.End();
            Line(manager, 0, 3); Rebuild(manager, new[] { source }, new IEnergyConsumer[] { consumer });
            PowerSettlement.Begin(.2f, 0); TestRunner.Check(PowerSettlement.TryConsumer(consumer, 100, out _), "supported replacement rebuilds coefficients"); PowerSettlement.End();
            manager.Put(1, new Wire(), 0);
            TestRunner.Check(!PowerSettlement.TryRead(1, out _, out _, out _), "dirty topology hides stale loss");
            manager.SimulateRebuild();
            TestRunner.Check(!PowerSettlement.TryRead(1, out _, out _, out _), "rebuild callback clears settlement snapshot");
            PowerSettlement.Reset(); PowerTopologyMonitor.Reset();
            TestRunner.Check(!PowerSettlement.TryRead(1, out _, out _, out _), "world cleanup leaves no old circuit ledger");
        }
    }
}
