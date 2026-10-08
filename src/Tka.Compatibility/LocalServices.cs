using Microsoft.Xna.Framework;

namespace Tka.Compatibility;

public static class LocalServices
{
    public static string StorageRoot { get; set; } = Path.Combine(AppContext.BaseDirectory, "userdata");
    public static Action<string> Log { get; set; } = Console.WriteLine;
    // Normal PC play uses the full game. Trial is an explicit regression-test
    // option only; it is not inferred from the Indie Games package title ID.
    public static bool IsTrialMode { get; set; }
}

internal sealed class CompletedResult<T> : IAsyncResult
{
    public T Value { get; }
    public object? AsyncState { get; }
    public WaitHandle AsyncWaitHandle { get; } = new ManualResetEvent(true);
    public bool CompletedSynchronously => true;
    public bool IsCompleted => true;
    public CompletedResult(T value, object? state, AsyncCallback? callback)
    {
        Value = value;
        AsyncState = state;
        callback?.Invoke(this);
    }
}
