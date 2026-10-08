using Microsoft.Xna.Framework;

namespace Tka.Compatibility;

public static class GameEvents
{
    // XNA used EventArgs; MonoGame 3.8.4.1 uses ExitingEventArgs.
    // Keep the game's original save-on-exit callback and adapt only its delegate.
    public static void AddExiting(Game game, EventHandler<EventArgs> handler)
        => game.Exiting += (sender, args) => handler(sender, args);
}
