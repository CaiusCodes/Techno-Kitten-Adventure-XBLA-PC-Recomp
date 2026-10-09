using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Tka.Compatibility;

// Uses the already verified Game1.Draw end bridge; no additional guest IL edits.
internal sealed class TitleBuildBadge : IDisposable
{
    private readonly Game game;
    private readonly FieldInfo state;
    private readonly FieldInfo splash;
    private readonly object opening;
    private readonly string label;
    private Texture2D? artwork;

    public TitleBuildBadge(Game game, Type originalType)
    {
        this.game = game;
        state = originalType.GetField("gameState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        splash = originalType.GetField("splashScreen", BindingFlags.Instance | BindingFlags.NonPublic)!;
        // OPENING=0 alone includes the splash screens; splashScreen must also be false.
        opening = Enum.Parse(state.FieldType, "OPENING");
        var version = typeof(TitleBuildBadge).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        label = "v" + (version.EndsWith(".0", StringComparison.Ordinal) ? version[..^2] : version);
    }

    public void Draw(SpriteBatch batch)
    {
        if (!opening.Equals(state.GetValue(game)) || splash.GetValue(game) is not false) return;
        artwork ??= CreateArtwork(batch.GraphicsDevice);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);
        batch.Draw(artwork, new Vector2(24, 674), Color.White);
        batch.End();
    }

    // Original 5x7 pixel lettering, independent of imported game fonts.
    private Texture2D CreateArtwork(GraphicsDevice device)
    {
        const int unit = 2, height = 23;
        var width = label.Length * 6 * unit + 4;
        var mask = new bool[width * height];
        for (var i = 0; i < label.Length; i++)
        {
            var rows = label[i] switch {
                'v' => new[] { "00000", "00000", "10001", "10001", "10001", "01010", "00100" },
                '.' => new[] { "00000", "00000", "00000", "00000", "00000", "00110", "00110" },
                '0' => new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
                '1' => new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
                '2' => new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
                '3' => new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
                '4' => new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
                '5' => new[] { "11111", "10000", "10000", "11110", "00001", "00001", "11110" },
                '6' => new[] { "01110", "10000", "10000", "11110", "10001", "10001", "01110" },
                '7' => new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
                '8' => new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
                '9' => new[] { "01110", "10001", "10001", "01111", "00001", "00001", "01110" },
                _ => throw new InvalidDataException("Unsupported release version glyph.") };
            for (var y = 0; y < 7; y++) for (var x = 0; x < 5; x++)
                if (rows[y][x] == '1')
                    for (var dy = 0; dy < unit; dy++) for (var dx = 0; dx < unit; dx++)
                        mask[(2 + y * unit + dy) * width + 2 + i * 6 * unit + x * unit + dx] = true;
        }
        var pixels = new Color[width * height];
        for (var y = 1; y < 2 + 7 * unit; y++) for (var x = 1; x < width - 2; x++)
            if (mask[y * width + x])
                for (var dy = -1; dy <= 2; dy++) for (var dx = -1; dx <= 2; dx++)
                    pixels[(y + dy) * width + x + dx] = new Color(35, 17, 57);
        for (var i = 0; i < mask.Length; i++) if (mask[i]) pixels[i] = new Color(255, 240, 204);
        for (var y = 2 + 7 * unit + 3; y < 2 + 7 * unit + 5; y++) for (var x = 2; x < width - 7; x++)
            pixels[y * width + x] = x < width / 2 ? new Color(69, 233, 245) : new Color(255, 79, 163);
        var texture = new Texture2D(device, width, height);
        texture.SetData(pixels);
        return texture;
    }

    public void Dispose() => artwork?.Dispose();
}
