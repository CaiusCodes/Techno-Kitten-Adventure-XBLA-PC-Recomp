using System.Security.Cryptography;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Tka.Compatibility;

internal static class RuntimeAdapter
{
    public static int Patch(string input, string output)
    {
        var bytes = File.ReadAllBytes(input);
        const string expected = "97063554F0508C29F269892E796603C4C9041242215FFF72B0E1B87570539144";
        if (Convert.ToHexString(SHA256.HashData(bytes)) != expected)
            throw new InvalidDataException("Unsupported MonoGame 3.8.4.1 WindowsDX binary.");
        if (Path.GetFullPath(input) == Path.GetFullPath(output)) throw new IOException("Cannot modify the input runtime.");
        using var module = ModuleDefinition.ReadModule(new MemoryStream(bytes));
        var constructor = module.ImportReference(typeof(XboxAudioReader).GetConstructor([typeof(Stream)])!);
        var changes = new List<object>();
        foreach (var type in module.Types.Where(t => t.FullName is
            "Microsoft.Xna.Framework.Audio.AudioEngine" or "Microsoft.Xna.Framework.Audio.SoundBank" or "Microsoft.Xna.Framework.Audio.WaveBank"))
        foreach (var method in type.Methods.Where(m => m.HasBody))
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode != OpCodes.Newobj || instruction.Operand is not MethodReference member ||
                member.FullName != "System.Void System.IO.BinaryReader::.ctor(System.IO.Stream)") continue;
            changes.Add(new { method = method.FullName, token = method.MetadataToken.ToUInt32(), il_offset = instruction.Offset });
            instruction.Operand = constructor;
        }
        if (changes.Count != 3) throw new InvalidDataException($"Expected 3 XACT reader substitutions, found {changes.Count}.");
        // XNA exposed these two overloads. MonoGame replaced them with optional
        // arguments, which are source-compatible but not binary-compatible.
        // Restore forwarding overloads in our runtime; leave guest calls intact.
        var spriteBatch = module.GetType("Microsoft.Xna.Framework.Graphics.SpriteBatch");
        var begin = spriteBatch.Methods.Single(m => m.Name == "Begin" && m.Parameters.Count == 7);
        // Scale coordinates only for the port-owned stage target and canvas;
        // leave unrelated texture baking and installer/tool rendering alone.
        var beginIl = begin.Body.GetILProcessor();
        var first = begin.Body.Instructions[0];
        foreach (var instruction in new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg, begin.Parameters[6]),
            Instruction.Create(OpCodes.Call, module.ImportReference(typeof(PcDisplay).GetMethod(nameof(PcDisplay.CanvasTransform))!)),
            Instruction.Create(OpCodes.Starg, begin.Parameters[6]) }) beginIl.InsertBefore(first, instruction);
        changes.Add(new { method = begin.FullName, token = begin.MetadataToken.ToUInt32(), il_offset = 0, bridge = "PcDisplay.CanvasTransform" });
        foreach (var arity in new[] { 2, 6 })
        {
            if (spriteBatch.Methods.Any(m => m.Name == "Begin" && m.Parameters.Count == arity)) throw new InvalidDataException("Unexpected Begin overload.");
            var wrapper = new MethodDefinition("Begin", MethodAttributes.Public | MethodAttributes.HideBySig, module.TypeSystem.Void);
            foreach (var parameter in begin.Parameters.Take(arity))
                wrapper.Parameters.Add(new ParameterDefinition(parameter.Name, ParameterAttributes.None, parameter.ParameterType));
            wrapper.Body.InitLocals = true;
            var transform = new VariableDefinition(begin.Parameters[6].ParameterType);
            wrapper.Body.Variables.Add(transform);
            var il = wrapper.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            foreach (var parameter in wrapper.Parameters) il.Emit(OpCodes.Ldarg, parameter);
            for (var index = arity; index < 6; index++) il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldloc, transform); // default Nullable<Matrix> => identity
            il.Emit(OpCodes.Call, begin);
            il.Emit(OpCodes.Ret);
            spriteBatch.Methods.Add(wrapper);
            changes.Add(new { added_method = wrapper.FullName, forwards_to = begin.FullName });
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        module.Write(output);
        File.WriteAllText(output + ".patch.json", JsonSerializer.Serialize(new {
            input_sha256 = expected, output_sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output))), changes
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Adapted three XACT readers and restored two XNA SpriteBatch overloads in the private runtime copy.");
        return 0;
    }
}
