using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tka.Compatibility;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

// One-shot opt-in render comparison. Connection overrides are test-only; real
// hot-plug hardware is not simulated or claimed by this check.
internal static class ControllerPromptProbe
{
    public static bool Enabled;
    private static bool ran;
    public static void Run(Game game, Action<string> log)
    {
        if (!Enabled || ran) return;
        ran = true;
        var original = game.GetType().BaseType!;
        var assembly = original.Assembly;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var batch = (SpriteBatch)original.GetField("spriteBatch", fields)!.GetValue(game)!;
        var connection = typeof(ControllerPrompts).GetField("testConnected",BindingFlags.Static|BindingFlags.NonPublic)!;
        var directory = Path.Combine(LocalServices.StorageRoot,"..","logs","controller-prompt-check");
        Directory.CreateDirectory(directory);
        using var target = new RenderTarget2D(game.GraphicsDevice,1280,720);
        var bindings = game.GraphicsDevice.GetRenderTargets();
        var viewport = game.GraphicsDevice.Viewport;
        try
        {
            foreach (var name in new[] { "StageSelectMenu", "CatSelectMenu", "instructions" })
            {
                object? menu = name == "instructions" ? null : Activator.CreateInstance(assembly.GetType("Helicopter."+name,true)!,true);
                var method = name == "instructions" ? original.GetMethod("DisplayInstructions",fields)!
                    : menu!.GetType().GetMethod("DrawBackground",fields)!;
                Color[] Render(bool connected, string suffix)
                {
                    connection.SetValue(null,connected); ControllerPrompts.Sample();
                    if (ControllerPrompts.Visible != connected) throw new InvalidOperationException("Connection state failed.");
                    game.GraphicsDevice.SetRenderTarget(target); game.GraphicsDevice.Clear(Color.White);
                    batch.Begin(SpriteSortMode.Deferred,BlendState.NonPremultiplied);
                    if (menu is null) method.Invoke(game,null);
                    else method.Invoke(menu,name == "CatSelectMenu" ? new object[] { batch,0 } : new object[] { batch });
                    batch.End(); game.GraphicsDevice.SetRenderTarget(null);
                    var pixels = new Color[1280*720]; target.GetData(pixels);
                    using var file = File.Create(Path.Combine(directory,name+"-"+suffix+".png"));
                    target.SaveAsPng(file,1280,720);
                    return pixels;
                }
                var connected = Render(true,"connected");
                var disconnected = Render(false,"disconnected");
                var restored = Render(true,"reconnected");
                var hiddenAgain = Render(false,"disconnected-again");
                if (!connected.SequenceEqual(restored) || !disconnected.SequenceEqual(hiddenAgain)) throw new InvalidOperationException("Hot-plug render restoration failed: "+name);
                var changed = 0;
                for (var i=0;i<connected.Length;i++) if(connected[i]!=disconnected[i])
                {
                    changed++;
                    if (menu is not null && !new Rectangle(118,575,218,100).Contains(i%1280,i/1280) && !new Rectangle(980,575,182,100).Contains(i%1280,i/1280))
                        throw new InvalidOperationException("Unrelated menu pixels changed: "+name);
                }
                if (changed == 0 || menu is null && disconnected.Any(c=>c!=Color.White)) throw new InvalidOperationException("Prompt visibility failed: "+name);
                log("Controller prompt render PASS: "+name+"; changed pixels="+changed+"; connected/disconnected/reconnected restored.");
            }
        }
        finally { connection.SetValue(null,null); ControllerPrompts.Sample(); game.GraphicsDevice.SetRenderTargets(bindings); game.GraphicsDevice.Viewport=viewport; }
        log("Controller prompt integration PASS (simulated connection; physical hot-plug NOT TESTED).");
    }
}
