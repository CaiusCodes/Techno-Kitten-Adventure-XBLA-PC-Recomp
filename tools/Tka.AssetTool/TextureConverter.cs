using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class TextureConverter
{
    public static int Convert(string inspection, string output)
    {
        if (Directory.Exists(output)) throw new IOException("Output already exists.");
        Directory.CreateDirectory(output);
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(inspection, "inventory.json")));
        var report = new List<object>();
        foreach (var entry in inventory.RootElement.EnumerateArray())
        {
            var readerName = entry.GetProperty("readers")[0].GetString()!;
            var font = readerName.StartsWith("Microsoft.Xna.Framework.Content.SpriteFontReader,", StringComparison.Ordinal);
            if (!font && !readerName.StartsWith("Microsoft.Xna.Framework.Content.Texture2DReader,", StringComparison.Ordinal)) continue;
            if (entry.GetProperty("platform").GetString() != "x") throw new InvalidDataException("Expected an Xbox texture.");
            var relative = entry.GetProperty("file").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(inspection, relative + ".body"));
            var originalHash = System.Convert.ToHexString(SHA256.HashData(bytes));
            var offset = entry.GetProperty("payloadOffset").GetInt32();
            using var reader = new BinaryReader(new MemoryStream(bytes));
            reader.BaseStream.Position = offset;
            if (font && reader.Read7BitEncodedInt() != 2) throw new InvalidDataException("Unexpected embedded font texture reader.");
            var format = reader.ReadInt32();
            var width = reader.ReadInt32(); var height = reader.ReadInt32(); var mips = reader.ReadInt32();
            if (format != (font ? 5 : 0) || width is < 1 or > 8192 || height is < 1 or > 8192 || mips is < 1 or > 14) throw new InvalidDataException("Unsupported texture layout.");
            var levels = new List<object>();
            for (var mip = 0; mip < mips; mip++)
            {
                var length = reader.ReadInt32();
                var w = Math.Max(1, width >> mip); var h = Math.Max(1, height >> mip);
                var expected = font ? checked(((w + 3) / 4) * ((h + 3) / 4) * 16) : checked(w * h * 4);
                if (length != expected) throw new InvalidDataException("Texture size mismatch.");
                var at = checked((int)reader.BaseStream.Position);
                if (length > bytes.Length - at) throw new InvalidDataException("Truncated texture.");
                // XNA Color's packed uint is ABGR. Xbox writes that uint in
                // big-endian order; desktop Color expects little-endian RGBA.
                var unit = font ? 2 : 4; // Xbox DXT5 uses 8-in-16 byte swapping.
                for (var pixel = at; pixel < at + length; pixel += unit) Array.Reverse(bytes, pixel, unit);
                reader.BaseStream.Position += length;
                levels.Add(new { mip, body_offset = at, length });
            }
            if (!font && reader.BaseStream.Position != bytes.Length) throw new InvalidDataException("Unexpected trailing texture data.");
            var targetPath = Path.GetFullPath(Path.Combine(output, relative));
            if (!targetPath.StartsWith(Path.GetFullPath(output) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid texture path.");
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using (var writer = new BinaryWriter(File.Create(targetPath)))
            {
                writer.Write(Encoding.ASCII.GetBytes("XNBw")); writer.Write((byte)5); writer.Write((byte)1);
                writer.Write(10 + bytes.Length); writer.Write(bytes);
            }
            report.Add(new { file = relative, original_body_sha256 = originalHash, converted_body_sha256 = System.Convert.ToHexString(SHA256.HashData(bytes)), width, height, levels });
        }
        if (report.Count != 68) throw new InvalidDataException("Expected 66 color textures and two font textures.");
        File.WriteAllText(Path.Combine(output, "texture-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Converted 66 Xbox Color textures and two DXT5 font textures to desktop byte order.");
        return 0;
    }
}
