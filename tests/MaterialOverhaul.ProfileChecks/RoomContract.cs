using System.Reflection;
using System.Reflection.Emit;
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
        Method("Workable", "GetEfficiencyMultiplier", "Single(WorkerBase)");
        Method("ComplexFabricator", "ComputeWorkProgress", "Single(Single,ComplexRecipe)");
        Method("ElementConverter", "GetSpeedMultiplier", "Single()");
        Method("LegacyModMain", "ConfigElements", "Void()");
        Method("Clinic+ClinicSM+Instance", "StartEffect", "EffectInstance(String,Boolean)");
        Method("Clinic", "OnStopWork", "Void(WorkerBase)");
        Method("RoomProber", "ClearRoom", "Void(Room)");
        Method("Workable", "OnUpdateRoom", "Void(Object)");
        Method("MingleCellTracker", "Sim1000ms", "Void(Single)");
        Method("CavityInfo", "OnEnter", "Void(Object)");
        Method("ModifierSet", "CreateTrait", "Trait(String,String,String,String,Boolean,ChoreGroup[],Boolean,Boolean)");
        Method("RoomTracker", "IsInCorrectRoom", "Boolean()");
        Method("RoomTracker", "OnUpdateRoom", "Void(Object)");
        Method("Game", "OnCleanUp", "Void()");

        // §4.9 剩余房间效果的补丁目标。
        Method("Assignables", "AutoAssignSlot", "Assignable(AssignableSlot)");
        Method("MissionControlWorkable", "OnCompleteWork", "Void(WorkerBase)");
        Method("MissionControlWorkable", "get_TargetSpacecraft", "Spacecraft()");
        Method("Spacecraft", "ProgressMission", "Void(Single)");
        Method("MissionControlClusterWorkable", "OnCompleteWork", "Void(WorkerBase)");
        Method("MissionControlClusterWorkable", "OnWorkTick", "Boolean(WorkerBase,Single)");
        Method("MissionControlClusterWorkable", "IsRocketInRange", "Boolean(AxialI,AxialI)");
        Method("MissionControlClusterWorkable", "get_TargetClustercraft", "Clustercraft()");
        Method("MissionControlCluster+Instance", "UpdateWorkableRocketsInRange", "Void(Object)");
        Method("MissionControlCluster+Instance", "CanBeBoosted", "Boolean(Clustercraft)");
        Method("MissionControlCluster+Instance", "IsOwnWorld", "Boolean(Clustercraft)");
        Method("Clustercraft", "get_Speed", "Single()");
        Method("Clustercraft", "get_FuelPerDistance", "Single()");
        Method("ClustercraftConfig", "CreatePrefab", "GameObject()");
        Method("CraftModuleInterface", "DoLaunch", "Void()");
        Method("CraftModuleInterface", "get_CurrentPad", "LaunchPad()");
        Method("CraftModuleInterface", "get_Range", "Single()");
        Method("CraftModuleInterface", "get_MaxRange", "Int32()");
        Method("CraftModuleInterface", "get_FuelPerHex", "Single()");
        Method("RocketModulePerformance", "get_FuelKilogramPerDistance", "Single()");

        // 航程显示转接补丁：三个属性各恰有 1 处读取每格耗量，否则运行期保留原版指令。
        foreach (string getter in new[] { "get_Range", "get_MaxRange", "get_FuelPerHex" })
        {
            var type = reader.GetTypeDefinition(GetTypeHandle("CraftModuleInterface"));
            MethodDefinition method = type.GetMethods().Select(reader.GetMethodDefinition)
                .Single(m => reader.GetString(m.Name) == getter);
            int calls = CountCalls(pe, reader, method, "RocketModulePerformance", "get_FuelKilogramPerDistance");
            Check(calls == 1, "CraftModuleInterface." + getter + " 读取每格耗量 " + calls + " 处，期望 1 处");
        }
        Method("Clustercraft", "SetCraftStatus", "Void(CraftStatus)");
        Method("Clustercraft", "BurnFromTank", "Single(Single,RocketEngineCluster,Tag,IStorage,Single&)");
        Method("GeoTuner", "ApplyTuning", "Void(Instance)");
        Method("GeoTuner", "RemoveTuning", "Void(Instance)");
        Method("GeoTunerSideScreen", "SetRow", "Void(Int32,String,Sprite,Geyser,Boolean)");
        Method("GeoTunerSideScreen", "RefreshOptions", "Void(Object)");
        Method("RailGun", "LaunchProjectile", "Void()");
        Method("RailGun", "Sim200ms", "Void(Single)");
        Method("RailGunPayload+StatesInstance", "Launch", "Void(AxialI,AxialI)");
        Method("BallisticClusterGridEntity", "GetSpeed", "Single()");

        // 反射追加条件／效果使用的私有自动属性字段。
        Field("RoomType", "<additional_constraints>k__BackingField", "Constraint[]");
        Field("RoomType", "<effects>k__BackingField", "String[]");
        Field("RoomType", "<display_details>k__BackingField", "Detail[]");
        Field("RoomTracker", "statusItemGuid", "Guid");
        Field("Spacecraft", "missionElapsed", "Single");
        Field("MissionControlCluster+Instance", "boostableClustercraft", "List<Clustercraft>");
        Field("GeoTunerSideScreen", "targetGeotuner", "Instance");
        Field("Workable", "useLaboratoryEfficiencyBonus", "Boolean");
        Field("Workable", "currentlyInLaboratory", "Boolean");
        Field("Workable", "laboratoryEfficiencyBonusStatusItemHandle", "Guid");

        // 房间效果引用的原版类型。
        Check(types.ContainsKey("Klei.AI.Effect"), "原版类型缺失：Klei.AI.Effect");
        Check(types.ContainsKey("Klei.AI.AttributeModifier"), "原版类型缺失：Klei.AI.AttributeModifier");
        Check(types.ContainsKey("Klei.AI.Effects"), "原版类型缺失：Klei.AI.Effects");
    }

    /// <summary>统计方法 IL 中对指定同程序集方法的调用次数（只读元数据，不加载程序集）。</summary>
    private static int CountCalls(PEReader pe, MetadataReader reader, MethodDefinition method, string ownerName, string methodName)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
            .ToDictionary(op => unchecked((ushort)op.Value));
        byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
        int count = 0;
        for (int offset = 0; offset < il.Length;)
        {
            ushort value = il[offset++];
            if (value == 0xfe)
            {
                value = (ushort)(0xfe00 | il[offset++]);
            }

            OpCode opcode = opcodes[value];
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                EntityHandle handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, offset));
                if (handle.Kind == HandleKind.MethodDefinition)
                {
                    MethodDefinition callee = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    if (reader.GetString(callee.Name) == methodName
                        && reader.GetString(reader.GetTypeDefinition(callee.GetDeclaringType()).Name) == ownerName)
                    {
                        count++;
                    }
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

        return count;
    }
}
