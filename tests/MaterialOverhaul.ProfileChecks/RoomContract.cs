using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static partial class Program
{
    // 只读目标程序集元数据，核对房间补丁与反射访问的成员在当前游戏版本仍存在。
    // 不加载游戏程序集，也不替代游戏内行为验收。
    private static void InspectRoomGameContract(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var provider = new SignatureNames();
        var types = new Dictionary<string, TypeDefinitionHandle>(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
        {
            string name = TypeName(reader, handle);
            if (name.StartsWith(".", StringComparison.Ordinal))
            {
                name = name.Substring(1);
            }

            types[name] = handle;
        }

        TypeDefinitionHandle GetTypeHandle(string name)
        {
            Check(types.TryGetValue(name, out TypeDefinitionHandle handle), "原版类型缺失：" + name);
            return handle;
        }

        void Method(string typeName, string methodName, string expectedSignature)
        {
            var type = reader.GetTypeDefinition(GetTypeHandle(typeName));
            var matches = type.GetMethods().Select(reader.GetMethodDefinition)
                .Where(m => reader.GetString(m.Name) == methodName)
                .Select(m => m.DecodeSignature(provider, (object?)null))
                .Select(s => s.ReturnType + "(" + string.Join(",", s.ParameterTypes) + ")")
                .ToList();
            Check(matches.Count > 0, typeName + "." + methodName + " 缺失");
            Check(matches.Contains(expectedSignature),
                typeName + "." + methodName + " 签名改变，期望 " + expectedSignature + "，实际 " + string.Join(" / ", matches));
        }

        void Field(string typeName, string fieldName, string expectedSignature)
        {
            var type = reader.GetTypeDefinition(GetTypeHandle(typeName));
            var found = false;
            var signature = string.Empty;
            foreach (FieldDefinitionHandle handle in type.GetFields())
            {
                FieldDefinition field = reader.GetFieldDefinition(handle);
                if (reader.GetString(field.Name) != fieldName)
                {
                    continue;
                }

                found = true;
                signature = field.DecodeSignature(provider, (object?)null);
                break;
            }

            Check(found, typeName + "." + fieldName + " 字段缺失");
            Check(signature == expectedSignature,
                typeName + "." + fieldName + " 类型改变，期望 " + expectedSignature + "，实际 " + signature);
        }

        // 补丁目标。
        Method("Db", "Initialize", "Void()");
        Method("Database.RoomTypes", "GetRoomType", "RoomType(Room)");
        Method("Database.RoomTypes", "HasAmbiguousRoomType", "Boolean(Room,RoomType,RoomType)");
        Method("RoomType", "isSatisfactory", "RoomIdentificationResult(Room)");
        Method("RoomProber", "Refresh", "Void()");
        Method("RoomProber", "Sim1000ms", "Void(Single)");
        Method("RoomProber", "UpdateRoom", "Void(CavityInfo)");
        Method("RoomProber", "GetRoomOfGameObject", "Room(GameObject)");
        Method("RoomConstraints", "RoomCriteriaString", "String(Room)");
        Method("RoomDetails", "RoomDetailString", "String(Room)");
        Method("Workable", "CompleteWork", "Void(WorkerBase)");

        // 反射追加条件／效果使用的私有自动属性字段。
        Field("RoomType", "<additional_constraints>k__BackingField", "Constraint[]");
        Field("RoomType", "<effects>k__BackingField", "String[]");

        // 房间效果引用的原版类型。
        Check(types.ContainsKey("Klei.AI.Effect"), "原版类型缺失：Klei.AI.Effect");
        Check(types.ContainsKey("Klei.AI.AttributeModifier"), "原版类型缺失：Klei.AI.AttributeModifier");
        Check(types.ContainsKey("Klei.AI.Effects"), "原版类型缺失：Klei.AI.Effects");
    }
}
