using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

internal static class XboxEffects
{
    // Only operates on private decompressed XNB bodies. Every offset is validated
    // against the containing shader, using the XenosRecomp ShaderContainer schema.
    public static int Extract(string directory, string output)
    {
        if (Directory.Exists(output)) throw new IOException("Output already exists.");
        Directory.CreateDirectory(output);
        var manifest = new List<object>();
        foreach (var file in Directory.EnumerateFiles(directory, "effect*.xnb.body").Order())
        {
            var body = File.ReadAllBytes(file);
            using var reader = new BinaryReader(new MemoryStream(body));
            if (reader.Read7BitEncodedInt() != 1 || !reader.ReadString().StartsWith("Microsoft.Xna.Framework.Content.EffectReader,", StringComparison.Ordinal) ||
                reader.ReadInt32() != 0 || reader.Read7BitEncodedInt() != 0 || reader.Read7BitEncodedInt() != 1)
                throw new InvalidDataException("Unexpected effect XNB body.");
            var length = reader.ReadInt32();
            var effectOffset = (int)reader.BaseStream.Position;
            var effect = reader.ReadBytes(length);
            if (effect.Length != length || reader.BaseStream.Position != body.Length ||
                !effect.AsSpan(0, 4).SequenceEqual(new byte[] { 0xBC, 0xF0, 0x0B, 0xCF }))
                throw new InvalidDataException("Invalid Xbox effect container.");
            var matches = new List<int>();
            for (var i = 0; i <= effect.Length - 36; i += 4)
                if (U32(effect, i) == 0x102A1100) matches.Add(i);
            if (matches.Count != 1) throw new InvalidDataException("Expected one pixel shader container.");
            var offset = matches[0];
            var virtualSize = checked((int)U32(effect, offset + 4));
            var physicalSize = checked((int)U32(effect, offset + 8));
            var shader = effect.AsSpan(offset, checked(virtualSize + physicalSize)).ToArray();
            var table = checked((int)U32(shader, 16) + 4);
            var count = checked((int)U32(shader, table + 12));
            var info = checked(table + (int)U32(shader, table + 16));
            if (count > 64) throw new InvalidDataException("Unreasonable constant count.");
            var parameters = new List<object>();
            for (var i = 0; i < count; i++)
            {
                var entry = info + i * 20;
                var name = CString(shader, checked(table + (int)U32(shader, entry)));
                var type = checked(table + (int)U32(shader, entry + 12));
                parameters.Add(new { name, register_set = U16(shader, entry + 4), register_index = U16(shader, entry + 6),
                    register_count = U16(shader, entry + 8), parameter_class = U16(shader, type),
                    parameter_type = U16(shader, type + 2), rows = U16(shader, type + 4), columns = U16(shader, type + 6) });
            }
            var nameStem = Path.GetFileName(file).Replace(".xnb.body", "", StringComparison.Ordinal);
            File.WriteAllBytes(Path.Combine(output, nameStem + ".xenos"), shader);
            manifest.Add(new { file = Path.GetFileName(file), body_sha256 = Convert.ToHexString(SHA256.HashData(body)),
                effect_body_offset = effectOffset, shader_effect_offset = offset, virtualSize, physicalSize, parameters });
        }
        if (manifest.Count != 5) throw new InvalidDataException("Expected five effects.");
        File.WriteAllText(Path.Combine(output, "shader-manifest.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Extracted five Xbox shader containers with constant metadata.");
        return 0;
    }

    private static uint U32(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at, 4));
    private static ushort U16(byte[] bytes, int at) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(at, 2));
    private static string CString(byte[] bytes, int at)
    {
        var end = Array.IndexOf(bytes, (byte)0, at);
        if (end < at) throw new InvalidDataException("Unterminated constant name.");
        return Encoding.ASCII.GetString(bytes, at, end - at);
    }
}
