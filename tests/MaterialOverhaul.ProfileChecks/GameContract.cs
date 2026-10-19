using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static partial class Program
{
    // 只读目标DLL的IL和签名；不把游戏程序集加载到测试进程。
    private static void InspectGameContract(string path)
    {
        using var stream = File.OpenRead(path); using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader(); var provider = new SignatureNames();
        var circuit = reader.TypeDefinitions.Select(reader.GetTypeDefinition).Single(t => reader.GetString(t.Name) == "CircuitManager");
        var methods = circuit.GetMethods().Select(reader.GetMethodDefinition)
            .Where(m => reader.GetString(m.Name) is "PowerFromGenerator" or "PowerFromBatteries" or "ChargeBatteries" or "ChargeTransformers" or "Sim200msLast")
            .ToDictionary(m => reader.GetString(m.Name));
        var signatures = new Dictionary<string, string>
        {
            ["PowerFromGenerator"] = "Single(Single,Generator,IEnergyConsumer)",
            ["PowerFromBatteries"] = "Single(Single,List<Battery>,IEnergyConsumer)",
            ["ChargeBatteries"] = "Void(List<Battery>,List<Generator>,Single&)",
            ["ChargeTransformers"] = "Void(List<Battery>,List<!!0>,Single&)"
        };
        foreach (var pair in signatures)
        {
            var signature = methods[pair.Key].DecodeSignature(provider, (object?)null);
            Check(signature.ReturnType + "(" + string.Join(",", signature.ParameterTypes) + ")" == pair.Value, "原版签名改变：" + pair.Key);
        }
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(op => unchecked((ushort)op.Value));
        var calls = new Dictionary<string, int>();
        byte[] il = pe.GetMethodBody(methods["Sim200msLast"].RelativeVirtualAddress).GetILBytes()!;
        for (int offset = 0; offset < il.Length;)
        {
            ushort value = il[offset++]; if (value == 0xfe) value = (ushort)(0xfe00 | il[offset++]);
            OpCode opcode = opcodes[value];
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset)); string generic = "";
                if (handle.Kind == HandleKind.MethodSpecification)
                {
                    var specification = reader.GetMethodSpecification((MethodSpecificationHandle)handle);
                    generic = "<" + string.Join(",", specification.DecodeSignature(provider, (object?)null)) + ">"; handle = specification.Method;
                }
                string name = "", owner = "";
                if (handle.Kind == HandleKind.MethodDefinition)
                {
                    var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    name = reader.GetString(method.Name); owner = reader.GetString(reader.GetTypeDefinition(method.GetDeclaringType()).Name);
                }
                if (owner == "CircuitManager" && signatures.ContainsKey(name))
                {
                    string key = name + generic; calls[key] = calls.GetValueOrDefault(key) + 1;
                }
            }
            offset += opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, offset) * 4,
                _ => 4
            };
        }
        var expected = new Dictionary<string, int> { ["PowerFromGenerator"] = 2, ["PowerFromBatteries"] = 1,
            ["ChargeBatteries"] = 2, ["ChargeTransformers<Generator>"] = 2, ["ChargeTransformers<Battery>"] = 1 };
        Check(calls.Count == expected.Count, "电力目标调用种类改变。");
        foreach (var pair in expected) Check(calls.GetValueOrDefault(pair.Key) == pair.Value, "电力目标调用次数改变：" + pair.Key);
    }

    private sealed class SignatureNames : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode code) => code.ToString();
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte kind) => reader.GetString(reader.GetTypeDefinition(handle).Name).Split('`')[0];
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte kind) => reader.GetString(reader.GetTypeReference(handle).Name).Split('`')[0];
        public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte kind) => reader.GetTypeSpecification(handle).DecodeSignature(this, context);
        public string GetGenericInstantiation(string type, ImmutableArray<string> arguments) => type + "<" + string.Join(",", arguments) + ">";
        public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? context, int index) => "!" + index;
        public string GetByReferenceType(string type) => type + "&";
        public string GetSZArrayType(string type) => type + "[]";
        public string GetArrayType(string type, ArrayShape shape) => type + "[,]";
        public string GetPointerType(string type) => type + "*";
        public string GetPinnedType(string type) => type;
        public string GetModifiedType(string modifier, string type, bool required) => type;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    }
}
