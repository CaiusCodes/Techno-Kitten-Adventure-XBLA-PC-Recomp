using Mono.Cecil;
using Mono.Cecil.Cil;
using Microsoft.Xna.Framework.Input;
using Tka.Compatibility;

internal static class PcInputPatches
{
    // The caller first verifies the complete original assembly SHA-256. These
    // exact members are then checked before adding small calls to normal source.
    public static List<object> Apply(ModuleDefinition module)
    {
        var report = new List<object>();
        MethodDefinition Find(string typeName, string methodName, string result, params string[] parameters)
        {
            var type = module.Types.Single(t => t.FullName == typeName);
            return type.Methods.Single(m => m.Name == methodName && m.ReturnType.FullName == result &&
                m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(parameters) && m.HasBody);
        }
        MethodReference Bridge(string name) => module.ImportReference(typeof(PcInput).GetMethod(name)!);
        void AtReturn(MethodDefinition method, string name, bool passButton = false, bool passThis = false)
        {
            var returns = method.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
            if (returns.Length != 1) throw new InvalidDataException("Input return signature changed: " + method.FullName);
            var ret = returns[0];
            var processor = method.Body.GetILProcessor();
            var offset = ret.Offset;
            var bridge = Bridge(name);
            ret.OpCode = passThis ? OpCodes.Ldarg_0 : passButton ? OpCodes.Ldarg_1 : OpCodes.Call;
            ret.Operand = passThis || passButton ? null : bridge;
            var cursor = ret;
            if (passThis || passButton) { cursor = Instruction.Create(OpCodes.Call, bridge); processor.InsertAfter(ret, cursor); }
            processor.InsertAfter(cursor, Instruction.Create(OpCodes.Ret));
            report.Add(new { method = method.FullName, token = method.MetadataToken.ToUInt32(), original_il_offset = ret.Offset,
                action = "before return", bridge = "PcInput." + name });
        }
        var input = "Helicopter.InputState";
        AtReturn(Find(input, "Update", "System.Void"), nameof(PcInput.Sample));
        AtReturn(Find(input, "EndUpdate", "System.Void"), nameof(PcInput.End));
        foreach (var (original, bridge) in new[] { ("IsButtonPressed", nameof(PcInput.Pressed)),
            ("IsButtonDown", nameof(PcInput.Down)), ("IsButtonUp", nameof(PcInput.Up)) })
            AtReturn(Find(input, original, "System.Boolean", typeof(Buttons).FullName!), bridge, true);
        AtReturn(Find("Helicopter.Menu", "Update", "System.Void", "System.Single", input), nameof(PcInput.MenuHover), passThis: true);
        var stage = Find("Helicopter.StageSelectMenu", "Update", "System.Void", "System.Single", input, "Helicopter.GameState&");
        var processor = stage.Body.GetILProcessor();
        var first = stage.Body.Instructions[0];
        processor.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        processor.InsertBefore(first, Instruction.Create(OpCodes.Call, Bridge(nameof(PcInput.StageArrows))));
        report.Add(new { method = stage.FullName, token = stage.MetadataToken.ToUInt32(), original_il_offset = 0,
            action = "at entry before original paging", bridge = "PcInput.StageArrows" });
        return report;
    }
}
