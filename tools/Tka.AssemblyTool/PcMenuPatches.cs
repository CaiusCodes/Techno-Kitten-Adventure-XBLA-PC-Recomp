using Mono.Cecil;
using Mono.Cecil.Cil;
using Tka.Compatibility;

internal static class PcMenuPatches
{
    // Called only after the complete original input SHA-256 has been verified.
    public static List<object> Apply(ModuleDefinition module)
    {
        var report = new List<object>();
        MethodDefinition Method(string type, string name, uint token, int count) =>
            module.Types.Single(t => t.FullName == type).Methods.Single(m => m.Name == name && m.MetadataToken.ToUInt32() == token && m.Parameters.Count == count);
        MethodReference Bridge(Type type, string name) => module.ImportReference(type.GetMethod(name)!);
        void BeforeReturns(MethodDefinition method, params Instruction[] sequence)
        {
            var ret = method.Body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
            report.Add(new { method = method.FullName, token = method.MetadataToken.ToUInt32(), original_il_offset = ret.Offset, action = "return bridge", bridge = sequence.Last().Operand?.ToString() });
            // Reuse the return instruction so original branches cannot skip the hook.
            var il = method.Body.GetILProcessor();
            ret.OpCode = sequence[0].OpCode; ret.Operand = sequence[0].Operand;
            var previous = ret;
            foreach (var instruction in sequence.Skip(1).Append(Instruction.Create(OpCodes.Ret))) { il.InsertAfter(previous, instruction); previous = instruction; }
        }
        var ctor = Method("Helicopter.OptionsMenu", ".ctor", 0x060000F0, 0);
        BeforeReturns(ctor, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Bridge(typeof(PcOptionsMenu), nameof(PcOptionsMenu.Construct))));
        var update = Method("Helicopter.OptionsMenu", "Update", 0x060000F1, 3);
        var navigation = update.Body.Instructions.Single(i => i.OpCode == OpCodes.Call && i.Operand is MethodReference m &&
            m.FullName == "System.Void Helicopter.Menu::Update(System.Single,Helicopter.InputState)");
        report.Add(new { method = update.FullName, token = update.MetadataToken.ToUInt32(), original_il_offset = navigation.Offset, action = "after base navigation", bridge = "PcOptionsMenu.BeforeActions" });
        var cursor = navigation;
        foreach (var instruction in new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_2),
            Instruction.Create(OpCodes.Call, Bridge(typeof(PcOptionsMenu), nameof(PcOptionsMenu.BeforeActions))) })
        { update.Body.GetILProcessor().InsertAfter(cursor, instruction); cursor = instruction; }
        BeforeReturns(update, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, Bridge(typeof(PcOptionsMenu), nameof(PcOptionsMenu.AfterActions))));
        var draw = Method("Helicopter.OptionsMenu", "Draw", 0x060000F3, 1);
        BeforeReturns(draw, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldarg_1), Instruction.Create(OpCodes.Call, Bridge(typeof(PcOptionsMenu), nameof(PcOptionsMenu.DrawValues))));
        var gameDraw = Method("Helicopter.Game1", "Draw", 0x06000009, 1);
        report.Add(new { method = gameDraw.FullName, token = gameDraw.MetadataToken.ToUInt32(), original_il_offset = 0, action = "entry bridge", bridge = "PcDisplay.BeginFrame" });
        gameDraw.Body.GetILProcessor().InsertBefore(gameDraw.Body.Instructions[0], Instruction.Create(OpCodes.Call, Bridge(typeof(PcDisplay), nameof(PcDisplay.BeginFrame))));
        BeforeReturns(gameDraw, Instruction.Create(OpCodes.Call, Bridge(typeof(PcDisplay), nameof(PcDisplay.EndFrame))));
        var camera = module.Types.Single(t => t.FullName == "Helicopter.Camera").Methods.Single(m => m.Name == "Draw" && m.Parameters.Count == 4);
        var target = camera.Body.Instructions.Single(i => i.Operand is MethodReference m && m.FullName == "System.Void Microsoft.Xna.Framework.Graphics.GraphicsDevice::SetRenderTarget(Microsoft.Xna.Framework.Graphics.RenderTarget2D)");
        if (target.Previous.OpCode != OpCodes.Ldnull || target.OpCode != OpCodes.Callvirt) throw new InvalidDataException("Camera output signature changed.");
        report.Add(new { method = camera.FullName, token = camera.MetadataToken.ToUInt32(), original_il_offset = target.Offset, action = "redirect null output to presentation canvas", bridge = "PcDisplay.SetCameraTarget" });
        target.OpCode = OpCodes.Call;
        target.Operand = Bridge(typeof(PcDisplay), nameof(PcDisplay.SetCameraTarget));
        return report;
    }
}
