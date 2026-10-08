using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;

// Opt-in, bounded test observer. The original assembly stays untouched: an
// in-memory subclass calls original Draw, then reads back at most twelve frames.
public static class BaselineProbe
{
    private static readonly Stopwatch Timer = new();
    private static Action<string> log = _ => { };
    private static int captures;
    private static string directory = "";
    private static bool scripted;
    private static bool cameraDiagnostics;
    private static bool previousActive;
    private static Keys[] previousKeys = [];
    private static int lastStep = -1;
    private static double keyReleaseAt;
    private static (double at, Keys key)[] Steps = [(4, Keys.S), (6, Keys.Right), (7, Keys.Space),
        (10, Keys.B), (12, Keys.Space), (15, Keys.Space), (18, Keys.Space), (21, Keys.Space), (24, Keys.S), (27, Keys.B)];
    private static readonly FieldInfo Active = typeof(Keyboard).GetField("_isActive", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly FieldInfo KeyList = typeof(Keyboard).GetField("_keys", BindingFlags.Static | BindingFlags.NonPublic)!;

    public static Game Create(Type original, Action<string> write, bool simulateInput, bool displayTest = false, bool optionsTest = false, bool popagandaTest = false)
    {
        cameraDiagnostics = popagandaTest;
        if (popagandaTest) Steps = [(4, Keys.S), (6, Keys.Space), (8, Keys.Right), (10, Keys.Space), (12, Keys.Space), (14, Keys.Space), (18, Keys.Space), (24, Keys.S), (27, Keys.B)];
        if (displayTest) Steps = [(4, Keys.S), (6, Keys.Right), (7, Keys.Space),
            (9, Keys.Down), (10, Keys.Down), (11, Keys.Down), (12, Keys.Right),
            (14, Keys.Down), (15, Keys.Right), (17, Keys.Right), (19, Keys.Right), (21, Keys.Right),
            (23, Keys.Up), (24, Keys.Left), (26, Keys.Down), (27, Keys.Right), (29, Keys.Right),
            (31, Keys.Right), (33, Keys.Right), (35, Keys.Left), (37, Keys.Right), (39, Keys.Right), (41, Keys.B)];
        if (optionsTest) Steps = [(4, Keys.S), (6, Keys.Right), (7, Keys.Space), (9, Keys.Space), (10, Keys.Left),
            (11, Keys.Down), (12, Keys.Space), (13, Keys.Left), (14, Keys.Down), (15, Keys.Space), (16, Keys.Left),
            (17, Keys.Down), (18, Keys.Down), (19, Keys.Down), (20, Keys.Space), (22, Keys.B),
            (24, Keys.Down), (25, Keys.Down), (26, Keys.Down), (27, Keys.Down), (28, Keys.Down), (29, Keys.Down), (30, Keys.Space)];
        log = write;
        scripted = simulateInput;
        if (scripted) log("TEST MODE: scripted keyboard states; this does not verify physical keyboard/controller input.");
        directory = Path.Combine(AppContext.BaseDirectory, "logs", "frames-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(directory);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Tka.BaselineProbe"), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("Probe").DefineType("ObservedGame", TypeAttributes.Public, original);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var draw = original.GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(GameTime)])!;
        var wrapper = type.DefineMethod("Draw", MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(void), [typeof(GameTime)]);
        var il = wrapper.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, draw);
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, typeof(BaselineProbe).GetMethod(nameof(AfterDraw))!); il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(wrapper, draw);
        var update = original.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(GameTime)])!;
        var updateWrapper = type.DefineMethod("Update", MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(void), [typeof(GameTime)]);
        il = updateWrapper.GetILGenerator();
        il.Emit(OpCodes.Call, typeof(BaselineProbe).GetMethod(nameof(BeforeUpdate))!);
        il.BeginExceptionBlock();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, update);
        il.BeginFinallyBlock();
        il.Emit(OpCodes.Call, typeof(BaselineProbe).GetMethod(nameof(AfterUpdate))!);
        il.EndExceptionBlock(); il.Emit(OpCodes.Ret);
        type.DefineMethodOverride(updateWrapper, update);
        return (Game)Activator.CreateInstance(type.CreateType()!)!;
    }

    public static void BeforeUpdate()
    {
        if (!scripted) return;
        previousActive = (bool)Active.GetValue(null)!;
        var keys = (List<Keys>)KeyList.GetValue(null)!;
        previousKeys = keys.ToArray();
        Active.SetValue(null, false); keys.Clear();
        var now = Timer.Elapsed.TotalSeconds;
        if (lastStep + 1 < Steps.Length && now >= Steps[lastStep + 1].at && now >= keyReleaseAt)
        {
            lastStep++;
            keyReleaseAt = now + 0.15;
            log("Scripted key: " + Steps[lastStep].key);
        }
        if (lastStep >= 0 && now < keyReleaseAt) keys.Add(Steps[lastStep].key);
    }

    public static void AfterUpdate()
    {
        if (!scripted) return;
        var keys = (List<Keys>)KeyList.GetValue(null)!;
        keys.Clear(); keys.AddRange(previousKeys); Active.SetValue(null, previousActive);
    }

    public static object? ReadField(Game game, string name) =>
        game.GetType().Assembly.GetName().Name == "Tka.BaselineProbe"
            ? game.GetType().BaseType!.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game)
            : game.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game);

    public static void AfterDraw(Game game)
    {
        if (!Timer.IsRunning) Timer.Start();
        if (captures >= 12 || Timer.Elapsed.TotalSeconds < captures * 3) return;
        var device = game.GraphicsDevice;
        var pp = device.PresentationParameters;
        var pixels = new Color[pp.BackBufferWidth * pp.BackBufferHeight];
        device.GetBackBufferData(pixels);
        using var texture = new Texture2D(device, pp.BackBufferWidth, pp.BackBufferHeight);
        texture.SetData(pixels);
        var state = ReadField(game, "gameState");
        var path = Path.Combine(directory, $"{captures++:D2}-{state}.png");
        using var file = File.Create(path);
        texture.SaveAsPng(file, texture.Width, texture.Height);
        var score = ReadField(game, "scoreSystem");
        var currentScore = score?.GetType().GetField("currScore", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(score);
        log($"Frame captured: {path}; music: {Microsoft.Xna.Framework.Media.MediaPlayer.PlayPosition}; splash: {ReadField(game, "splashScreen")}; score: {currentScore}");
        if (cameraDiagnostics && state?.ToString() is ("PLAY" or "CAT_SELECT" or "PAUSE"))
        {
            var camera = ReadField(game, "stageSelectMenu")!.GetType().Assembly.GetType("Helicopter.Camera")!;
            log("Camera probe: " + string.Join(", ", new[] { "effectIndex", "alpha", "theta", "effectOffset" }.Select(name => name + "=" + camera.GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null))));
            if (ReadField(game, "renderTarget") is RenderTarget2D stage)
            {
                using var stageFile = File.Create(Path.Combine(directory, $"{captures - 1:D2}-stage-source.png"));
                stage.SaveAsPng(stageFile, stage.Width, stage.Height);
            }
        }
        if (state?.ToString() == "OPTIONS")
        {
            var options = ReadField(game, "optionsMenu")!;
            var fields = BindingFlags.Instance | BindingFlags.NonPublic;
            log("Options probe: " + string.Join(", ", new[] { "musicOn", "sfxOn", "vibrationOn" }.Select(name => name + "=" + options.GetType().GetField(name, fields)!.GetValue(options)))
                + "; index=" + options.GetType().BaseType!.GetField("index_", fields)!.GetValue(options));
        }
    }
}
