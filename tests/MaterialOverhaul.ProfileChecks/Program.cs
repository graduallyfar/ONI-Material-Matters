using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// 只读PE元数据，绝不加载Unity或游戏程序集，也不以替身代码证明隔离。
internal static partial class Program
{
    private static int checks;
    private static readonly HashSet<string> PowerPatches = new(StringComparer.Ordinal)
    {
        "MaterialOverhaul.Patches.Patch_PowerTopology_Initialize",
        "MaterialOverhaul.Patches.Patch_PowerTopology_CleanUp",
        "MaterialOverhaul.Patches.Patch_PowerTopology_Rebuild",
        "MaterialOverhaul.Patches.Patch_PowerInfoCard_WireCount",
        "MaterialOverhaul.Patches.Patch_PowerSettlement_Sim"
    };
    private static readonly HashSet<string> RoomPatches = new(StringComparer.Ordinal)
    {
        "MaterialOverhaul.Patches.Patch_Room_Db_Initialize",
        "MaterialOverhaul.Patches.Patch_RoomTypes_GetRoomType",
        "MaterialOverhaul.Patches.Patch_RoomType_IsSatisfactory",
        "MaterialOverhaul.Patches.Patch_RoomProber_Refresh",
        "MaterialOverhaul.Patches.Patch_RoomProber_Sim1000ms",
        "MaterialOverhaul.Patches.Patch_RoomDetails_RoomDetailString",
        "MaterialOverhaul.Patches.Patch_Workable_CompleteWork",
        "MaterialOverhaul.Patches.Patch_Room_Workable_GetEfficiencyMultiplier",
        "MaterialOverhaul.Patches.Patch_Room_ComplexFabricator_ComputeWorkProgress",
        "MaterialOverhaul.Patches.Patch_Room_ElementConverter_GetSpeedMultiplier",
        "MaterialOverhaul.Patches.Patch_RoomMaterial_ConfigElements",
        "MaterialOverhaul.Patches.Patch_Room_Clinic_StartEffect",
        "MaterialOverhaul.Patches.Patch_Room_Clinic_OnStopWork",
        "MaterialOverhaul.Patches.Patch_Room_RoomProber_ClearRoom",
        "MaterialOverhaul.Patches.Patch_Room_RoomProber_CreateRoom",
        "MaterialOverhaul.Patches.Patch_Room_RoomProber_UpdateRoom",
        "MaterialOverhaul.Patches.Patch_Room_Workable_OnUpdateRoom",
        "MaterialOverhaul.Patches.Patch_Room_MingleCellTracker_Sim1000ms",
        "MaterialOverhaul.Patches.Patch_Room_Caregiver_CompleteWork",
        "MaterialOverhaul.Patches.Patch_Room_CavityInfo_OnEnter",
        "MaterialOverhaul.Patches.Patch_Room_RoomTracker_IsInCorrectRoom",
        "MaterialOverhaul.Patches.Patch_Room_RoomTracker_OnUpdateRoom",
        "MaterialOverhaul.Patches.Patch_RoomAdvanced_Game_OnCleanUp",
        "MaterialOverhaul.Patches.Patch_Room_Assignables_AutoAssignSlot",
        "MaterialOverhaul.Patches.Patch_Room_MissionControl_OnCompleteWork",
        "MaterialOverhaul.Patches.Patch_Room_Spacecraft_ProgressMission",
        "MaterialOverhaul.Patches.Patch_Room_MissionControlCluster_OnCompleteWork",
        "MaterialOverhaul.Patches.Patch_Room_Clustercraft_Speed",
        "MaterialOverhaul.Patches.Patch_Room_MissionControlCluster_UpdateRange",
        "MaterialOverhaul.Patches.Patch_Room_MissionControlCluster_OnWorkTick",
        "MaterialOverhaul.Patches.Patch_Room_MissionControlCluster_IsRocketInRange",
        "MaterialOverhaul.Patches.Patch_Room_GeoTunerResearch_CompleteWork",
        "MaterialOverhaul.Patches.Patch_Room_GeoTuner_ApplyTuning",
        "MaterialOverhaul.Patches.Patch_Room_GeoTuner_RemoveTuning",
        "MaterialOverhaul.Patches.Patch_Room_GeoTuner_InitializeStates",
        "MaterialOverhaul.Patches.Patch_Room_GeoTunerSideScreen_SetRow",
        "MaterialOverhaul.Patches.Patch_Room_ClustercraftConfig_CreatePrefab",
        "MaterialOverhaul.Patches.Patch_Room_CraftModuleInterface_DoLaunch",
        "MaterialOverhaul.Patches.Patch_Room_Clustercraft_SetCraftStatus",
        "MaterialOverhaul.Patches.Patch_Room_Clustercraft_FuelPerDistance",
        "MaterialOverhaul.Patches.Patch_Room_Clustercraft_BurnFromTank",
        "MaterialOverhaul.Patches.Patch_Room_CraftModuleInterface_FuelRange",
        "MaterialOverhaul.Patches.Patch_Room_RailGun_LaunchProjectile",
        "MaterialOverhaul.Patches.Patch_Room_RailGunPayload_Launch",
        "MaterialOverhaul.Patches.Patch_Room_BallisticClusterGridEntity_GetSpeed",
        "MaterialOverhaul.Patches.Patch_Room_RailGun_Sim200ms"
    };
    private static readonly HashSet<string> RadiationPatches = new(StringComparer.Ordinal)
    {
        "MaterialOverhaul.Patches.Patch_Radiation_Db_Initialize",
        "MaterialOverhaul.Patches.Patch_Radiation_BuildingComplete",
        "MaterialOverhaul.Patches.Patch_Radiation_RoomProber_Sim1000ms",
        "MaterialOverhaul.Patches.Patch_Radiation_Game_OnCleanUp"
    };
    private sealed record Snapshot(HashSet<string> Types, HashSet<string> Patches);

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidDataException(message);
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--game")
            {
                InspectGameContract(args[1]);
                Console.WriteLine($"原版电力IL调用契约 {checks} 项，全部通过。");
                return 0;
            }
            if (args.Length == 2 && args[0] == "--room-game")
            {
                InspectRoomGameContract(args[1]);
                Console.WriteLine($"原版房间补丁与反射字段契约 {checks} 项，全部通过。");
                return 0;
            }
            Check(args.Length > 0 && args.Length % 2 == 0, "用法：ProfileChecks <Industrial|Power|Room|Combined|Radiation> <包目录> [更多模式与目录]");
            var profiles = new Dictionary<string, Snapshot>();
            for (int i = 0; i < args.Length; i += 2) profiles.Add(args[i], Inspect(args[i], args[i + 1]));
            if (profiles.TryGetValue("Combined", out var combined)
                && profiles.TryGetValue("Industrial", out var industrial)
                && profiles.TryGetValue("Power", out var power)
                && profiles.TryGetValue("Room", out var room))
            {
                var union = new HashSet<string>(industrial.Patches);
                union.UnionWith(power.Patches);
                union.UnionWith(room.Patches);
                Check(union.SetEquals(combined.Patches), "完整整合版补丁必须恰好等于三个独立版补丁的并集。");
                Check(!industrial.Patches.Overlaps(power.Patches), "工业与电力独立版不得包含同一个功能补丁。");
                Check(!industrial.Patches.Overlaps(room.Patches), "工业与房间独立版不得包含同一个功能补丁。");
                Check(!power.Patches.Overlaps(room.Patches), "电力与房间独立版不得包含同一个功能补丁。");
            }
            Console.WriteLine($"隔离程序集与验证包检查 {checks} 项，全部通过。");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("[失败] " + e.Message); return 1; }
    }

    private static Snapshot Inspect(string profile, string directory)
    {
        Check(profile is "Industrial" or "Power" or "Room" or "Combined" or "Radiation", "未知构建模式：" + profile);
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "build_profile.json")));
        JsonElement root = manifest.RootElement;
        string expectedVersion = profile switch { "Power" => "0.0.2", "Combined" => "1.0.1", "Radiation" => "0.0.1", _ => "0.2.2" };
        Check(root.GetProperty("version").GetString() == expectedVersion, "包版本与系统专属版本不符。");
        Check(File.ReadAllText(Path.Combine(directory, "mod_info.yaml")).Contains("version: \"" + expectedVersion + "\""), "模组元数据版本不符。");
        Check(root.GetProperty("profile").GetString() == profile, "包的模式与请求模式不符。");
        Check(root.GetProperty("industrialEnabled").GetBoolean() == (profile is "Industrial" or "Combined"), "清单工业开关不符。");
        Check(root.GetProperty("powerEnabled").GetBoolean() == (profile is "Power" or "Combined"), "清单电力开关不符。");
        Check(root.GetProperty("roomEnabled").GetBoolean() == (profile is "Room" or "Combined"), "清单房间开关不符。");
        // 辐射系统尚未整合：只有辐射独立版开启。
        Check(root.GetProperty("radiationEnabled").GetBoolean() == (profile == "Radiation"), "清单辐射开关不符。");
        foreach (JsonProperty file in root.GetProperty("files").EnumerateObject())
        {
            string path = Path.GetFullPath(Path.Combine(directory, file.Name));
            Check(path.StartsWith(Path.GetFullPath(directory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "清单文件越出包目录。");
            Check(File.Exists(path), "验证包缺少文件：" + file.Name);
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) == file.Value.GetString(), "验证包文件哈希不符：" + file.Name);
        }
        foreach (string required in new[] { "MaterialOverhaul.dll", "MaterialOverhaul.pdb", "mod.yaml", "mod_info.yaml", "strings/zh.po" })
            Check(root.GetProperty("files").TryGetProperty(required, out _), "清单缺少必要文件：" + required);
        Check(Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories).Select(Path.GetFileName)
            .SequenceEqual(new[] { "MaterialOverhaul.dll" }), "验证包只能包含Mod本体DLL。");
        string modYaml = File.ReadAllText(Path.Combine(directory, "mod.yaml"));
        Check(modYaml.Contains("staticID: \"MaterialOverhaul\"", StringComparison.Ordinal), "所有模式必须使用同一个Mod ID，互相替换安装。");

        using var stream = File.OpenRead(Path.Combine(directory, "MaterialOverhaul.dll"));
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        Check(metadata.GetAssemblyDefinition().Version == Version.Parse(expectedVersion + ".0"), "程序集版本与系统专属版本不符。");
        string? embeddedProfile = null;
        string? fileVersion = null, productVersion = null;
        foreach (CustomAttributeHandle handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            string attributeType = AttributeType(metadata, attribute);
            if (attributeType is not ("System.Reflection.AssemblyMetadataAttribute" or "System.Reflection.AssemblyFileVersionAttribute" or "System.Reflection.AssemblyInformationalVersionAttribute")) continue;
            var blob = metadata.GetBlobReader(attribute.Value); blob.ReadUInt16();
            string? key = blob.ReadSerializedString();
            if (attributeType == "System.Reflection.AssemblyFileVersionAttribute") fileVersion = key;
            else if (attributeType == "System.Reflection.AssemblyInformationalVersionAttribute") productVersion = key;
            else if (key == "MaterialOverhaul.SystemProfile") embeddedProfile = blob.ReadSerializedString();
        }
        Check(embeddedProfile == profile, "DLL内嵌模式与验证包不符。");
        Check(fileVersion == expectedVersion + ".0", "DLL文件版本不符。");
        Check(productVersion == expectedVersion || productVersion?.StartsWith(expectedVersion + "+", StringComparison.Ordinal) == true, "DLL产品版本不符。");
        var types = new HashSet<string>(StringComparer.Ordinal);
        var patches = new HashSet<string>(StringComparer.Ordinal);
        var textKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string name = TypeName(metadata, handle); types.Add(name);
            if (name == "MaterialOverhaul.Core.ModInfo")
            {
                var versionField = type.GetFields().Select(metadata.GetFieldDefinition).Single(f => metadata.GetString(f.Name) == "Version");
                Check(Encoding.Unicode.GetString(metadata.GetBlobBytes(metadata.GetConstant(versionField.GetDefaultValue()).Value)) == expectedVersion, "启动日志版本与包版本不一致。");
            }
            foreach (CustomAttributeHandle attr in type.GetCustomAttributes())
                if (AttributeType(metadata, metadata.GetCustomAttribute(attr)) == "HarmonyLib.HarmonyPatch") patches.Add(name);
            if (name == "MaterialOverhaul.Core.BuildProfile")
            {
                foreach (FieldDefinitionHandle fieldHandle in type.GetFields())
                {
                    FieldDefinition field = metadata.GetFieldDefinition(fieldHandle);
                    var constantHandle = field.GetDefaultValue();
                    if (constantHandle.IsNil) continue;
                    byte[] bytes = metadata.GetBlobBytes(metadata.GetConstant(constantHandle).Value);
                    switch (metadata.GetString(field.Name))
                    {
                        case "Name": Check(Encoding.Unicode.GetString(bytes) == profile, "入口构建名称不符。"); break;
                        case "IndustrialEnabled": Check((bytes[0] != 0) == (profile is "Industrial" or "Combined"), "DLL工业开关不符。"); break;
                        case "PowerEnabled": Check((bytes[0] != 0) == (profile is "Power" or "Combined"), "DLL电力开关不符。"); break;
                        case "RoomEnabled": Check((bytes[0] != 0) == (profile is "Room" or "Combined"), "DLL房间开关不符。"); break;
                        case "RadiationEnabled": Check((bytes[0] != 0) == (profile == "Radiation"), "DLL辐射开关不符。"); break;
                    }
                }
            }
            const string textPrefix = "MaterialOverhaul.Game.ModStrings+";
            const string roomTextPrefix = "MaterialOverhaul.Game.RoomStrings+";
            const string radiationTextPrefix = "MaterialOverhaul.Game.RadiationStrings+";
            if (name.StartsWith(textPrefix, StringComparison.Ordinal))
                foreach (FieldDefinitionHandle field in type.GetFields())
                    textKeys.Add("STRINGS." + name[textPrefix.Length..].Replace('+', '.').ToUpperInvariant()
                        + "." + metadata.GetString(metadata.GetFieldDefinition(field).Name).ToUpperInvariant());
            if (name.StartsWith(roomTextPrefix, StringComparison.Ordinal))
                foreach (FieldDefinitionHandle field in type.GetFields())
                    textKeys.Add("STRINGS." + name[roomTextPrefix.Length..].Replace('+', '.').ToUpperInvariant()
                        + "." + metadata.GetString(metadata.GetFieldDefinition(field).Name).ToUpperInvariant());
            if (name.StartsWith(radiationTextPrefix, StringComparison.Ordinal))
                foreach (FieldDefinitionHandle field in type.GetFields())
                    textKeys.Add("STRINGS." + name[radiationTextPrefix.Length..].Replace('+', '.').ToUpperInvariant()
                        + "." + metadata.GetString(metadata.GetFieldDefinition(field).Name).ToUpperInvariant());
        }
        Check(types.Contains("MaterialOverhaul.Core.BuildProfile"), "缺少构建身份。");
        if (profile != "Radiation")
        {
            Check(!patches.Overlaps(RadiationPatches), profile + "包含辐射补丁（辐射系统尚未整合）。");
            Check(!types.Any(t => t.StartsWith("MaterialOverhaul.Radiation.", StringComparison.Ordinal)
                || t.Split('+')[0].StartsWith("MaterialOverhaul.Game.Radiation", StringComparison.Ordinal)), profile + "包含辐射源码（辐射系统尚未整合）。");
        }
        if (profile == "Radiation")
        {
            Check(patches.SetEquals(RadiationPatches), "辐射独立版只应包含明确列出的4个辐射补丁。");
            foreach (string forbidden in new[] { "MaterialOverhaul.Zones.", "MaterialOverhaul.Heating.", "MaterialOverhaul.Hardness.", "MaterialOverhaul.Orders.", "MaterialOverhaul.Power.", "MaterialOverhaul.Rooms." })
                Check(!types.Any(t => t.StartsWith(forbidden, StringComparison.Ordinal)), "辐射独立版混入其他系统判断层：" + forbidden);
            foreach (string forbidden in new[] { "MaterialOverhaulFab", "MaterialRegistration", "ContentRegistration", "PowerRegistry", "DebugValues", "BuildingWhitelist", "PowerTopologyMonitor", "PowerWireCount", "RoomRegistration" })
                Check(!types.Any(t => t.Split('+')[0].EndsWith("." + forbidden, StringComparison.Ordinal)), "辐射独立版混入其他系统组件：" + forbidden);
            Check(types.Contains("MaterialOverhaul.Game.RadiationActivation"), "辐射独立版缺少建筑活化组件。");
        }
        else if (profile == "Power")
        {
            Check(patches.SetEquals(PowerPatches), "电力独立版只应包含明确的5个电力补丁。");
            foreach (string forbidden in new[] { "MaterialOverhaul.Zones.", "MaterialOverhaul.Heating.", "MaterialOverhaul.Hardness.", "MaterialOverhaul.Orders.", "MaterialOverhaul.Rooms." })
                Check(!types.Any(t => t.StartsWith(forbidden, StringComparison.Ordinal)), "电力独立版混入其他判断层：" + forbidden);
            foreach (string forbidden in new[] { "MaterialOverhaulFab", "MaterialRegistration", "ContentRegistration", "PowerRegistry", "DebugValues", "BuildingWhitelist" })
                Check(!types.Any(t => t.Split('+')[0].EndsWith("." + forbidden, StringComparison.Ordinal)), "电力独立版混入工业组件：" + forbidden);
        }
        else if (profile == "Room")
        {
            Check(patches.SetEquals(RoomPatches), "房间独立版只应包含明确列出的房间补丁。");
            foreach (string forbidden in new[] { "MaterialOverhaul.Zones.", "MaterialOverhaul.Heating.", "MaterialOverhaul.Hardness.", "MaterialOverhaul.Orders.", "MaterialOverhaul.Power." })
                Check(!types.Any(t => t.StartsWith(forbidden, StringComparison.Ordinal)), "房间独立版混入其他系统判断层：" + forbidden);
            foreach (string forbidden in new[] { "MaterialOverhaulFab", "MaterialRegistration", "ContentRegistration", "PowerRegistry", "DebugValues", "BuildingWhitelist", "PowerTopologyMonitor", "PowerWireCount" })
                Check(!types.Any(t => t.Split('+')[0].EndsWith("." + forbidden, StringComparison.Ordinal)), "房间独立版混入其他系统组件：" + forbidden);
        }
        else
        {
            foreach (string required in new[] { "Patch_Db_Initialize", "Patch_ElementLoader_Load", "Patch_LegacyModMain_ConfigElements", "Patch_IndustrialGeneration", "Patch_EnergyConsumer_WattsUsed", "Patch_ComplexFabricator_UpdateChore" })
                Check(patches.Contains("MaterialOverhaul.Patches." + required), "工业功能缺少补丁：" + required);
            Check(types.Contains("MaterialOverhaul.Game.MaterialOverhaulFab"), "工业版缺少建筑组件。");
            if (profile == "Industrial")
            {
                Check(!patches.Overlaps(PowerPatches), "工业独立版包含电力补丁。");
                Check(!patches.Overlaps(RoomPatches), "工业独立版包含房间补丁。");
                Check(!types.Any(t => t.StartsWith("MaterialOverhaul.Power.", StringComparison.Ordinal)), "工业独立版包含电力计算层。");
                Check(!types.Any(t => t.StartsWith("MaterialOverhaul.Rooms.", StringComparison.Ordinal)), "工业独立版包含房间判断层。");
                Check(!types.Contains("MaterialOverhaul.Game.PowerTopologyMonitor") && !types.Contains("MaterialOverhaul.Game.PowerWireCount"), "工业独立版包含电力服务。");
                Check(!types.Contains("MaterialOverhaul.Game.RoomRegistration"), "工业独立版包含房间服务。");
            }
            else
            {
                Check(PowerPatches.IsSubsetOf(patches), "完整整合版缺少电力补丁。");
                Check(RoomPatches.IsSubsetOf(patches), "完整整合版缺少房间补丁。");
            }
        }
        int expectedKeys = profile == "Power" ? 14 : profile == "Industrial" ? 89 : profile == "Room" ? 163 : profile == "Radiation" ? 2 : 266;
        var poKeys = File.ReadLines(Path.Combine(directory, "strings", "zh.po"))
            .Select(line => Regex.Match(line, "^msgctxt \"([^\"]+)\"$"))
            .Where(match => match.Success).Select(match => match.Groups[1].Value).ToList();
        Check(textKeys.Count == expectedKeys, "DLL本地化字段数不符。");
        Check(poKeys.Count == expectedKeys && textKeys.SetEquals(poKeys), "PO与此模式的真实字段不一致。");
        Console.WriteLine($"{profile}: {patches.Count}个补丁，{textKeys.Count}条文本；DLL与包一致。");
        return new Snapshot(types, patches);
    }

    private static string TypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        var parent = type.GetDeclaringType();
        return parent.IsNil ? reader.GetString(type.Namespace) + "." + reader.GetString(type.Name)
            : TypeName(reader, parent) + "+" + reader.GetString(type.Name);
    }
    private static string AttributeType(MetadataReader reader, CustomAttribute attribute)
    {
        EntityHandle typeHandle = attribute.Constructor.Kind == HandleKind.MemberReference
            ? reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent
            : reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
        if (typeHandle.Kind == HandleKind.TypeDefinition) return TypeName(reader, (TypeDefinitionHandle)typeHandle);
        if (typeHandle.Kind != HandleKind.TypeReference) return string.Empty;
        var type = reader.GetTypeReference((TypeReferenceHandle)typeHandle);
        return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
    }
}
