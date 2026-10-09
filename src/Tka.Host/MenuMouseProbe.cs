using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Tka.Compatibility;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;

// Opt-in bounded integration check: synthetic snapshots, actual imported menu
// Update bodies. Runs once after content loads; never enabled in normal play.
internal static class MenuMouseProbe
{
    public static bool Enabled;
    private static bool ran;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    public static void Run(Game game, Action<string> log)
    {
        if (!Enabled || ran) return;
        ran = true;
        if (!game.IsActive) throw new InvalidOperationException("Mouse test needs an active game window.");
        var assembly = game.GetType().BaseType!.Assembly;
        var input = Activator.CreateInstance(assembly.GetType("Helicopter.InputState", true)!, true)!;
        var stateType = assembly.GetType("Helicopter.GameState", true)!;
        var state = Enum.Parse(stateType, "OPTIONS");
        var options = Activator.CreateInstance(assembly.GetType("Helicopter.OptionsMenu", true)!, true)!;
        var stage = Activator.CreateInstance(assembly.GetType("Helicopter.StageSelectMenu", true)!, true)!;
        var inputFields = typeof(PcInput).GetFields(BindingFlags.Static | BindingFlags.NonPublic);
        void Snapshot(int x, int y)
        {
            var bounds = game.Window.ClientBounds;
            var scale = Math.Min(bounds.Width / 1280d, bounds.Height / 720d);
            var mouse = new MouseState((int)Math.Round((bounds.Width - 1280 * scale) / 2 + x * scale),
                (int)Math.Round((bounds.Height - 720 * scale) / 2 + y * scale), 0,
                ButtonState.Pressed, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            foreach (var field in inputFields)
            {
                if (field.Name == "currentMouse") field.SetValue(null, mouse);
                else if (field.Name == "previousMouse") field.SetValue(null, default(MouseState));
                else if (field.Name is "currentKeys" or "previousKeys") field.SetValue(null, default(KeyboardState));
                else if (field.Name == "mouseDirection") field.SetValue(null, 0);
                else if (field.Name == "mouseActionConsumed") field.SetValue(null, false);
            }
        }
        void Click(object menu, int x, int y)
        {
            Snapshot(x, y);
            object[] args = [0f, input, state];
            menu.GetType().GetMethod("Update", [typeof(float), input.GetType(), stateType.MakeByRefType()])!.Invoke(menu, args);
            if (!Equals(args[2], state)) throw new InvalidOperationException("Arrow/value click also activated a menu item.");
        }
        void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Mouse target failed: " + name);
            log("Mouse target PASS: " + name);
        }
        foreach (var (name, y) in new[] { ("musicOn", 191), ("sfxOn", 254), ("vibrationOn", 318) })
        {
            var field = options.GetType().GetField(name, Fields)!;
            Click(options, 1100, y); Check(!(bool)field.GetValue(options)!, name + " OFF");
            Click(options, 940, y); Check((bool)field.GetValue(options)!, name + " ON");
            Click(options, 940, y); Check((bool)field.GetValue(options)!, name + " ON stays ON");
        }
        var originalResolution = PcDisplay.Current.Resolution;
        var size = PcDisplay.Resolutions[originalResolution];
        var font = (Microsoft.Xna.Framework.Graphics.SpriteFont)assembly.GetType("Helicopter.Global", true)!.GetField("menuFont")!.GetValue(null)!;
        var text = $"< {size.X} x {size.Y} >";
        var measure = font.MeasureString(text);
        var labelScale = Math.Min(56 / measure.Y, 510 / measure.X);
        Click(options, (int)(960 - measure.X * labelScale / 2 + 3), 469);
        Check(PcDisplay.Current.Resolution == (originalResolution + PcDisplay.Resolutions.Length - 1) % PcDisplay.Resolutions.Length, "resolution left arrow");
        Click(options, 960, 469); Check(PcDisplay.Current.Resolution == originalResolution, "resolution value advances");
        Click(options, (int)(960 + measure.X * labelScale / 2 - 3), 469);
        Check(PcDisplay.Current.Resolution == (originalResolution + 1) % PcDisplay.Resolutions.Length, "resolution right arrow");
        PcDisplay.Change(false, -1);
        var originalFullscreen = PcDisplay.Current.Fullscreen;
        Click(options, 960, 402); Check(PcDisplay.Current.Fullscreen != originalFullscreen, "display value changes mode");
        Click(options, 960, 402); Check(PcDisplay.Current.Fullscreen == originalFullscreen, "display value restores mode");
        var start = stage.GetType().GetField("startingIndex", Fields)!;
        Click(stage, 1210, 280); Check((int)start.GetValue(stage)! == 1, "stage right page 1");
        Click(stage, 1210, 280); Check((int)start.GetValue(stage)! == 2, "stage right page 2");
        Snapshot(1210, 280); PcInput.StageArrows(stage);
        Check(!(bool)inputFields.Single(f => f.Name == "mouseActionConsumed").GetValue(null)!, "hidden right arrow has no target");
        Click(stage, 70, 280); Check((int)start.GetValue(stage)! == 1, "stage left page 1");
        Click(stage, 70, 280); Check((int)start.GetValue(stage)! == 0, "stage left page 0");
        Snapshot(70, 280); PcInput.StageArrows(stage);
        Check(!(bool)inputFields.Single(f => f.Name == "mouseActionConsumed").GetValue(null)!, "hidden left arrow has no target");
        PcInput.End();
        log("Mouse menu integration PASS (synthetic input; physical mouse not tested).");
    }
}
