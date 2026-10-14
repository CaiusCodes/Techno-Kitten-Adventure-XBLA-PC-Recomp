using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tka.Installer;

internal static class RuntimeManifest
{
    // Full verification belongs to import/migration, never the normal launch path.
    public static void Validate(string root,CancellationToken cancel)
    {
        PortableLayout.EnsureRegularTree(PortableLayout.Resources(root));
        var manifestPath = Path.Combine(PortableLayout.Resources(root),"release-manifest.json");
        if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 4_000_000)
            throw new InvalidDataException("Runtime files are incomplete. Extract the complete portable download, keeping resources/data to preserve your saves.");
        using var json = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifest = json.RootElement;
        if (manifest.GetProperty("format").GetInt32() != 3 || manifest.GetProperty("assets_included").GetBoolean() ||
            manifest.GetProperty("version").GetString() != FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion)
            throw new InvalidDataException("The runtime manifest doesn't match this launcher version.");
        var files = manifest.GetProperty("files");
        if (files.GetArrayLength() is < 1 or > 2000) throw new InvalidDataException("Invalid runtime inventory.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in files.EnumerateArray())
        {
            cancel.ThrowIfCancellationRequested();
            var path = item.GetProperty("path").GetString() ?? "";
            if (!seen.Add(path) || path.Contains('\\') || path.Contains(':') || path.Split('/').Any(part => part is "" or "." or "..") ||
                path != "Techno Kitten Adventure.exe" && !new[] { "resources/game/","resources/setup/","resources/compiler/","resources/translator/" }.Any(path.StartsWith))
                throw new InvalidDataException("Invalid runtime path.");
            using var stream = new FileStream(Path.Combine(root,path),FileMode.Open,FileAccess.Read,FileShare.Read);
            if (stream.Length != item.GetProperty("bytes").GetInt64() ||
                Convert.ToHexString(SHA256.HashData(stream)) != item.GetProperty("sha256").GetString())
                throw new InvalidDataException("A runtime file is missing or changed. Extract the complete portable download again; your imported data and saves can be kept.");
        }
    }
}
