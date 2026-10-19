using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MaterialOverhaul.Game;

namespace MaterialOverhaul.Patches
{
    [HarmonyPatch(typeof(CircuitManager), "Sim200msLast")]
    internal static class Patch_PowerSettlement_Sim
    {
        private static void Prefix(float dt, float ___elapsedTime) { PowerSettlement.Begin(dt, ___elapsedTime); }
        private static void Postfix() { PowerSettlement.End(); }
        private static Exception Finalizer(Exception __exception) { PowerSettlement.End(); return __exception; }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var result = instructions.ToList();
            var counts = new Dictionary<string, int>();
            var replacements = new List<(CodeInstruction Instruction, MethodInfo Wrapper)>();
            foreach (CodeInstruction instruction in result)
            {
                if (!(instruction.operand is MethodInfo method) || method.DeclaringType != typeof(CircuitManager)
                    || (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)) continue;
                string replacement = null;
                if (method.Name == "PowerFromGenerator" || method.Name == "PowerFromBatteries" || method.Name == "ChargeBatteries") replacement = method.Name;
                else if (method.Name == "ChargeTransformers" && method.IsGenericMethod)
                {
                    Type source = method.GetGenericArguments()[0];
                    if (source != typeof(Generator) && source != typeof(Battery))
                        throw new InvalidOperationException("Unexpected transformer source type: " + source);
                    replacement = source == typeof(Generator) ? "ChargeFromGenerators" : "ChargeFromBatteries";
                }
                if (replacement == null) continue;
                counts[replacement] = counts.TryGetValue(replacement, out int count) ? count + 1 : 1;
                MethodInfo wrapper = typeof(PowerSettlementCalls).GetMethod(replacement, BindingFlags.Static | BindingFlags.NonPublic);
                if (wrapper == null || wrapper.ReturnType != method.ReturnType
                    || !wrapper.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                        new[] { typeof(CircuitManager) }.Concat(method.GetParameters().Select(p => p.ParameterType))))
                    throw new InvalidOperationException("Power wrapper signature mismatch: " + method);
                replacements.Add((instruction, wrapper));
            }
            // 目标744825共8个调用；全部吻合才返回，拒绝部分替换后仍声称损耗已启用。
            var expected = new Dictionary<string, int> { { "PowerFromGenerator", 2 }, { "PowerFromBatteries", 1 },
                { "ChargeBatteries", 2 }, { "ChargeFromGenerators", 2 }, { "ChargeFromBatteries", 1 } };
            if (counts.Count != expected.Count || expected.Any(pair => !counts.TryGetValue(pair.Key, out int count) || count != pair.Value))
                throw new InvalidOperationException("CircuitManager.Sim200msLast power call sites do not match target build 744825.");
            foreach (var replacement in replacements)
            { replacement.Instruction.opcode = OpCodes.Call; replacement.Instruction.operand = replacement.Wrapper; }
            PowerSettlement.PatchReady = true;
            return result;
        }
    }

    /// <summary>静态签名保留原实例参数；未支持的整网调用原私有方法，不走第二套近似结算。</summary>
    internal static class PowerSettlementCalls
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly MethodInfo NativeGenerator = typeof(CircuitManager).GetMethod("PowerFromGenerator", Flags);
        private static readonly MethodInfo NativeBatteries = typeof(CircuitManager).GetMethod("PowerFromBatteries", Flags);
        private static readonly MethodInfo NativeCharge = typeof(CircuitManager).GetMethod("ChargeBatteries", Flags);
        private static readonly MethodInfo NativeTransformers = typeof(CircuitManager).GetMethod("ChargeTransformers", Flags);
        private static readonly MethodInfo NativeChargeGenerators = NativeTransformers.MakeGenericMethod(typeof(Generator));
        private static readonly MethodInfo NativeChargeBatteries = NativeTransformers.MakeGenericMethod(typeof(Battery));

        internal static float PowerFromGenerator(CircuitManager manager, float needed, Generator generator, IEnergyConsumer consumer)
            => PowerSettlement.TryConsumer(consumer, needed, out float remaining) ? remaining
                : (float)NativeGenerator.Invoke(manager, new object[] { needed, generator, consumer });
        internal static float PowerFromBatteries(CircuitManager manager, float needed, List<Battery> batteries, IEnergyConsumer consumer)
            => PowerSettlement.TryConsumer(consumer, needed, out float remaining) ? remaining
                : (float)NativeBatteries.Invoke(manager, new object[] { needed, batteries, consumer });
        internal static void ChargeBatteries(CircuitManager manager, List<Battery> sinks, List<Generator> sources, ref float used)
            => Charge(manager, sinks, sources, ref used, false, NativeCharge);
        internal static void ChargeFromGenerators(CircuitManager manager, List<Battery> sinks, List<Generator> sources, ref float used)
            => Charge(manager, sinks, sources, ref used, true, NativeChargeGenerators);
        internal static void ChargeFromBatteries(CircuitManager manager, List<Battery> sinks, List<Battery> sources, ref float used)
            => Charge(manager, sinks, sources, ref used, true, NativeChargeBatteries);

        private static void Charge<T>(CircuitManager manager, List<Battery> sinks, List<T> sources, ref float used,
            bool transformer, MethodInfo original)
        {
            if (sinks.Count == 0) return;
            if (!PowerSettlement.TryCharge(sinks[0], transformer, out float delivered))
            {
                object[] arguments = { sinks, sources, used }; original.Invoke(manager, arguments); used = (float)arguments[2]; return;
            }
            used += delivered;
            for (int i = 1; i < sinks.Count; i++)
            {
                if (!PowerSettlement.TryCharge(sinks[i], transformer, out delivered))
                    throw new InvalidOperationException("Charging list contains different power circuits.");
                used += delivered;
            }
        }
    }
}
