using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Tka.Compatibility;

// Only the independently verified menu backgrounds and instruction method use
// these bridges. Original textures stay intact; cleaned footer patches live in RAM.
public static class ControllerPrompts
{
    private static FieldInfo stageTexture = null!, catTexture = null!;
    private static Texture2D? stageLeft, stageRight;
    private static bool? testConnected = null; // Set only by the bounded host rendering probe.
    public static bool Visible { get; private set; }
    private static readonly Rectangle Left = new(118, 575, 218, 100);
    private static readonly Rectangle Right = new(980, 575, 182, 100);

    public static void Initialize(Type originalType, Game game)
    {
        var global = originalType.Assembly.GetType("Helicopter.Global", true)!;
        stageTexture = global.GetField("selectStageTex")!;
        catTexture = global.GetField("selectCatTex")!;
        if (stageTexture.FieldType != typeof(Texture2D) || catTexture.FieldType != typeof(Texture2D))
            throw new InvalidDataException("Controller prompt atlas signature changed.");
        game.Disposed += (_, _) => { stageLeft?.Dispose(); stageRight?.Dispose(); stageLeft = stageRight = null; };
    }

    public static void Sample()
    {
        if (testConnected is bool forced) { Visible = forced; return; }
        Visible = false;
        for (var i = 0; i < 4; i++) if (GamePad.GetState((PlayerIndex)i).IsConnected) { Visible = true; break; }
    }

    public static void DrawBackground(SpriteBatch batch, Texture2D texture, Vector2 position, Rectangle? source, Color color)
    {
        if (Visible) { batch.Draw(texture, position, source, color); return; }
        if (ReferenceEquals(texture, catTexture.GetValue(null)) && position == new Vector2(0, 428) && source == new Rectangle(0, 428, 1280, 292))
        {
            // Cat-select footer is transparent except for A/Select and Back/B.
            // Omit just those rectangles, preserving the underlying live stage.
            foreach (var region in new[] { new Rectangle(0,428,1280,159), new Rectangle(0,587,130,73),
                new Rectangle(331,587,657,73), new Rectangle(1153,587,127,73), new Rectangle(0,660,1280,60) })
                batch.Draw(texture, new Vector2(region.X, region.Y), region, color);
            return;
        }
        batch.Draw(texture, position, source, color);
        if (ReferenceEquals(texture, stageTexture.GetValue(null)) && position == Vector2.Zero && source == new Rectangle(0,0,1280,720))
        {
            stageLeft ??= CleanFooter(texture, Left);
            stageRight ??= CleanFooter(texture, Right);
            batch.Draw(stageLeft, new Vector2(Left.X, Left.Y), color);
            batch.Draw(stageRight, new Vector2(Right.X, Right.Y), color);
        }
    }

    // The stage footer is baked into opaque cloud artwork. Reconstruct each
    // small prompt rectangle from its unchanged boundary with harmonic colour
    // interpolation. This is local presentation only, never an imported file edit.
    private static Texture2D CleanFooter(Texture2D original, Rectangle area)
    {
        var border = new Rectangle(area.X - 1, area.Y - 1, area.Width + 2, area.Height + 2);
        var pixels = new Color[border.Width * border.Height];
        original.GetData(0, border, pixels, 0, pixels.Length);
        var channels = new float[pixels.Length * 3];
        var mask = new bool[pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
        { channels[i*3] = pixels[i].R; channels[i*3+1] = pixels[i].G; channels[i*3+2] = pixels[i].B; }
        for (var y = 1; y <= area.Height; y++) for (var x = 1; x <= area.Width; x++)
        {
            var px = area.X+x-1; var py = area.Y+y-1;
            var centre = area == Left ? 166 : 1116;
            var text = area == Left ? new Rectangle(198,608,130,31) : new Rectangle(992,608,91,31);
            mask[y*border.Width+x] = (px-centre)*(px-centre)+(py-625)*(py-625) <= 44*44 || text.Contains(px,py);
            if (!mask[y*border.Width+x]) continue;
            for (var c = 0; c < 3; c++)
                channels[(y*border.Width+x)*3+c] = MathHelper.Lerp(channels[x*3+c], channels[((border.Height-1)*border.Width+x)*3+c], y/(float)(border.Height-1));
        }
        for (var pass = 0; pass < 180; pass++)
            for (var y = 1; y <= area.Height; y++) for (var x = 1; x <= area.Width; x++)
            {
                if (!mask[y*border.Width+x]) continue;
                for (var c = 0; c < 3; c++)
                {
                    var i = (y*border.Width+x)*3+c;
                    var average = (channels[i-3]+channels[i+3]+channels[i-border.Width*3]+channels[i+border.Width*3])/4;
                    channels[i] += 1.75f*(average-channels[i]);
                }
            }
        var clean = new Color[area.Width*area.Height];
        for (var y = 0; y < area.Height; y++) for (var x = 0; x < area.Width; x++)
        {
            var i = ((y+1)*border.Width+x+1)*3;
            clean[y*area.Width+x] = new Color((byte)Math.Clamp(MathF.Round(channels[i]),0,255),
                (byte)Math.Clamp(MathF.Round(channels[i+1]),0,255), (byte)Math.Clamp(MathF.Round(channels[i+2]),0,255), (byte)255);
        }
        var result = new Texture2D(original.GraphicsDevice,area.Width,area.Height);
        result.SetData(clean);
        return result;
    }
}
