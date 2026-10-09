using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Tka.Compatibility;

// These managed fields are resolved by exact name/type; no CLR memory offsets.
// The original update, boolean callbacks, Back and Credits bodies remain intact.
public static class PcOptionsMenu
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly ConditionalWeakTable<object, State> States = new();
    private sealed class State
    {
        public required FieldInfo Index;
        public required MethodInfo Pressed;
        public required SpriteFont Font;
        public int DisplayIndex, ActionIndex;
    }

    public static void Construct(object menu)
    {
        var type = menu.GetType();
        if (type.FullName != "Helicopter.OptionsMenu" || type.BaseType?.FullName != "Helicopter.Menu")
            throw new InvalidDataException("Unsupported options menu type.");
        var assembly = type.Assembly;
        var global = assembly.GetType("Helicopter.Global", true)!;
        var texture = (Texture2D)global.GetField("optionsTex")!.GetValue(null)!;
        var font = (SpriteFont)global.GetField("menuFont")!.GetValue(null)!;
        var items = (IList)type.BaseType.GetField("menuItems_", Instance)!.GetValue(menu)!;
        Rectangle[] expected = [new(0,722,190,41), new(0,765,125,41), new(0,808,412,41), new(0,851,364,54), new(0,907,236,54)];
        Vector2[] positions = [new(223,191.5f), new(190.5f,254.5f), new(334,318.5f), new(640,490), new(640,587)];
        var itemType = assembly.GetType("Helicopter.MenuItem", true)!;
        var rect = itemType.GetField("texRect_", Instance)!;
        var image = itemType.GetField("texture_", Instance)!;
        var position = itemType.GetField("position_", Instance)!;
        if (items.Count != expected.Length || items.Cast<object>().Where((item, i) =>
            (Rectangle)rect.GetValue(item)! != expected[i] || (Vector2)position.GetValue(item)! != positions[i] || !ReferenceEquals(image.GetValue(item), texture)).Any())
            throw new InvalidDataException("Original options atlas/item signature mismatch.");
        position.SetValue(items[3], new Vector2(640, 558));
        position.SetValue(items[4], new Vector2(640, 646));
        foreach (var (text, index, y) in new[] { ("DISPLAY", 3, 402f), ("RESOLUTION", 4, 469f) })
        {
            var label = MakeLabel(texture.GraphicsDevice, font, text);
            var item = Activator.CreateInstance(itemType, label, new Rectangle(0, 0, label.Width, label.Height), new Vector2(128 + label.Width / 2, y))!;
            items.Insert(index, item);
        }
        States.Add(menu, new State { Index = type.BaseType.GetField("index_", Instance)!, Font = font,
            Pressed = assembly.GetType("Helicopter.InputState", true)!.GetMethod("IsButtonPressed", [typeof(Buttons)])! });
        LocalServices.Log("Native options verified: original atlas/positions; added Display and Resolution rows.");
    }

    private static Texture2D MakeLabel(GraphicsDevice device, SpriteFont font, string text)
    {
        var measure = font.MeasureString(text);
        var scale = 64f / measure.Y;
        var label = new RenderTarget2D(device, (int)Math.Ceiling(measure.X * scale) + 5, 69);
        var targets = device.GetRenderTargets();
        var viewport = device.Viewport;
        using var batch = new SpriteBatch(device);
        device.SetRenderTarget(label);
        device.Clear(Color.Transparent);
        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        batch.DrawString(font, text, new Vector2(3, 4), new Color(143, 0, 65), 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        batch.DrawString(font, text, Vector2.Zero, Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        batch.End();
        device.SetRenderTargets(targets);
        device.Viewport = viewport;
        return label;
    }

    public static bool MouseTarget(object menu, Point point, out int row, out int direction)
    {
        row = direction = 0;
        if (!States.TryGetValue(menu, out var state)) return false;
        // Original ON/OFF atlas draws: x=900/1060, y=171/234/298,
        // heights 41 and widths 92/119, with eight pixels of click padding.
        for (var i = 0; i < 3; i++)
        {
            var y = new[] { 171, 234, 298 }[i];
            if (new Rectangle(892, y - 8, 108, 57).Contains(point)) { row = i; direction = -1; return true; }
            if (new Rectangle(1052, y - 8, 135, 57).Contains(point)) { row = i; direction = 1; return true; }
        }
        // Match the actual font scale and position used by DrawValues.
        var size = PcDisplay.Resolutions[PcDisplay.Current.Resolution];
        foreach (var (text, y, index) in new[] {
            (PcDisplay.Current.Fullscreen ? "< FULLSCREEN >" : "< WINDOWED >", 402, 3),
            ($"< {size.X} x {size.Y} >", 469, 4) })
        {
            var measure = state.Font.MeasureString(text);
            var scale = Math.Min(56 / measure.Y, 510 / measure.X);
            var left = 960 - measure.X * scale / 2;
            if (new Rectangle((int)left - 8, y - 36, (int)Math.Ceiling(measure.X * scale) + 16, 72).Contains(point))
            {
                row = index;
                direction = point.X < left + state.Font.MeasureString("<").X * scale + 12 ? -1 : 1;
                return true;
            }
        }
        return false;
    }

    // Called immediately after original Menu.Update navigation/animation.
    public static void BeforeActions(object menu, object input)
    {
        var state = States.GetValue(menu, _ => throw new InvalidOperationException("Unregistered options menu."));
        var index = (int)state.Index.GetValue(menu)!;
        state.DisplayIndex = index;
        state.ActionIndex = index switch { 3 => 7, 4 => 8, 5 => 3, 6 => 4, _ => index };
        if (index is 3 or 4)
        {
            bool Press(Buttons key) => (bool)state.Pressed.Invoke(input, [key])!;
            var direction = Press(Buttons.DPadLeft) ? -1 : Press(Buttons.DPadRight) || Press(Buttons.A) ? 1 : 0;
            if (direction != 0) PcDisplay.Change(index == 3, direction);
        }
        state.Index.SetValue(menu, state.ActionIndex);
    }

    // Original Credits/Back reset index to zero; preserve that reset.
    public static void AfterActions(object menu)
    {
        var state = States.GetValue(menu, _ => throw new InvalidOperationException("Unregistered options menu."));
        if ((int)state.Index.GetValue(menu)! == state.ActionIndex) state.Index.SetValue(menu, state.DisplayIndex);
    }

    public static void DrawValues(object menu, SpriteBatch batch)
    {
        var state = States.GetValue(menu, _ => throw new InvalidOperationException("Unregistered options menu."));
        var size = PcDisplay.Resolutions[PcDisplay.Current.Resolution];
        Draw(PcDisplay.Current.Fullscreen ? "< FULLSCREEN >" : "< WINDOWED >", 402, 3);
        Draw($"< {size.X} x {size.Y} >", 469, 4);
        void Draw(string text, float y, int row)
        {
            var measure = state.Font.MeasureString(text);
            var scale = Math.Min(56 / measure.Y, 510 / measure.X);
            var point = new Vector2(960 - measure.X * scale / 2, y - measure.Y * scale / 2);
            batch.DrawString(state.Font, text, point + new Vector2(3, 4), new Color(100, 0, 55), 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            batch.DrawString(state.Font, text, point, (int)state.Index.GetValue(menu)! == row ? new Color(0, 230, 255) : Color.White,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }
    }
}
