using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tka.Compatibility;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

// Opt-in GPU regression probe. Uses synthetic art and the imported game's exact
// Camera.Draw method; never changes retail assets or normal gameplay state.
internal sealed class InternalRenderProbe : Game
{
    private readonly GraphicsDeviceManager manager;
    private readonly Type camera;
    private readonly Action<string> log;
    private RenderTarget2D stage = null!;
    private SpriteBatch batch = null!;
    private Texture2D pixel = null!, pattern = null!;
    private string output = "";

    public InternalRenderProbe(Assembly guest, Action<string> log)
    { this.log = log; camera = guest.GetType("Helicopter.Camera", true)!; manager = new GraphicsDeviceManager(this); }

    protected override void LoadContent()
    {
        output = Path.Combine(AppContext.BaseDirectory, "logs", "internal-render-" + PcDisplay.Current.InternalScale + "x-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(output);
        stage = PcInternalRender.CreateStageTarget(GraphicsDevice, 1280, 720, false, SurfaceFormat.Color, DepthFormat.None);
        batch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White });
        pattern = new Texture2D(GraphicsDevice, 64, 36);
        var colors = new Color[64 * 36];
        for (var y = 0; y < 36; y++) for (var x = 0; x < 64; x++)
            colors[y * 64 + x] = new Color(x * 4, y * 7, ((x / 4 + y / 4) % 2) * 180 + 30);
        pattern.SetData(colors);
        var effects = Enumerable.Range(0, 5).Select(i => Content.Load<Effect>("Effects/effect" + i)).ToArray();
        camera.GetField("effects", BindingFlags.Public | BindingFlags.Static)!.SetValue(null, effects);
    }

    private void Field(string name, object value) => camera.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, value);

    protected override void Draw(GameTime time)
    {
        PcDisplay.BeginFrame();
        GraphicsDevice.SetRenderTarget(stage); GraphicsDevice.Clear(Color.Black);
        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp);
        batch.Draw(pattern, new Rectangle(0, 0, 1280, 720), Color.White);
        batch.Draw(pixel, new Rectangle(100, 120, 32, 32), Color.Red);
        batch.Draw(pixel, new Rectangle(170, 100, 20, 20), Color.Black);
        batch.Draw(pixel, new Vector2(180.5f, 100), null, Color.White, 0, Vector2.Zero, new Vector2(.5f, 20), SpriteEffects.None, 0);
        batch.End();
        PcDisplay.SetCameraTarget(GraphicsDevice, null);
        var source = new Color[stage.Width * stage.Height]; stage.GetData(source);
        var scale = PcDisplay.Current.InternalScale;
        if (stage.Width != 1280 * scale || stage.Height != 720 * scale) throw new InvalidDataException("Wrong internal stage dimensions.");
        var subpixelDetail = scale != 2 || source[210 * stage.Width + 360] == Color.Black && source[210 * stage.Width + 361] == Color.White;
        if (!subpixelDetail) throw new InvalidDataException("2x stage did not resolve the two half-pixel samples.");
        using (var file = File.Create(Path.Combine(output, "stage.png"))) stage.SaveAsPng(file, stage.Width, stage.Height);
        var results = new List<object>();
        var cases = new[] { ("identity", -1, 0f, 1f, SpriteEffects.None),
            ("rotate-zoom-shake", -1, .13f, 1.1f, SpriteEffects.None),
            ("flip-horizontal", -1, 0f, 1f, SpriteEffects.FlipHorizontally),
            ("flip-vertical", -1, 0f, 1f, SpriteEffects.FlipVertically) }
            .Concat(Enumerable.Range(0, 5).Select(i => ("effect" + i, i, 0f, 1f, SpriteEffects.None)));
        foreach (var (name, effect, rotation, zoom, flip) in cases)
        {
            camera.GetMethod("Reset")!.Invoke(null, null);
            var position = name == "rotate-zoom-shake" ? new Vector2(660, 340) : new Vector2(640, 360);
            Field("effectIndex", effect); Field("alpha", 1f); Field("position_", position);
            Field("rotation_", rotation); Field("scale_", zoom); Field("spriteEffect_", flip);
            PcDisplay.BeginFrame();
            camera.GetMethod("Draw")!.Invoke(null, new object[] { batch, stage, manager, GraphicsDevice });
            PcDisplay.EndFrame();
            var pp = GraphicsDevice.PresentationParameters;
            var pixels = new Color[pp.BackBufferWidth * pp.BackBufferHeight]; GraphicsDevice.GetBackBufferData(pixels);
            var unique = pixels.Select(p => p.PackedValue).Distinct().Count();
            var passed = unique > (effect == 1 ? 1 : 256);
            if (effect == -1)
            {
                var point = new Vector2(116, 136);
                if (flip == SpriteEffects.FlipHorizontally) point.X = 1280 - point.X;
                if (flip == SpriteEffects.FlipVertically) point.Y = 720 - point.Y;
                point = Vector2.Transform((point - new Vector2(640, 360)) * zoom, Matrix.CreateRotationZ(rotation)) + position;
                var ratio = Math.Min(pp.BackBufferWidth / 1280f, pp.BackBufferHeight / 720f);
                point = point * ratio + new Vector2((pp.BackBufferWidth - 1280 * ratio) / 2, (pp.BackBufferHeight - 720 * ratio) / 2);
                var sample = pixels[(int)point.Y * pp.BackBufferWidth + (int)point.X];
                passed &= sample.R > 240 && sample.G < 16 && sample.B < 16;
            }
            results.Add(new { name, unique_colors = unique, passed });
            using var capture = new Texture2D(GraphicsDevice, pp.BackBufferWidth, pp.BackBufferHeight); capture.SetData(pixels);
            using var file = File.Create(Path.Combine(output, name + ".png")); capture.SaveAsPng(file, capture.Width, capture.Height);
            if (!passed) throw new InvalidDataException("Internal render regression: " + name);
        }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { scale, stage_width = stage.Width, stage_height = stage.Height,
            output_width = GraphicsDevice.PresentationParameters.BackBufferWidth, output_height = GraphicsDevice.PresentationParameters.BackBufferHeight,
            half_pixel_detail_resolved = scale == 2 ? subpixelDetail : (bool?)null, cases = results }, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(Path.Combine(output, "allocations.json"), JsonSerializer.Serialize(RenderAllocationProbe.Run(GraphicsDevice, batch, stage), new JsonSerializerOptions { WriteIndented = true }));
        log("Internal render GPU checks passed: " + output);
        Exit();
    }
    protected override void UnloadContent() { batch.Dispose(); pixel.Dispose(); pattern.Dispose(); base.UnloadContent(); }
}
