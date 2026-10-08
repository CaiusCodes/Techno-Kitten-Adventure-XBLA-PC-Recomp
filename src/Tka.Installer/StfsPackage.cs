using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tka.Installer;

internal static class StfsPackage
{
    public const string ExpectedHash = "A472E517EF37A35A5C956C6ED558FE918F8D4E0C775995188CC17671FBB50EE5";
    private sealed record Entry(int Index, string Name, bool Directory, bool Consecutive, int Blocks, int Block, int Parent, int Size);

    // Deliberately version-specific. A differently packaged release must be
    // audited before acceptance, even if its display title happens to match.
    public static void Extract(string packagePath, string output, CancellationToken cancel, Action<int, string> progress)
    {
        using var source = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length != 83353600) throw new InvalidDataException("This package version is not supported. Select the original Techno Kitten Adventure package.");
        var bytes = new byte[checked((int)source.Length)]; source.ReadExactly(bytes);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != ExpectedHash) throw new InvalidDataException("The package is different from the supported version, or is damaged. Your file has not been changed.");
        cancel.ThrowIfCancellationRequested();
        int Number(int at, int width, bool little = false)
        {
            if (at < 0 || at > bytes.Length - width) throw new InvalidDataException("Package offset outside file.");
            var value = 0;
            for (var i = 0; i < width; i++) value = checked(value * 256 + bytes[at + (little ? width - 1 - i : i)]);
            return value;
        }
        if (Encoding.ASCII.GetString(bytes, 0, 4) != "LIVE" || Number(0x344, 4) != 2 || Number(0x360, 4) != 0x584E07D2 || bytes[0x37B] != 1)
            throw new InvalidDataException("Unexpected title or package layout.");
        var firstHash = checked((Number(0x340, 4) + 4095) / 4096 * 4096);
        int DataOffset(int block)
        {
            if (block < 0 || block >= 0x70E4) throw new InvalidDataException("Invalid data block.");
            var backing = block + (block + 0xAA) / 170;
            if (block >= 0xAA) backing += (block + 0x70E4) / 28900;
            var offset = checked(firstHash + backing * 4096);
            if (offset > bytes.Length - 4096) throw new InvalidDataException("Truncated block.");
            return offset;
        }
        int Next(int block) => Number(firstHash + (block < 0xAA ? 0 : block / 170 * 0xAB + 1) * 4096 + block % 170 * 24 + 21, 3);
        if (Number(0x37C, 2, true) != 2 || Number(0x37E, 3, true) != 0) throw new InvalidDataException("Unexpected file table.");
        var entries = new Dictionary<int, Entry>();
        var tableBlock = 0;
        var tables = new HashSet<int>();
        for (var table = 0; table < 2; table++)
        {
            if (!tables.Add(tableBlock)) throw new InvalidDataException("Cyclic table.");
            var at = DataOffset(tableBlock);
            for (var slot = 0; slot < 64; slot++)
            {
                var entryAt = at + slot * 64;
                var flags = bytes[entryAt + 0x28]; var length = flags & 63;
                if (length == 0) continue;
                if (length > 40) throw new InvalidDataException("Invalid name length.");
                var name = Encoding.ASCII.GetString(bytes, entryAt, length);
                if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' ') ||
                    Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)", RegexOptions.IgnoreCase)) throw new InvalidDataException("Unsafe package filename.");
                var index = table * 64 + slot;
                entries.Add(index, new Entry(index, name, (flags & 128) != 0, (flags & 64) != 0, Number(entryAt + 0x29, 3, true),
                    Number(entryAt + 0x2F, 3, true), Number(entryAt + 0x32, 2), Number(entryAt + 0x34, 4)));
            }
            if (table == 0) tableBlock = Next(tableBlock);
        }
        if (entries.Count != 120 || Directory.Exists(output)) throw new InvalidDataException("Unexpected inventory or occupied staging directory.");
        Directory.CreateDirectory(output);
        var root = Path.GetFullPath(output) + Path.DirectorySeparatorChar;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifest = new List<object>();
        foreach (var entry in entries.Values.OrderBy(e => e.Index))
        {
            cancel.ThrowIfCancellationRequested();
            var parts = new List<string> { entry.Name }; var parent = entry.Parent; var parents = new HashSet<int>();
            while (parent != 65535)
            {
                if (!parents.Add(parent) || !entries.TryGetValue(parent, out var folder) || !folder.Directory) throw new InvalidDataException("Invalid package directory tree.");
                parts.Insert(0, folder.Name); parent = folder.Parent;
            }
            var relative = Path.Combine(parts.ToArray());
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !seen.Add(relative)) throw new InvalidDataException("Duplicate or escaping filename.");
            if (entry.Directory) { Directory.CreateDirectory(target); continue; }
            if (entry.Blocks != (entry.Size + 4095) / 4096) throw new InvalidDataException("Invalid file length.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using (var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var remaining = entry.Size; var block = entry.Block; var chain = new HashSet<int>();
                while (remaining > 0)
                {
                    if (!chain.Add(block)) throw new InvalidDataException("Cyclic data chain.");
                    var count = Math.Min(remaining, 4096); file.Write(bytes, DataOffset(block), count); remaining -= count;
                    if (remaining > 0) block = entry.Consecutive ? block + 1 : Next(block);
                }
                file.Flush(true);
            }
            manifest.Add(new { path = relative, bytes = entry.Size, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target))) });
            progress(5 + manifest.Count * 25 / 97, $"Unpacking your game… {manifest.Count}/97");
        }
        if (manifest.Count != 97 || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(output, "584E07D1/Helicopter.exe")))) !=
            "12C05F08878273BB6BF9C006D379F81C81AAF07FCF32EB3AF2DCDBB1FAB6848D") throw new InvalidDataException("Extracted game failed verification.");
        File.WriteAllText(Path.Combine(output, "extraction-manifest.json"), JsonSerializer.Serialize(new { package_sha256 = ExpectedHash, file_count = 97, files = manifest }));
    }
}
