using Microsoft.Xna.Framework;
using Tka.Compatibility;

namespace Microsoft.Xna.Framework.Storage;

public sealed class StorageDevice
{
    public bool IsConnected => true;
    public static IAsyncResult BeginShowSelector(int bytes, int directories, AsyncCallback? callback, object? state)
        => new CompletedResult<StorageDevice>(new StorageDevice(), state, callback);
    public static IAsyncResult BeginShowSelector(PlayerIndex player, int bytes, int directories, AsyncCallback? callback, object? state)
        => BeginShowSelector(bytes, directories, callback, state);
    public static StorageDevice EndShowSelector(IAsyncResult result) => ((CompletedResult<StorageDevice>)result).Value;
    public IAsyncResult BeginOpenContainer(string name, AsyncCallback? callback, object? state)
        => new CompletedResult<StorageContainer>(new StorageContainer(name), state, callback);
    public StorageContainer EndOpenContainer(IAsyncResult result) => ((CompletedResult<StorageContainer>)result).Value;
}

public sealed class StorageContainer : IDisposable
{
    private readonly string root;
    internal StorageContainer(string name)
    {
        // Only this title's independently verified container name is supported.
        if (name != "Techno Kitten Adventure") throw new NotSupportedException($"Unknown container: {name}");
        root = Path.GetFullPath(Path.Combine(LocalServices.StorageRoot, "profile", name));
        Directory.CreateDirectory(root);
    }
    private string Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
            throw new IOException("Invalid save filename.");
        return Path.Combine(root, name);
    }
    public bool FileExists(string name) => File.Exists(Resolve(name));
    public void DeleteFile(string name) => File.Delete(Resolve(name));
    public Stream CreateFile(string name) => File.Create(Resolve(name));
    public Stream OpenFile(string name, FileMode mode) => File.Open(Resolve(name), mode);
    public void Dispose() { }
}
