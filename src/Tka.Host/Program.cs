using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tka.Compatibility;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var root = AppContext.BaseDirectory;
        if (args.Length >= 2 && args[0] == "--game-root")
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(args[1]));
            args = args.Skip(2).ToArray();
        }
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        var logPath = Path.Combine(root, "logs", "baseline-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
        using var log = new StreamWriter(logPath) { AutoFlush = true };
        void Write(string text) { Console.WriteLine(text); log.WriteLine($"{DateTime.UtcNow:O} {text}"); }
        LocalServices.Log = Write;
        LocalServices.StorageRoot = Path.Combine(root, "userdata");
        try
        {
            if (args.Length == 0) args = [Path.Combine(root, "Helicopter.dll"), Path.Combine(root, "Content"), "0", "--fixed60"];
            if (args.Length < 2) throw new ArgumentException("Usage: Techno Kitten Adventure assembly.dll content-directory [smoke-seconds] [--scripted] [--fixed60]");
            var assemblyPath = Path.GetFullPath(args[0]);
            var contentPath = Path.GetFullPath(args[1]);
            // MonoGame's TitleContainer initializes Location from the managed
            // host EXE directory (Game/resources/game). XNA audio opens its
            // banks relative to that Location, while the imported Content is
            // beside the play EXE in Game. Set the verified 3.8.4.1 property
            // before creating Game1; keep all game-derived files in Game.
            var titleLocation = typeof(TitleContainer).GetProperty("Location", BindingFlags.Static | BindingFlags.NonPublic);
            if (titleLocation?.PropertyType != typeof(string) || titleLocation.GetSetMethod(true) is null)
                throw new InvalidDataException("MonoGame TitleContainer location signature changed.");
            titleLocation.SetValue(null, root);
            if (!string.Equals((string?)titleLocation.GetValue(null), root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Could not set the portable content location.");
            var seconds = args.Length > 2 ? double.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 0;
            LocalServices.IsTrialMode = args.Contains("--trial-baseline", StringComparer.Ordinal);
            Write($"Assembly: {assemblyPath}; Content: {contentPath}; trial baseline: {LocalServices.IsTrialMode}");
            var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
            var gameType = assembly.GetType("Helicopter.Game1", throwOnError: true)!;
            MenuMouseProbe.Enabled = seconds > 0 && args.Contains("--mouse-menu-test");
            ControllerPromptProbe.Enabled = seconds > 0 && args.Contains("--controller-prompts-test");
            using var game = args.Contains("--display-aspect-test") ? new DisplayAspectProbe()
                : args.Contains("--internal-render-test") ? new InternalRenderProbe(assembly, Write)
                : seconds > 0 ? BaselineProbe.Create(gameType, Write, args.Contains("--scripted"), args.Contains("--display-test"), args.Contains("--options-test"), args.Contains("--popaganda-test"), args.Contains("--pc-input-test")) : (Game)Activator.CreateInstance(gameType)!;
            // The package targets the Xbox XNA HiDef profile; MonoGame defaults
            // to Reach unless the host selects the corresponding profile.
            var graphics = (GraphicsDeviceManager)game.Services.GetService<IGraphicsDeviceManager>();
            graphics.GraphicsProfile = GraphicsProfile.HiDef;
            graphics.DeviceCreated += (_, _) =>
            {
                var device = game.GraphicsDevice.GetType().GetField("_d3dDevice", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(game.GraphicsDevice);
                Write("Graphics profile: " + game.GraphicsDevice.GraphicsProfile + "; D3D level: " + device?.GetType().GetProperty("FeatureLevel")?.GetValue(device));
            };
            graphics.ApplyChanges();
            PcDisplay.Initialize(game, graphics);
            if (args.Contains("--fixed60", StringComparer.Ordinal))
            {
                game.TargetElapsedTime = TimeSpan.FromTicks(166667);
                game.IsFixedTimeStep = true;
                Write("Compatibility timing: fixed 60 updates/second. Original uncapped settings remain available through explicit arguments.");
            }
            game.Content.RootDirectory = contentPath;
            game.Window.Title = "Techno Kitten Adventure!";
            using var gameIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
            if (System.Windows.Forms.Control.FromHandle(game.Window.Handle) is System.Windows.Forms.Form gameForm)
                gameForm.Icon = gameIcon;
            if (seconds > 0) game.Components.Add(new SmokeTest(game, seconds, Write));
            game.Exiting += (_, _) => Write("Game Exiting event.");
            Write("Starting Game.Run.");
            try { game.Run(); }
            catch (Exception exception)
            {
                // Record the primary failure before framework disposal can mask it.
                Write("Game.Run failed: " + exception);
                return 1;
            }
            Write("Game.Run returned normally.");
            return 0;
        }
        catch (Exception exception)
        {
            Write(exception.ToString());
            return 1;
        }
    }
}

internal sealed class SmokeTest(Game game, double seconds, Action<string> log) : GameComponent(game)
{
    private readonly Stopwatch timer = new();
    private string? lastState;
    private bool? lastCursorVisible;
    private long frames;
    public override void Update(GameTime gameTime)
    {
        frames++;
        if (!timer.IsRunning) timer.Start();
        var state = BaselineProbe.ReadField(Game, "gameState")?.ToString();
        if (state != lastState) { log($"State: {state}"); lastState = state; }
        if (Game.IsMouseVisible != lastCursorVisible)
        {
            lastCursorVisible = Game.IsMouseVisible;
            log($"Cursor visible: {lastCursorVisible}; state: {state}");
        }
        if (timer.Elapsed.TotalSeconds >= seconds)
        {
            log($"Smoke-test timeout after {frames} updates; requesting normal exit.");
            Game.Exit();
        }
    }
}
