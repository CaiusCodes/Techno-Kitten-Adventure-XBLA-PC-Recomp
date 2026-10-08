using Microsoft.Xna.Framework;
using Tka.Compatibility;

namespace Microsoft.Xna.Framework.GamerServices;

public sealed class GamerServicesComponent(Game game) : GameComponent(game);
public class GamerServicesNotAvailableException : Exception;
public class GuideAlreadyVisibleException : Exception;
public enum MessageBoxIcon { None, Error, Warning, Alert }
public sealed class GamerPrivileges { public bool AllowPurchaseContent => false; }
public class Gamer
{
    public static SignedInGamerCollection SignedInGamers { get; } = new();
}
public sealed class SignedInGamer : Gamer
{
    public bool IsSignedInToLive => false;
    public GamerPrivileges Privileges { get; } = new();
}
public sealed class SignedInGamerCollection
{
    private readonly SignedInGamer local = new();
    public SignedInGamer? this[PlayerIndex index] => index == PlayerIndex.One ? local : null;
}
public static class Guide
{
    public static bool IsVisible => false;
    public static bool IsTrialMode => LocalServices.IsTrialMode;
    public static void ShowMarketplace(PlayerIndex player)
    {
        LocalServices.Log("Unsupported Xbox Marketplace requested.");
        throw new GamerServicesNotAvailableException();
    }
    public static IAsyncResult BeginShowMessageBox(string title, string text, IEnumerable<string> buttons,
        int focusButton, MessageBoxIcon icon, AsyncCallback? callback, object? state)
    {
        LocalServices.Log($"Unexpected Xbox guide prompt: {title}");
        throw new GamerServicesNotAvailableException();
    }
    public static IAsyncResult BeginShowMessageBox(PlayerIndex player, string title, string text,
        IEnumerable<string> buttons, int focusButton, MessageBoxIcon icon, AsyncCallback? callback, object? state)
        => BeginShowMessageBox(title, text, buttons, focusButton, icon, callback, state);
    public static int? EndShowMessageBox(IAsyncResult result) => ((CompletedResult<int?>)result).Value;
}
