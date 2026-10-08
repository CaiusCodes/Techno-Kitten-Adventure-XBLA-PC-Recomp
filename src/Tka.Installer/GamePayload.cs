using System.Security.Cryptography;
using System.Text.Json;

namespace Tka.Installer;

// Store identical runtime files once in the download, materialize a complete
// standalone Game directory in transaction staging. No installed DLL moves.
internal static class GamePayload
{
    public sealed record Entry(string Path, string Sha256, long Bytes);
    public static void RestoreShared(string resources, string destination, CancellationToken cancel)
    {
        var manifest = System.IO.Path.Combine(resources, "game-shared-files.json");
        if (!File.Exists(manifest)) throw new FileNotFoundException("Installer resources are incomplete. Extract the entire download.", manifest);
        var entries = JsonSerializer.Deserialize<Entry[]>(File.ReadAllText(manifest)) ?? throw new InvalidDataException("Invalid shared runtime manifest.");
        var shared = System.IO.Path.GetFullPath(System.IO.Path.Combine(resources, "installer"));
        var targetRoot = System.IO.Path.GetFullPath(destination);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            cancel.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.Path) || System.IO.Path.IsPathRooted(entry.Path) || entry.Path.Contains(':') || !seen.Add(entry.Path))
                throw new InvalidDataException("Invalid shared runtime path.");
            var source = Child(shared, entry.Path);
            var target = Child(targetRoot, entry.Path);
            using (var stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length != entry.Bytes || Convert.ToHexString(SHA256.HashData(stream)) != entry.Sha256)
                    throw new InvalidDataException("Installer runtime file is damaged: " + entry.Path);
                stream.Position = 0;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.CopyTo(output);
            }
        }
    }
    private static string Child(string root, string relative)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
        if (!path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Shared runtime path leaves its folder.");
        for (var item = path; item != root; item = System.IO.Path.GetDirectoryName(item)!)
            if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Shared runtime paths must not be redirected.");
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Runtime folder is redirected.");
        return path;
    }
}
