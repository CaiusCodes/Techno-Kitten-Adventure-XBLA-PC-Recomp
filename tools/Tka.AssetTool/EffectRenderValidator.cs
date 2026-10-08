using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

internal sealed class EffectRenderValidator : Game
{
    private readonly string input, output;
    private readonly List<object> results = [];
    public bool Passed { get; private set; } = true;
    public EffectRenderValidator(string input, string output)
    {
        this.input = Path.GetFullPath(input); this.output = Path.GetFullPath(output);
        if (Directory.Exists(this.output)) throw new IOException("Choose a fresh render-validation directory.");
        Directory.CreateDirectory(this.output);
        _ = new GraphicsDeviceManager(this) { GraphicsProfile = GraphicsProfile.HiDef, PreferredBackBufferWidth = 256, PreferredBackBufferHeight = 256 };
        Content.RootDirectory = this.input;
    }
    public static int Validate(string input, string output)
    {
        using var game = new EffectRenderValidator(input, output);
        game.Run();
        return game.Passed ? 0 : 1;
    }
    protected override void LoadContent()
    {
        using var sprite = new SpriteEffect(GraphicsDevice);
        DumpSignatures(sprite, "sprite");
    }
    protected override void Draw(GameTime time)
    {
        using var texture = new Texture2D(GraphicsDevice, 256, 256);
        var source = new Color[256 * 256];
        for (var y = 0; y < 256; y++) for (var x = 0; x < 256; x++)
            source[y * 256 + x] = new Color((byte)x, (byte)y, (byte)(((x / 32 + y / 32) % 2) * 180 + 30), (byte)255);
        texture.SetData(source);
        using var target = new RenderTarget2D(GraphicsDevice, 256, 256);
        using var batch = new SpriteBatch(GraphicsDevice);
        for (var index = 0; index < 5; index++)
        {
            var effect = Content.Load<Effect>("effect" + index);
            DumpSignatures(effect, "effect" + index);
            if (index == 0) effect.Parameters["Offset"].SetValue(Vector2.Zero);
            if (index == 4) effect.Parameters["Strength"].SetValue(0f);
            GraphicsDevice.SetRenderTarget(target); GraphicsDevice.Clear(Color.White);
            batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, null, null, effect);
            batch.Draw(texture, Vector2.Zero, Color.White);
            batch.End(); GraphicsDevice.SetRenderTarget(null);
            var pixels = new Color[source.Length]; target.GetData(pixels);
            var unique = pixels.Select(p => p.PackedValue).Distinct().Count();
            var error = pixels.Select((p, i) => (Math.Abs(p.R - source[i].R) + Math.Abs(p.G - source[i].G) + Math.Abs(p.B - source[i].B)) / 3.0).Average();
            // Edge detection intentionally produces sparse, nearly binary lines.
            // The other effects must retain the pattern's spatial color variation.
            var black = pixels.Count(p => p.R == 0 && p.G == 0 && p.B == 0);
            var passed = index == 1 ? unique > 1 && black > pixels.Length / 2 && black < pixels.Length - 100
                : unique > 256 && (index != 0 && index != 4 || error < 1);
            Passed &= passed;
            results.Add(new { effect = index, unique_colors = unique, rgb_mean_error = error, passed });
            using var file = File.Create(Path.Combine(output, "effect" + index + ".png"));
            target.SaveAsPng(file, 256, 256);
        }
        File.WriteAllText(Path.Combine(output, "render-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(results));
        Exit();
    }
    private void DumpSignatures(Effect effect, string name)
    {
        var shaders = (Array)typeof(Effect).GetField("_shaders", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(effect)!;
        var report = new StringBuilder();
        foreach (var shader in shaders)
        {
            var bytes = (byte[])shader!.GetType().GetField("_shaderBytecode", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(shader)!;
            report.AppendLine("Stage: " + shader.GetType().GetProperty("Stage")!.GetValue(shader));
            for (var i = 0; i < BitConverter.ToInt32(bytes, 28); i++)
            {
                var at = BitConverter.ToInt32(bytes, 32 + i * 4);
                var tag = Encoding.ASCII.GetString(bytes, at, 4);
                if (tag is not ("ISGN" or "OSGN")) continue;
                var body = at + 8;
                for (var j = 0; j < BitConverter.ToInt32(bytes, body); j++)
                {
                    var record = body + 8 + j * 24;
                    var textAt = body + BitConverter.ToInt32(bytes, record);
                    var end = Array.IndexOf(bytes, (byte)0, textAt);
                    report.AppendLine($"{tag} {Encoding.ASCII.GetString(bytes, textAt, end-textAt)}{BitConverter.ToInt32(bytes, record+4)} register={BitConverter.ToInt32(bytes,record+16)} mask={bytes[record+20]:X}");
                }
            }
        }
        File.WriteAllText(Path.Combine(output, name + "-signatures.txt"), report.ToString());
        if (name != "sprite")
            foreach (var signature in new[] { "ISGN SV_Position0 register=0 mask=F", "ISGN COLOR0 register=1 mask=F", "ISGN TEXCOORD0 register=2 mask=3" })
                if (!report.ToString().Contains(signature, StringComparison.Ordinal)) Passed = false;
    }
}
