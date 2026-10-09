using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tka.Installer;

// The public ZIP contains only Setup, README and notices. Setup carries the
// asset-free runtime archive and verifies every file before staging the game.
internal static class InstallerPayload
{
    public static void Extract(string destination, CancellationToken cancel)
    {
        using var source = typeof(InstallerPayload).Assembly.GetManifestResourceStream("Tka.RuntimePayload")
            ?? throw new InvalidDataException("Setup has no embedded runtime payload.");
        using var archive = new ZipArchive(source, ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("release-manifest.json")
            ?? throw new InvalidDataException("Setup has no release manifest.");
        if (manifestEntry.Length is < 1 or > 4_000_000) throw new InvalidDataException("Invalid release manifest size.");
        using var manifestStream = manifestEntry.Open();
        using var document = JsonDocument.Parse(manifestStream);
        var manifest = document.RootElement;
        var version = FileVersionInfo.GetVersionInfo(Environment.ProcessPath!).ProductVersion;
        if (manifest.GetProperty("format").GetInt32() != 2 ||
            manifest.GetProperty("assets_included").GetBoolean() ||
            manifest.GetProperty("version").GetString() != version)
            throw new InvalidDataException("Setup and embedded runtime versions disagree.");
        var files = manifest.GetProperty("files");
        if (files.GetArrayLength() is < 1 or > 1000 || archive.Entries.Count != files.GetArrayLength() + 1)
            throw new InvalidDataException("Unexpected embedded runtime inventory.");
        var root = Path.GetFullPath(destination);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in files.EnumerateArray())
        {
            cancel.ThrowIfCancellationRequested();
            var name = item.GetProperty("path").GetString() ?? "";
            if (!seen.Add(name) || name.Contains('\\') || name.Contains(':') ||
                name.Split('/').Any(part => part is "" or "." or "..") ||
                name != "Techno Kitten Adventure.exe" && !name.StartsWith("resources/", StringComparison.Ordinal))
                throw new InvalidDataException("Invalid embedded runtime path.");
            var entry = archive.GetEntry(name) ?? throw new InvalidDataException("Missing embedded runtime file: " + name);
            var length = item.GetProperty("bytes").GetInt64();
            var expected = item.GetProperty("sha256").GetString();
            if (length < 0 || entry.Length != length || expected is null || expected.Length != 64)
                throw new InvalidDataException("Invalid embedded runtime file: " + name);
            var target = Path.GetFullPath(Path.Combine(root, name.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Embedded runtime path leaves Game.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[131072];
            long count = 0;
            int read;
            while ((read = input.Read(buffer)) > 0)
            {
                cancel.ThrowIfCancellationRequested();
                count += read;
                if (count > length) throw new InvalidDataException("Embedded runtime file is oversized: " + name);
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
            }
            if (count != length || Convert.ToHexString(hash.GetHashAndReset()) != expected)
                throw new InvalidDataException("Embedded runtime hash mismatch: " + name);
        }
        if (archive.Entries.Any(entry => entry.FullName != "release-manifest.json" && !seen.Contains(entry.FullName)))
            throw new InvalidDataException("Unexpected embedded runtime file.");
        File.WriteAllText(Path.Combine(root, "release-manifest.json"), manifest.GetRawText());
    }
}
