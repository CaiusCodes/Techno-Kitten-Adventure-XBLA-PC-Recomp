using Mono.Cecil;
using Mono.Cecil.Cil;
using Tka.Compatibility;

internal static class ControllerPromptPatches
{
    public static List<object> Apply(ModuleDefinition module)
    {
        var report = new List<object>();
        MethodDefinition Find(string type, string name, params string[] parameters) => module.Types.Single(t => t.FullName == type).Methods.Single(m =>
            m.Name == name && m.HasBody && m.ReturnType.FullName == "System.Void" && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters));
        const string batch = "Microsoft.Xna.Framework.Graphics.SpriteBatch";
        const string draw = "System.Void Microsoft.Xna.Framework.Graphics.SpriteBatch::Draw(Microsoft.Xna.Framework.Graphics.Texture2D,Microsoft.Xna.Framework.Vector2,System.Nullable`1<Microsoft.Xna.Framework.Rectangle>,Microsoft.Xna.Framework.Color)";
        foreach (var (type, parameters, expected) in new[] {
            ("Helicopter.StageSelectMenu", new[] { batch }, 1),
            ("Helicopter.CatSelectMenu", new[] { batch, "System.Int32" }, 2) })
        {
            var method = Find(type,"DrawBackground",parameters);
            var calls = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Callvirt && i.Operand is MethodReference member && member.FullName == draw).ToArray();
            if (calls.Length != expected) throw new InvalidDataException("Prompt background draw signature changed.");
            foreach (var call in calls)
            {
                report.Add(new { method = method.FullName, token = method.MetadataToken.ToUInt32(), original_il_offset = call.Offset, bridge = "ControllerPrompts.DrawBackground" });
                call.OpCode = OpCodes.Call;
                call.Operand = module.ImportReference(typeof(ControllerPrompts).GetMethod(nameof(ControllerPrompts.DrawBackground))!);
            }
        }
        var instructions = Find("Helicopter.Game1", "DisplayInstructions");
        var strings = instructions.Body.Instructions.Where(i => i.OpCode == OpCodes.Ldstr).Select(i => (string)i.Operand).ToArray();
        if (!strings.SequenceEqual(new[] { "Press       To Start", "Hold       to go up\nRelease to go down" }))
            throw new InvalidDataException("Flight instruction string signature changed.");
        var il = instructions.Body.GetILProcessor();
        var first = instructions.Body.Instructions[0];
        il.InsertBefore(first, Instruction.Create(OpCodes.Call, module.ImportReference(typeof(ControllerPrompts).GetProperty(nameof(ControllerPrompts.Visible))!.GetMethod!)));
        il.InsertBefore(first, Instruction.Create(OpCodes.Brtrue, first));
        il.InsertBefore(first, Instruction.Create(OpCodes.Ret));
        report.Add(new { method = instructions.FullName, token = instructions.MetadataToken.ToUInt32(), original_il_offset = 0, bridge = "ControllerPrompts.Visible entry guard" });
        return report;
    }
}
