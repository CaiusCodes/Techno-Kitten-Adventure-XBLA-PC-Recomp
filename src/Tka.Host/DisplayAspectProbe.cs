using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Tka.Compatibility;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

// Opt-in check of the final presentation, including the OS-sized client area.
internal sealed class DisplayAspectProbe : Game
{
    private SpriteBatch batch = null!;
    private Texture2D pixel = null!;
    private int frame, mode;
    private bool failed;
    private readonly List<object> results = [];
    public DisplayAspectProbe() { _ = new GraphicsDeviceManager(this); }
    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        if (PcDisplay.Current.Fullscreen || PcDisplay.Current.Resolution != 0)
            throw new InvalidDataException("Aspect probe requires a fresh windowed 720p fixture.");
    }
    protected override void Update(GameTime time)
    {
        if (frame == 30 && mode < 8)
        {
            mode++; frame = 0;
            if (mode == 4) PcDisplay.Change(true, 1);
            if (mode < 8) PcDisplay.Change(false, 1);
        }
        base.Update(time);
    }
    protected override void Draw(GameTime time)
    {
        if (mode == 8)
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "logs", "display-aspect-results.json"),
                JsonSerializer.Serialize(new { passed = !failed, cases = results }, new JsonSerializerOptions { WriteIndented = true }));
            if (failed) throw new InvalidDataException("Displayed aspect ratio regression; see display-aspect-results.json.");
            Exit(); return;
        }
        PcDisplay.BeginFrame();
        batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque);
        batch.Draw(pixel, new Rectangle(0, 0, 1280, 720), Color.White);
        batch.Draw(pixel, new Rectangle(590, 310, 100, 100), Color.Cyan);
        batch.End();
        PcDisplay.EndFrame();
        if (++frame != 30) return;
        var pp = GraphicsDevice.PresentationParameters;
        if (pp.PresentationInterval != PresentInterval.Immediate) throw new InvalidDataException("VSync is not disabled.");
        var client = Window.ClientBounds;
        var native = System.Windows.Forms.Control.FromHandle(Window.Handle)!.ClientSize;
        var data = new Color[pp.BackBufferWidth * pp.BackBufferHeight];
        GraphicsDevice.GetBackBufferData(data);
        (int width, int height) Extent(Func<Color, bool> match)
        {
            int minX = pp.BackBufferWidth, minY = pp.BackBufferHeight, maxX = -1, maxY = -1;
            for (int y = 0; y < pp.BackBufferHeight; y++) for (int x = 0; x < pp.BackBufferWidth; x++)
                if (match(data[y * pp.BackBufferWidth + x])) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            return (maxX - minX + 1, maxY - minY + 1);
        }
        var picture = Extent(c => c.G > 240 && c.B > 240);
        var square = Extent(c => c.R < 16 && c.G > 240 && c.B > 240);
        double Aspect((int width, int height) bounds) => (double)bounds.width * native.Width / pp.BackBufferWidth / ((double)bounds.height * native.Height / pp.BackBufferHeight);
        var pictureAspect = Aspect(picture); var squareAspect = Aspect(square);
        var passed = Math.Abs(pictureAspect - 16d / 9) < .005 && Math.Abs(squareAspect - 1) < .015
            && client.Width == native.Width && client.Height == native.Height;
        failed |= !passed;
        results.Add(new { settings = PcDisplay.Current, client_width = native.Width, client_height = native.Height,
            buffer_width = pp.BackBufferWidth, buffer_height = pp.BackBufferHeight, picture_aspect = pictureAspect, square_aspect = squareAspect, passed });
        LocalServices.Log($"Aspect probe: {PcDisplay.Current}; client {native}; buffer {pp.BackBufferWidth}x{pp.BackBufferHeight}; picture {pictureAspect:F5}; square {squareAspect:F5}; passed {passed}");
    }
    protected override void UnloadContent() { batch.Dispose(); pixel.Dispose(); base.UnloadContent(); }
}
