using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Microsoft.Xna.Framework;
using Tka.Compatibility;

return AssemblyRetargeter.Run(args);

public static class AssemblyRetargeter
{
public static int Run(string[] args)
{
try
{
if (args.Length == 3 && args[0] == "--runtime") return RuntimeAdapter.Patch(args[1], args[2]);
if (args.Length != 2) throw new ArgumentException("Usage: Tka.AssemblyTool original.exe private-output.dll");
var input = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
if (File.Exists(output)) throw new IOException("Output already exists; refusing to overwrite.");
var expected = "12C05F08878273BB6BF9C006D379F81C81AAF07FCF32EB3AF2DCDBB1FAB6848D";
var data = File.ReadAllBytes(input);
if (Convert.ToHexString(SHA256.HashData(data)) != expected) throw new InvalidDataException("Unsupported original assembly hash.");
using var module = ModuleDefinition.ReadModule(new MemoryStream(data));
var framework = typeof(Game).Assembly;
var compatibility = typeof(LocalServices).Assembly;
// Resolve the actual desktop owner of each BCL type instead of assuming every
// compact-framework type belongs to the same modern assembly.
var bcl = new[] { typeof(object).Assembly, typeof(Uri).Assembly,
    typeof(IServiceProvider).Assembly, typeof(System.Xml.Serialization.XmlSerializer).Assembly,
    typeof(System.Collections.ObjectModel.Collection<>).Assembly };
var mappings = new List<object>();
foreach (var type in module.GetTypeReferences().ToArray())
{
    if (type.Scope is not AssemblyNameReference old) continue;
    if (old.Name == framework.GetName().Name || old.Name == compatibility.GetName().Name ||
        bcl.Any(a => a.GetName().Name == old.Name)) continue;
    Type? target;
    if (type.FullName == "Microsoft.Xna.Framework.Media.VisualizationData") target = typeof(XnaVisualizationData);
    else if (type.FullName == "Microsoft.Xna.Framework.Media.MediaPlayer") target = typeof(XnaMediaPlayer);
    else if (old.Name.StartsWith("Microsoft.Xna.Framework", StringComparison.Ordinal))
        target = compatibility.GetType(type.FullName) ?? framework.GetType(type.FullName);
    else if (old.Name is "mscorlib" or "System" or "System.Xml.Serialization")
        target = Type.GetType(type.FullName) ?? bcl.Select(a => a.GetType(type.FullName)).FirstOrDefault(t => t != null);
    else throw new NotSupportedException($"Unmapped assembly {old.FullName}");
    if (target == null) throw new TypeLoadException($"Unmapped type {type.FullName} from {old.Name}");
    var imported = module.ImportReference(target);
    mappings.Add(new { type = type.FullName, from = old.FullName, to = imported.Scope.ToString() });
    type.Scope = imported.Scope;
    type.Namespace = imported.Namespace;
    type.Name = imported.Name;
}
// Single verified ABI bridge: original Game1 constructor subscribes an XNA
// EventHandler<EventArgs>, whereas MonoGame expects ExitingEventArgs. The
// original callback remains unchanged. No menu/gameplay logic is modified.
var bridges = new List<object>();
foreach (var method in module.Types.SelectMany(t => t.Methods).Where(m => m.HasBody))
foreach (var instruction in method.Body.Instructions)
{
    if (instruction.Operand is not MethodReference member ||
        member.FullName != "System.Void Microsoft.Xna.Framework.Game::add_Exiting(System.EventHandler`1<System.EventArgs>)") continue;
    if (method.MetadataToken.ToUInt32() != 0x06000001 || instruction.OpCode != OpCodes.Call)
        throw new InvalidDataException("Unexpected Exiting subscription signature.");
    bridges.Add(new { method_token = method.MetadataToken.ToUInt32(), il_offset = instruction.Offset,
        original_member = member.FullName, bridge = "Tka.Compatibility.GameEvents.AddExiting" });
    instruction.Operand = module.ImportReference(typeof(GameEvents).GetMethod(nameof(GameEvents.AddExiting))!);
}
if (bridges.Count != 1) throw new InvalidDataException("Expected exactly one verified Exiting subscription.");
var pcMenuBridges = PcMenuPatches.Apply(module);
var internalRenderBridges = InternalRenderPatches.Apply(module);
module.Attributes &= ~(Mono.Cecil.ModuleAttributes.Required32Bit | Mono.Cecil.ModuleAttributes.StrongNameSigned);
module.RuntimeVersion = "v4.0.30319";
module.Kind = ModuleKind.Dll;
module.EntryPoint = null;
module.Assembly.Name.PublicKey = Array.Empty<byte>();
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
module.Write(output);
File.WriteAllText(output + ".retarget.json", JsonSerializer.Serialize(new {
    input_sha256 = expected, output_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))),
    abi_bridge_edits = bridges.Count, bridges, pc_menu_bridges = pcMenuBridges, internal_render_bridges = internalRenderBridges, mappings
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Retargeted {mappings.Count} type references and {bridges.Count} verified ABI bridge. Output: {output}");
return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}
}
}
