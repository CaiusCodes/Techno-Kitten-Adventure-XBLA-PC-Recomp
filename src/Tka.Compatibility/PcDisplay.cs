using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Tka.Compatibility;

// Presentation stays separate from the original 1280x720 simulation and camera.
public static class PcDisplay
{
    // Output size and internal supersampling are independent settings.
    public sealed record Settings(bool Fullscreen = false, int Resolution = 0, int InternalScale = 2);
    public static readonly Point[] Resolutions = [new(1280, 720), new(1920, 1080), new(2560, 1440), new(3840, 2160)];
    public static Settings Current { get; private set; } = new();
    private static GraphicsDeviceManager graphics = null!;
    private static GameWindow window = null!;
    private static (int, int, int, int)? lastPresentation;
    private static RenderTarget2D? canvas;
    // SpriteBatch.Begin and graphics access run on the game's render thread.
    private static readonly RenderTargetBinding[] targetScratch = new RenderTargetBinding[1];
    private static SpriteBatch? presenter;
    private static TitleBuildBadge? buildBadge;
    private static string SettingsPath => Path.Combine(LocalServices.StorageRoot, "settings", "display.json");

    public static void Initialize(Game game, GraphicsDeviceManager manager)
    {
        PcInput.Initialize(game);
        graphics = manager;
        window = game.Window;
        for (var type = game.GetType(); type != null; type = type.BaseType)
            if (type.FullName == "Helicopter.Game1") { buildBadge = new TitleBuildBadge(game, type); ControllerPrompts.Initialize(type, game); break; }
        lastPresentation = null;
        Current = new();
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath));
                if (loaded is null || loaded.Resolution < 0 || loaded.Resolution >= Resolutions.Length || loaded.InternalScale is < 1 or > 3)
                    throw new InvalidDataException("Invalid display settings.");
                Current = loaded;
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
        { LocalServices.Log("Display settings defaulted: " + e.Message); }
        Apply(Current, persist: false);
        game.Disposed += (_, _) => { buildBadge?.Dispose(); buildBadge = null; canvas?.Dispose(); presenter?.Dispose(); canvas = null; presenter = null; PcInternalRender.Release(); };
    }

    public static void Change(bool mode, int direction)
    {
        var next = mode ? Current with { Fullscreen = !Current.Fullscreen }
            : Current with { Resolution = (Current.Resolution + direction + Resolutions.Length) % Resolutions.Length };
        Apply(next, persist: true);
    }

    private static void Configure(Settings settings)
    {
        var size = Resolutions[settings.Resolution];
        graphics.HardwareModeSwitch = false; // Borderless fullscreen retains the desktop display mode.
        graphics.SynchronizeWithVerticalRetrace = false; // Keep VSync off across all mode changes.
        graphics.PreferredBackBufferWidth = size.X;
        graphics.PreferredBackBufferHeight = size.Y;
        graphics.IsFullScreen = settings.Fullscreen;
        graphics.ApplyChanges();
        var pp = graphics.GraphicsDevice.PresentationParameters;
        if (!settings.Fullscreen && (pp.BackBufferWidth != size.X || pp.BackBufferHeight != size.Y))
            throw new NotSupportedException($"Display does not support the selected {size.X}x{size.Y} mode.");
    }

    private static void Apply(Settings next, bool persist)
    {
        var previous = Current;
        try { Configure(next); }
        catch (Exception e)
        {
            LocalServices.Log("Display change failed; restoring previous mode: " + e.Message);
            var fallback = persist ? previous : new Settings(Resolution: 0);
            Configure(fallback);
            Current = fallback;
            return;
        }
        Current = next;
        var pp = graphics.GraphicsDevice.PresentationParameters;
        LocalServices.Log($"Display applied: {(next.Fullscreen ? "Fullscreen" : "Windowed")}; output {Resolutions[next.Resolution]}; internal scale {next.InternalScale}x; backbuffer {pp.BackBufferWidth}x{pp.BackBufferHeight}; fullscreen {pp.IsFullScreen}; vsync {graphics.SynchronizeWithVerticalRetrace}; interval {pp.PresentationInterval}");
        if (!persist) return;
        var temp = SettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, Current, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { LocalServices.Log("Display setting could not be saved: " + e.Message); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void BeginFrame()
    {
        ControllerPrompts.Sample();
        var device = graphics.GraphicsDevice;
        var size = new Point(1280 * Current.InternalScale, 720 * Current.InternalScale);
        if (canvas is not null && (canvas.Width != size.X || canvas.Height != size.Y)) { canvas.Dispose(); canvas = null; }
        if (canvas is null)
        {
            canvas = new RenderTarget2D(device, size.X, size.Y, false, SurfaceFormat.Color, DepthFormat.None);
            LocalServices.Log($"Display canvas created: {size.X}x{size.Y}; scale {size.X / 1280f:0.##}x.");
        }
        presenter ??= new SpriteBatch(device);
        device.SetRenderTarget(canvas);
        device.Clear(Color.Black);
    }

    // Only the hash-verified original Camera.Draw null-target call uses this.
    public static void SetCameraTarget(GraphicsDevice device, RenderTarget2D? target) => device.SetRenderTarget(target ?? canvas);

    // Only the exact owned stage target and canvas receive logical-coordinate
    // scaling. Font baking, other textures and the final backbuffer are excluded.
    public static Matrix? CanvasTransform(SpriteBatch batch, Matrix? original)
    {
        if (canvas is null) return original;
        var device = batch.GraphicsDevice;
        if (device.RenderTargetCount != 1) return original;
        device.GetRenderTargets(targetScratch);
        var target = targetScratch[0].RenderTarget;
        targetScratch[0] = default; // Do not retain unrelated render targets.
        if (ReferenceEquals(target, canvas))
            return (original ?? Matrix.Identity) * Matrix.CreateScale(canvas.Width / 1280f, canvas.Height / 720f, 1);
        return PcInternalRender.Transform(target, original);
    }

    public static void EndFrame()
    {
        var device = graphics.GraphicsDevice;
        buildBadge?.Draw(presenter!);
        device.SetRenderTarget(null);
        device.Clear(Color.Black);
        var pp = device.PresentationParameters;
        var client = window.ClientBounds;
        if (client.Width <= 0 || client.Height <= 0) return;
        // Windows can constrain a requested 4K window to the monitor while
        // MonoGame retains the requested backbuffer. DXGI then scales that
        // buffer to the client area independently on each axis. Fit in actual
        // client pixels first, then map back to buffer pixels to compensate.
        var scale = Math.Min(client.Width / 1280d, client.Height / 720d);
        var width = Math.Clamp((int)Math.Round(1280 * scale * pp.BackBufferWidth / client.Width), 1, pp.BackBufferWidth);
        var height = Math.Clamp((int)Math.Round(720 * scale * pp.BackBufferHeight / client.Height), 1, pp.BackBufferHeight);
        var presentation = (client.Width, client.Height, pp.BackBufferWidth, pp.BackBufferHeight);
        if (lastPresentation != presentation)
        {
            lastPresentation = presentation;
            LocalServices.Log($"Presentation fit: client {client.Width}x{client.Height}; backbuffer {pp.BackBufferWidth}x{pp.BackBufferHeight}; picture {width}x{height}; aspect 16:9.");
        }
        presenter!.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
        presenter.Draw(canvas!, new Rectangle((pp.BackBufferWidth - width) / 2, (pp.BackBufferHeight - height) / 2, width, height), Color.White);
        presenter.End();
    }
}
