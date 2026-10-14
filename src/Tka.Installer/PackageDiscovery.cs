namespace Tka.Installer;

internal static class PackageDiscovery
{
    public static List<string> Nearby(string root, CancellationToken cancel) => Find([root,Path.Combine(root,"Game Package")], root, cancel);
    public static List<string> FromInput(string path, CancellationToken cancel)
    {
        if (File.Exists(path)) { StfsPackage.Validate(path,cancel); return [Path.GetFullPath(path)]; }
        if (Directory.Exists(path)) return Find([path], null, cancel);
        throw new IOException("Choose an original package file or drop its containing folder here.");
    }
    private static List<string> Find(string[] roots, string? shallowRoot, CancellationToken cancel)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = 0; var candidates = 0;
        foreach (var root in roots)
        {
            if (!Directory.Exists(root)) continue;
            var pending = new Queue<(string Path,int Depth)>(); pending.Enqueue((root,0));
            while (pending.Count > 0 && visited < 256 && candidates < 128)
            {
                cancel.ThrowIfCancellationRequested(); var (directory,depth) = pending.Dequeue(); visited++;
                try
                {
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                    foreach (var file in Directory.EnumerateFiles(directory))
                    {
                        cancel.ThrowIfCancellationRequested();
                        if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || new FileInfo(file).Length != 83353600 || !seen.Add(Path.GetFullPath(file))) continue;
                        candidates++; if (candidates > 128) break;
                        try { StfsPackage.Validate(file,cancel); result.Add(Path.GetFullPath(file)); }
                        catch (InvalidDataException) { } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                    if (depth < 4 && !string.Equals(directory,shallowRoot,StringComparison.OrdinalIgnoreCase))
                        foreach (var child in Directory.EnumerateDirectories(directory)) pending.Enqueue((child,depth+1));
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        return result;
    }
}
