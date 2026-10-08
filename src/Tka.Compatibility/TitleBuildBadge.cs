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
    private readonly FieldInfo font;
    private readonly object opening;
    private readonly string label;
    private Texture2D? pixel;

    public TitleBuildBadge(Game game, Type originalType)
    {
        this.game = game;
        state = originalType.GetField("gameState", BindingFlags.Instance | BindingFlags.NonPublic)!;
        splash = originalType.GetField("splashScreen", BindingFlags.Instance | BindingFlags.NonPublic)!;
        font = originalType.Assembly.GetType("Helicopter.Global", true)!.GetField("spriteFont", BindingFlags.Static | BindingFlags.Public)!;
        // OPENING=0 alone includes the splash screens; splashScreen must also be false.
        opening = Enum.Parse(state.FieldType, "OPENING");
        var version = typeof(TitleBuildBadge).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        label = "Build v" + version;
    }

    public void Draw(SpriteBatch batch)
    {
        if (!opening.Equals(state.GetValue(game)) || splash.GetValue(game) is not false || font.GetValue(null) is not SpriteFont textFont) return;
        if (pixel is null) { pixel = new Texture2D(batch.GraphicsDevice, 1, 1); pixel.SetData(new[] { Color.White }); }
        var scale = 16f / textFont.LineSpacing;
        var width = (int)Math.Ceiling(textFont.MeasureString(label).X * scale) + 24;
        var bounds = new Rectangle(16, 676, width, 28);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        batch.Draw(pixel, bounds, new Color(24, 17, 38, 215));
        batch.Draw(pixel, new Rectangle(bounds.X, bounds.Y, 3, bounds.Height), new Color(69, 233, 245));
        batch.DrawString(textFont, label, new Vector2(bounds.X + 12, bounds.Y + 5), Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        batch.End();
    }

    public void Dispose() => pixel?.Dispose();
}
