using System.Collections.Immutable;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using TerrariaSplit.Compatibility;
using TerrariaSplit.MemoryBridge.Payload;
using TerrariaSplit.Terraria.Memory;

namespace TerrariaSplit.Tests;

internal static class Terraria1461CompatibilityTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return TestCase.Sync("UI scale profiles preserve 1458 and apply 1461 atomically and idempotently", TestSuite.Core, ScaleProfileJourney);
        yield return TestCase.Sync("native1461 validates hooks luck call sites menu memory schema and scale against the installed game", TestSuite.Native, NativeCompatibilityJourney);
    }

    private static void ScaleProfileJourney()
    {
        (TerrariaImplementation Implementation, string[] Patterns)[] fixtures =
        [
            (TerrariaImplementation.UiScale145, ["7E5A0D0004220000003F59220000C03F5B2200000000220000803F28D800000A161428CC010006", "112C220000C03F5A220000003F58805A0D0004", "283F0C0006220000003F220000004028181700067B73110004223333B33E282F16000628410C0006"]),
            (TerrariaImplementation.UiScale146, ["7E910D0004220000003F59220000C03F5B2200000000220000803F285101000A161428FE010006", "112C220000C03F5A220000003F5880910D0004", "288E0C0006220000003F220000004028761700067BAC110004223333B33E288D16000628900C0006"])
        ];
        foreach (var fixture in fixtures)
        {
            byte[][] patterns = fixture.Patterns.Select(Convert.FromHexString).ToArray();
            byte[] image = patterns.SelectMany(pattern => pattern.Concat(new byte[32])).ToArray();
            IntPtr baseAddress = new(0x10000000);
            TerrariaUiScalePatchPlan plan = TerrariaUiScalePatch.CreatePlan(image, baseAddress, fixture.Implementation);
            Check.True(plan.CanApply);
            Check.False(plan.AlreadyApplied);
            Check.Equal(3, plan.Writes.Count);
            foreach (PatchWrite write in plan.Writes)
            {
                Check.True(write.Address.ToInt64() >= baseAddress.ToInt64());
                Check.Equal(4, write.Bytes.Length);
            }
            byte[] patched = TerrariaUiScalePatch.ApplyToBufferForTest(image, fixture.Implementation);
            TerrariaUiScalePatchPlan again = TerrariaUiScalePatch.CreatePlan(patched, IntPtr.Zero, fixture.Implementation);
            Check.True(again.CanApply);
            Check.True(again.AlreadyApplied);
            Check.Equal(0, again.Writes.Count);
            Check.True(TerrariaUiScalePatch.ApplyToBufferForTest(patched, fixture.Implementation).SequenceEqual(patched));
            TerrariaImplementation other = fixture.Implementation == TerrariaImplementation.UiScale145
                ? TerrariaImplementation.UiScale146 : TerrariaImplementation.UiScale145;
            Check.False(TerrariaUiScalePatch.CreatePlan(image, IntPtr.Zero, other).CanApply);
            foreach (byte[] invalid in new[] { patterns[0], image.Concat(patterns[0]).ToArray() })
            {
                TerrariaUiScalePatchPlan rejected = TerrariaUiScalePatch.CreatePlan(invalid, IntPtr.Zero, fixture.Implementation);
                Check.False(rejected.CanApply);
                Check.Equal(0, rejected.Writes.Count);
            }
        }
        Check.False(TerrariaUiScalePatch.CreatePlan([], IntPtr.Zero, TerrariaImplementation.None).CanApply);
    }

    private static void NativeCompatibilityJourney()
    {
        string binary = Environment.GetEnvironmentVariable("TERRARIA_SPLIT_TEST_TERRARIA_EXE") ??
            @"C:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe";
        if (!File.Exists(binary)) throw new FileNotFoundException("The native1461 audit requires Terraria 1.4.6.1.", binary);
        byte[] image = File.ReadAllBytes(binary);
        using var stream = new MemoryStream(image, writable: false);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();
        Guid mvid = metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
        Check.Equal(TerrariaBuilds.Race1461.Version, metadata.GetAssemblyDefinition().Version);
        Check.Equal(TerrariaBuilds.Race1461.ModuleVersionId, mvid);
        Check.Equal(Machine.I386, pe.PEHeaders.CoffHeader.Machine);
        var names = new TypeNames();
        var methods = new Dictionary<string, (int Token, byte[] Body)>(StringComparer.Ordinal);
        var byToken = new Dictionary<int, string>();
        var fields = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (TypeDefinitionHandle handle in metadata.TypeDefinitions)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string typeName = names.GetTypeFromDefinition(metadata, handle, 0);
            fields[typeName] = type.GetFields().Select(field => metadata.GetString(metadata.GetFieldDefinition(field).Name)).ToHashSet(StringComparer.Ordinal);
            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = metadata.GetMethodDefinition(methodHandle);
                MethodSignature<string> signature = method.DecodeSignature(names, (object?)null);
                string key = typeName + "." + metadata.GetString(method.Name) + "(" + string.Join(",", signature.ParameterTypes) + ")";
                int token = MetadataTokens.GetToken(methodHandle);
                byToken[token] = key;
                if (method.RelativeVirtualAddress != 0)
                    methods[key] = (token, pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!);
            }
        }
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestDirectory.FindSourceRoot(),
            "src", "TerrariaSplit.MemoryBridge.Payload", "terraria-compatibility.json")));
        JsonElement client = manifest.RootElement.GetProperty("supportedClients")[0];
        Check.Equal(mvid, Guid.Parse(client.GetProperty("moduleVersionId").GetString()!));
        int auditedCount = 0;
        foreach (JsonProperty entry in client.GetProperty("methods").EnumerateObject())
        {
            var method = methods[entry.Name];
            Check.Equal(entry.Value.GetString(), Convert.ToHexString(SHA256.HashData(method.Body)));
            Check.True(TerrariaHookAudit1461.MatchesMethod(method.Token, mvid, method.Body));
            Check.False(TerrariaHookAudit1461.MatchesMethod(method.Token, Guid.Empty, method.Body));
            byte[] changed = (byte[])method.Body.Clone();
            changed[0] ^= 1;
            Check.False(TerrariaHookAudit1461.MatchesMethod(method.Token, mvid, changed));
            auditedCount++;
        }
        Check.Equal(50, auditedCount);
        Check.False(TerrariaHookAudit1461.MatchesMethod(int.MaxValue, mvid, [0x2a]));
        var luckCalls = new List<string>();
        var naturalCalls = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pair in methods)
        {
            foreach (Instruction instruction in Decode(pair.Value.Body))
            {
                if (instruction.Opcode != OpCodes.Call && instruction.Opcode != OpCodes.Callvirt) continue;
                int token = BitConverter.ToInt32(pair.Value.Body, instruction.Operand);
                if (!byToken.TryGetValue(token, out string? target)) continue;
                if (target.StartsWith("Terraria.GameContent.Luck.Roll", StringComparison.Ordinal))
                    luckCalls.Add($"{pair.Value.Token:X8}@{instruction.Offset:X4}->{token:X8}");
                if (pair.Key == "Terraria.Main.UpdateTime()" &&
                    (target.StartsWith("Terraria.Main.StartRain(") || target.StartsWith("Terraria.Main.StartSlimeRain(") || target.StartsWith("Terraria.NPC.SpawnOnPlayer(")))
                    naturalCalls[target] = naturalCalls.GetValueOrDefault(target) + 1;
            }
        }
        Check.Sequence(TerrariaHookAudit1461.LuckCallSites, luckCalls.Order(StringComparer.Ordinal));
        Check.Equal(10, luckCalls.Count);
        Check.Equal(3, naturalCalls["Terraria.Main.StartRain(System.Boolean,System.Nullable{System.Single},System.Boolean)"]);
        Check.Equal(1, naturalCalls["Terraria.Main.StartSlimeRain(System.Boolean)"]);
        Check.Equal(7, naturalCalls["Terraria.NPC.SpawnOnPlayer(System.Int32,System.Int32,System.Single,System.Single,System.Single,System.Single)"]);
        // GetShimmered changed its locals in 1.4.6. Validate the actual replacement shape,
        // not just its fingerprint, before retaining the shared ingredient-local transpiler.
        byte[] shimmerBody = methods["Terraria.WorldItem.GetShimmered()"].Body;
        Instruction[] shimmer = Decode(shimmerBody).ToArray();
        int ingredientStores = 0, alchemyRolls = 0;
        for (int index = 0; index < shimmer.Length; index++)
        {
            Instruction instruction = shimmer[index];
            if (instruction.Opcode == OpCodes.Call || instruction.Opcode == OpCodes.Callvirt)
            {
                EntityHandle called = MetadataTokens.EntityHandle(BitConverter.ToInt32(shimmerBody, instruction.Operand));
                if (called.Kind == HandleKind.MemberReference)
                {
                    MemberReference member = metadata.GetMemberReference((MemberReferenceHandle)called);
                    if (metadata.GetString(member.Name) == "get_Current" && member.Parent.Kind == HandleKind.TypeSpecification &&
                        names.GetTypeFromSpecification(metadata, null, (TypeSpecificationHandle)member.Parent, 0).Contains("Terraria.Recipe+RequiredItemEntry", StringComparison.Ordinal) &&
                        index + 1 < shimmer.Length && shimmer[index + 1].Opcode.Name!.StartsWith("stloc", StringComparison.Ordinal))
                        ingredientStores++;
                }
            }
            if (instruction.Opcode == OpCodes.Ldsfld && index + 2 < shimmer.Length && shimmer[index + 1].Opcode == OpCodes.Ldc_I4_3 &&
                shimmer[index + 2].Opcode == OpCodes.Callvirt)
            {
                FieldDefinition field = metadata.GetFieldDefinition(MetadataTokens.FieldDefinitionHandle(
                    BitConverter.ToInt32(shimmerBody, instruction.Operand) & 0x00ffffff));
                if (metadata.GetString(field.Name) == "rand") alchemyRolls++;
            }
        }
        Check.True(ingredientStores > 0);
        Check.Equal(1, alchemyRolls);
        Check.True(methods.ContainsKey("Terraria.Main.ClearPendingPlayerSelectCallbacks()"));
        foreach (string key in new[]
        {
            "Terraria.NPC.CheckActive()", "Terraria.NPC.EncourageDespawn(System.Int32)",
            "Terraria.NPC.checkDead()", "Terraria.NPC.AI()", "Terraria.Main.DrawInterface_35_YouDied()",
            "Terraria.Player.Spawn(Terraria.PlayerSpawnContext)",
            "Terraria.Item.NewItem(Terraria.DataStructures.IEntitySource,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Int32,System.Boolean,System.Int32,Terraria.NewItemOwnership,System.Nullable{Microsoft.Xna.Framework.Vector2},Terraria.Item+NewItemModifier)"
        })
            if (!methods.ContainsKey(key)) throw new InvalidOperationException("Missing Boss penalty target: " + key);
        RequireFields(fields, "Terraria.Main", "gameMenu", "MenuUI", "player", "myPlayer", "npc", "mouseItem", "showSplash", "IsEngineLoaded");
        RequireFields(fields, "Terraria.Player", "inventory", "armor", "dye", "miscEquips", "miscDyes", "bank", "bank2", "bank3", "bank4",
            "zone1", "zone2", "zone3", "zone4", "zone5", "dead", "lastDeathPostion");
        RequireFields(fields, "Terraria.Item", "type", "stack");
        RequireFields(fields, "Terraria.NPC", "type", "active", "townNPC", "homeless", "homeTileX", "homeTileY", "activeTime", "lifeMax", "timeLeft");
        RequireFields(fields, "Terraria.Entity", "position", "width", "height");
        RequireFields(fields, "Terraria.GameContent.UI.States.UICharacterCreation", "_player", "_pasteTemplateButton");
        RequireFields(fields, "Terraria.GameContent.UI.States.UIWorldCreation", "_seedPlate", "_sizeButtons", "_difficultyButtons", "_evilButtons");
        RequireFields(fields, "Terraria.GameContent.UI.States.UIWorldCreationAdvanced", "_randomButton", "_seedPlate", "_seedButtons", "_backButton");
        RequireFields(fields, "Terraria.GameContent.UI.States.UIVirtualKeyboard", "_textBox", "_submitButton", "_submitButton2", "outerLayer1", "outerLayer2");
        TerrariaUiScalePatchPlan scale = TerrariaUiScalePatch.CreatePlan(image, IntPtr.Zero, TerrariaImplementation.UiScale146);
        Check.True(scale.CanApply);
        Check.Equal(3, scale.Writes.Count);
        byte[] patchedImage = TerrariaUiScalePatch.ApplyToBufferForTest(image, TerrariaImplementation.UiScale146);
        Check.True(TerrariaUiScalePatch.CreatePlan(patchedImage, IntPtr.Zero, TerrariaImplementation.UiScale146).AlreadyApplied);
        Check.False(TerrariaUiScalePatch.CreatePlan(image, IntPtr.Zero, TerrariaImplementation.UiScale145).CanApply);
    }

    private static void RequireFields(Dictionary<string, HashSet<string>> fields, string type, params string[] names)
    {
        foreach (string name in names)
            if (!fields.TryGetValue(type, out var available) || !available.Contains(name))
                throw new InvalidOperationException("Missing required Terraria 1461 field: " + type + "." + name);
    }

    private sealed record Instruction(int Offset, OpCode Opcode, int Operand);
    private static readonly Dictionary<ushort, OpCode> Opcodes = typeof(OpCodes).GetFields()
        .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(op => unchecked((ushort)op.Value));

    private static IEnumerable<Instruction> Decode(byte[] body)
    {
        int position = 0;
        while (position < body.Length)
        {
            int start = position;
            ushort value = body[position++];
            if (value == 0xfe) value = (ushort)(0xfe00 | body[position++]);
            OpCode opcode = Opcodes[value];
            int size = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + BitConverter.ToInt32(body, position) * 4,
                _ => 4
            };
            yield return new(start, opcode, position);
            position += size;
        }
    }

    private sealed class TypeNames : ISignatureTypeProvider<string, object?>
    {
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte kind)
        {
            TypeDefinition type = reader.GetTypeDefinition(handle);
            if (!type.GetDeclaringType().IsNil)
                return GetTypeFromDefinition(reader, type.GetDeclaringType(), kind) + "+" + reader.GetString(type.Name);
            string ns = reader.GetString(type.Namespace);
            return (ns.Length == 0 ? "" : ns + ".") + reader.GetString(type.Name);
        }
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte kind)
        {
            TypeReference type = reader.GetTypeReference(handle);
            return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        }
        public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte kind) => reader.GetTypeSpecification(handle).DecodeSignature(this, context);
        public string GetPrimitiveType(PrimitiveTypeCode code) => "System." + code;
        public string GetArrayType(string element, ArrayShape shape) => element + "[" + new string(',', shape.Rank - 1) + "]";
        public string GetByReferenceType(string element) => element + "&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
        public string GetGenericInstantiation(string type, ImmutableArray<string> args) => System.Text.RegularExpressions.Regex.Replace(type, "`[0-9]+$", "") + "{" + string.Join(",", args) + "}";
        public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? context, int index) => "!" + index;
        public string GetModifiedType(string modifier, string element, bool required) => element;
        public string GetPinnedType(string element) => element;
        public string GetPointerType(string element) => element + "*";
        public string GetSZArrayType(string element) => element + "[]";
    }
}
