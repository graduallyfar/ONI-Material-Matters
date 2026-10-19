using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MaterialOverhaul.Game;
using MaterialOverhaul.Patches;

namespace MaterialOverhaul.Tests
{
    public static class PowerPatchCallsTests
    {
        private static MethodInfo Method(string name) => typeof(CircuitManager).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        private static List<CodeInstruction> Calls()
        {
            return new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Call, Method("PowerFromGenerator")), new CodeInstruction(OpCodes.Call, Method("PowerFromGenerator")),
                new CodeInstruction(OpCodes.Call, Method("PowerFromBatteries")),
                new CodeInstruction(OpCodes.Call, Method("ChargeBatteries")), new CodeInstruction(OpCodes.Call, Method("ChargeBatteries")),
                new CodeInstruction(OpCodes.Call, Method("ChargeTransformers").MakeGenericMethod(typeof(Generator))),
                new CodeInstruction(OpCodes.Call, Method("ChargeTransformers").MakeGenericMethod(typeof(Generator))),
                new CodeInstruction(OpCodes.Call, Method("ChargeTransformers").MakeGenericMethod(typeof(Battery)))
            };
        }
        public static void Run()
        {
            TestRunner.Section("实际转接补丁：8个调用的签名、失败原子性和原版回退");
            PowerSettlement.Reset(); PowerSettlement.PatchReady = false;
            var missing = Calls(); missing.RemoveAt(0);
            bool rejected = false;
            try { Patch_PowerSettlement_Sim.Transpiler(missing).ToList(); } catch (InvalidOperationException) { rejected = true; }
            TestRunner.Check(rejected && !PowerSettlement.PatchReady, "reject partial target contract before enabling loss");
            TestRunner.Check(missing.All(c => ((MethodInfo)c.operand).DeclaringType == typeof(CircuitManager)), "failed contract leaves every original call intact");
            var calls = Calls(); var rewritten = Patch_PowerSettlement_Sim.Transpiler(calls).ToList();
            TestRunner.Check(PowerSettlement.PatchReady && rewritten.Count == 8, "all eight target calls replaced");
            TestRunner.Check(rewritten.All(c => ((MethodInfo)c.operand).IsStatic && ((MethodInfo)c.operand).DeclaringType == typeof(PowerSettlementCalls)), "every replacement is actual static adapter");
            TestRunner.Check(ReferenceEquals(calls[0], rewritten[0]), "instruction identity retains native labels and exception blocks");
            var manager = new CircuitManager(); var sink = new Battery(); var generator = new Generator(); var consumer = new EnergyConsumer();
            TestRunner.CheckEqual(100f, PowerSettlementCalls.PowerFromGenerator(manager, 100, generator, consumer), "unsupported circuit invokes native generator helper");
            PowerSettlementCalls.PowerFromBatteries(manager, 100, new List<Battery> { sink }, consumer);
            float used = 0;
            PowerSettlementCalls.ChargeBatteries(manager, new List<Battery> { sink }, new List<Generator> { generator }, ref used);
            TestRunner.CheckEqual(7f, used, "fallback preserves ordinary charging ref argument");
            PowerSettlementCalls.ChargeFromGenerators(manager, new List<Battery> { sink }, new List<Generator> { generator }, ref used);
            PowerSettlementCalls.ChargeFromBatteries(manager, new List<Battery> { sink }, new List<Battery>(), ref used);
            TestRunner.CheckEqual(29f, used, "both closed generic transformer fallbacks preserve ref accounting");
            TestRunner.Check(manager.NativeGeneratorCalls == 1 && manager.NativeBatteryCalls == 1 && manager.NativeChargeCalls == 1 && manager.NativeTransformerCalls == 2, "exactly one native fallback per call");
            PowerSettlement.PatchReady = false;
        }
    }
}
