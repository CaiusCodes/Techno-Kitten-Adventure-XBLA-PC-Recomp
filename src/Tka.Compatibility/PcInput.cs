using System.Collections;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Tka.Compatibility;

// Desktop input is sampled alongside the original InputState, once per frame.
// Original controller and keyboard results are preserved by the IL bridges.
public static class PcInput
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static Game? game;
    private static FieldInfo? gameState;
    private static MouseState currentMouse, previousMouse;
    private static KeyboardState currentKeys, previousKeys;
    private static Point previousPosition;
    private static int mouseDirection;
    private static bool mouseActionConsumed;

    public static void Initialize(Game owner)
    {
        var type = owner.GetType();
        while (type != null && type.FullName != "Helicopter.Game1") type = type.BaseType;
        if (type is null) throw new InvalidDataException("Unsupported game input owner.");
        game = owner;
        gameState = type.GetField("gameState", Fields)
            ?? throw new InvalidDataException("Game state field is missing.");
        if (gameState.FieldType.FullName != "Helicopter.GameState") throw new InvalidDataException("Game state signature changed.");
        currentMouse = previousMouse = Mouse.GetState();
        currentKeys = previousKeys = Keyboard.GetState();
        previousPosition = new Point(currentMouse.X, currentMouse.Y);
        owner.IsMouseVisible = true;
    }

    public static void Sample()
    {
        mouseDirection = 0;
        mouseActionConsumed = false;
        currentMouse = Mouse.GetState();
        currentKeys = Keyboard.GetState();
        // Keep the pointer available for the title and native menus, including
        // pause, while leaving active flight unobstructed.
        var showCursor = !Playing;
        if (game is not null && game.IsMouseVisible != showCursor)
            game.IsMouseVisible = showCursor;
    }

    public static void End()
    {
        previousMouse = currentMouse;
        previousKeys = currentKeys;
    }

    private static bool Active => game?.IsActive == true;
    private static bool Opening => gameState?.GetValue(game)?.ToString() == "OPENING";
    private static bool Playing => gameState?.GetValue(game)?.ToString() == "PLAY";
    private static bool KeyPressed(Keys key) => Active && currentKeys.IsKeyDown(key) && previousKeys.IsKeyUp(key);
    private static bool KeyDown(Keys key) => Active && currentKeys.IsKeyDown(key);
    private static bool LeftPressed => Active && currentMouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
    private static bool LeftDown => Active && currentMouse.LeftButton == ButtonState.Pressed;
    private static bool RightPressed => Active && currentMouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released;
    private static bool RightDown => Active && currentMouse.RightButton == ButtonState.Pressed;

    // Buttons values are verified from the original game's InputState switch:
    // DPad up/down/left/right 1/2/4/8, Start 16, A 4096, B 8192.
    public static bool Pressed(bool original, Buttons button) => original || ((int)button switch
    {
        1 => KeyPressed(Keys.W), 2 => KeyPressed(Keys.S),
        4 => KeyPressed(Keys.A) || mouseDirection == -1, 8 => KeyPressed(Keys.D) || mouseDirection == 1,
        16 => Playing ? KeyPressed(Keys.Escape) : KeyPressed(Keys.Enter) || (Opening && LeftPressed),
        4096 => LeftPressed && !mouseActionConsumed,
        8192 => RightPressed || (!Playing && KeyPressed(Keys.Escape)),
        _ => false
    });

    public static bool Down(bool original, Buttons button) => original || ((int)button switch
    {
        1 => KeyDown(Keys.W), 2 => KeyDown(Keys.S),
        4 => KeyDown(Keys.A), 8 => KeyDown(Keys.D),
        16 => Playing ? KeyDown(Keys.Escape) : KeyDown(Keys.Enter) || (Opening && LeftPressed),
        4096 => LeftDown && !mouseActionConsumed,
        8192 => RightDown || (!Playing && KeyDown(Keys.Escape)),
        _ => false
    });

    public static bool Up(bool original, Buttons button)
    {
        if (!Active) return original;
        return (int)button switch
        {
            1 => original && !KeyDown(Keys.W), 2 => original && !KeyDown(Keys.S),
            4 => original && !KeyDown(Keys.A), 8 => original && !KeyDown(Keys.D),
            16 => original && !(Playing ? KeyDown(Keys.Escape) : KeyDown(Keys.Enter)),
            4096 => original && !LeftDown,
            8192 => original && !RightDown && (Playing || !KeyDown(Keys.Escape)),
            _ => original
        };
    }

    // Called after the verified base Menu.Update body, before the derived menu's
    // action callbacks. The exact Menu/MenuItem field and CollisionRect signatures
    // are checked; the original index is changed only when the pointer moves or clicks.
    public static void MenuHover(object menu)
    {
        if (!Active || game is null || Playing) return;
        var mouse = new Point(currentMouse.X, currentMouse.Y);
        if (mouse == previousPosition && !LeftPressed) return;
        previousPosition = mouse;
        if (!TryLogicalMouse(out var point)) return;
        var x = point.X;
        var y = point.Y;
        var type = menu.GetType();
        var baseType = type.BaseType;
        if (baseType?.FullName != "Helicopter.Menu") throw new InvalidDataException("Unsupported menu owner.");
        var items = (IList)(baseType.GetField("menuItems_", Fields)?.GetValue(menu)
            ?? throw new InvalidDataException("Menu items field is missing."));
        var index = baseType.GetField("index_", Fields)
            ?? throw new InvalidDataException("Menu index field is missing.");
        if (PcOptionsMenu.MouseTarget(menu, point, out var row, out var direction))
        {
            index.SetValue(menu, row);
            if (LeftPressed) { mouseDirection = direction; mouseActionConsumed = true; }
            return;
        }
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i]!;
            var property = item.GetType().GetProperty("CollisionRect", Fields);
            if (property?.PropertyType != typeof(Rectangle)) throw new InvalidDataException("Menu hitbox signature changed.");
            var rect = (Rectangle)property.GetValue(item)!;
            if (rect.Contains(x, y))
            {
                index.SetValue(menu, i);
                return;
            }
        }
    }

    private static bool TryLogicalMouse(out Point point)
    {
        point = default;
        if (game is null) return false;
        var mouse = new Point(currentMouse.X, currentMouse.Y);
        var bounds = game.Window.ClientBounds;
        if (bounds.Width < 1 || bounds.Height < 1) return false;
        var scale = Math.Min(bounds.Width / 1280d, bounds.Height / 720d);
        var width = 1280 * scale;
        var height = 720 * scale;
        var x = (mouse.X - (bounds.Width - width) / 2) / scale;
        var y = (mouse.Y - (bounds.Height - height) / 2) / scale;
        if (x < 0 || x >= 1280 || y < 0 || y >= 720) return false;
        point = new Point((int)x, (int)y);
        return true;
    }

    // StageSelectMenu tests directions before calling the base menu update.
    // Verified original arrow centres (70/1210,280), 73x106 atlas sprites,
    // and +/-5 horizontal animation. Only visible arrows consume a click.
    public static void StageArrows(object menu)
    {
        if (!LeftPressed || Playing || !TryLogicalMouse(out var point)) return;
        var type = menu.GetType();
        var baseType = type.BaseType;
        if (type.FullName != "Helicopter.StageSelectMenu" || baseType?.FullName != "Helicopter.Menu")
            throw new InvalidDataException("Unsupported stage menu signature.");
        var start = (int)type.GetField("startingIndex", Fields)!.GetValue(menu)!;
        var max = (int)type.GetField("maxIndex", Fields)!.GetValue(menu)!;
        var direction = start > 0 && new Rectangle(20, 219, 100, 122).Contains(point) ? -1
            : start < max - 2 && new Rectangle(1160, 219, 100, 122).Contains(point) ? 1 : 0;
        if (direction == 0) return;
        baseType.GetField("index_", Fields)!.SetValue(menu, direction < 0 ? 0 : 2);
        mouseDirection = direction;
        mouseActionConsumed = true;
    }
}
