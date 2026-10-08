using Mono.Cecil;
using Mono.Cecil.Cil;
using Tka.Compatibility;

internal static class InternalRenderPatches
{
    // Invoked only after AssemblyRetargeter verifies the complete original hash.
    public static List<object> Apply(ModuleDefinition module)
    {
        var report = new List<object>();
        MethodDefinition Find(string type, string name, uint token) => module.Types.Single(t => t.FullName == type)
            .Methods.Single(m => m.Name == name && m.MetadataToken.ToUInt32() == token);
        void Replace(MethodDefinition method, int offset, string signature, OpCode opcode, string bridge)
        {
            var instruction = method.Body.Instructions.Single(i => i.Offset == offset);
            if (instruction.OpCode != opcode || instruction.Operand is not MethodReference target || target.FullName != signature)
                throw new InvalidDataException("Internal-render call signature changed: " + method.FullName);
            report.Add(new { method = method.FullName, token = method.MetadataToken.ToUInt32(), original_il_offset = offset, original_member = signature, bridge });
            instruction.OpCode = OpCodes.Call;
            instruction.Operand = module.ImportReference(typeof(PcInternalRender).GetMethod(bridge)!);
        }
        var initialize = Find("Helicopter.Game1", "Initialize", 0x06000004);
        var allocation = initialize.Body.Instructions.Single(i => i.Offset == 0x001C);
        if (allocation.Next.OpCode != OpCodes.Stfld || allocation.Next.Operand is not FieldReference field ||
            field.FullName != "Microsoft.Xna.Framework.Graphics.RenderTarget2D Helicopter.Game1::renderTarget")
            throw new InvalidDataException("Original stage field signature changed.");
        Replace(initialize, 0x001C, "System.Void Microsoft.Xna.Framework.Graphics.RenderTarget2D::.ctor(Microsoft.Xna.Framework.Graphics.GraphicsDevice,System.Int32,System.Int32,System.Boolean,Microsoft.Xna.Framework.Graphics.SurfaceFormat,Microsoft.Xna.Framework.Graphics.DepthFormat)",
            OpCodes.Newobj, nameof(PcInternalRender.CreateStageTarget));
        var camera = Find("Helicopter.Camera", "Draw", 0x0600013A);
        Replace(camera, 0x01A2, "System.Void Microsoft.Xna.Framework.Graphics.SpriteBatch::Draw(Microsoft.Xna.Framework.Graphics.Texture2D,Microsoft.Xna.Framework.Vector2,System.Nullable`1<Microsoft.Xna.Framework.Rectangle>,Microsoft.Xna.Framework.Color,System.Single,Microsoft.Xna.Framework.Vector2,System.Single,Microsoft.Xna.Framework.Graphics.SpriteEffects,System.Single)",
            OpCodes.Callvirt, nameof(PcInternalRender.DrawCameraTransform));
        Replace(camera, 0x01E0, "System.Void Microsoft.Xna.Framework.Graphics.SpriteBatch::Draw(Microsoft.Xna.Framework.Graphics.Texture2D,Microsoft.Xna.Framework.Vector2,Microsoft.Xna.Framework.Color)",
            OpCodes.Callvirt, nameof(PcInternalRender.DrawCameraEffect));
        return report;
    }
}
