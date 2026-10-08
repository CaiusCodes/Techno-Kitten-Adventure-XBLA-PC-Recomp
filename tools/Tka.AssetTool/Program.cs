using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;

return AssetCommands.Run(args);

public static class AssetCommands
{
public static int Run(string[] args)
{
try
{
    if (args.Length == 3 && args[0] == "--effects") return XboxEffects.Extract(args[1], args[2]);
    if (args.Length == 5 && args[0] == "--compile-effects") return EffectCompiler.Compile(args[1], args[2], args[3], args[4]);
    if (args.Length == 2 && args[0] == "--validate-effects") return EffectValidator.Validate(args[1]);
    if (args.Length == 3 && args[0] == "--render-effects") return EffectRenderValidator.Validate(args[1], args[2]);
    if (args.Length == 3 && args[0] == "--textures") return TextureConverter.Convert(args[1], args[2]);
    if (args.Length != 2) throw new ArgumentException("Usage: Tka.AssetTool input-content new-private-output");
    var input = Path.GetFullPath(args[0]);
    var output = Path.GetFullPath(args[1]);
    if (Directory.Exists(output)) throw new IOException("Output already exists.");
    Directory.CreateDirectory(output);
    var summary = new List<object>();
    foreach (var file in Directory.EnumerateFiles(input, "*.xnb", SearchOption.AllDirectories))
    {
        using var source = File.OpenRead(file);
        using var header = new BinaryReader(source);
        if (new string(header.ReadChars(3)) != "XNB") throw new InvalidDataException(file);
        var platform = header.ReadChar();
        var version = header.ReadByte();
        var flags = header.ReadByte();
        var size = header.ReadInt32();
        using var body = new MemoryStream();
        if ((flags & 0x80) != 0)
        {
            var length = header.ReadInt32();
            var type = typeof(Game).Assembly.GetType("MonoGame.Framework.Utilities.LzxDecoderStream", true)!;
            using var decoded = (Stream)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { source, length, size - 14 }, null)!;
            decoded.CopyTo(body);
            if (body.Length != length) throw new InvalidDataException("Decompression length mismatch.");
        }
        else source.CopyTo(body);
        var relative = Path.GetRelativePath(input, file);
        var path = Path.Combine(output, relative + ".body");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, body.ToArray());
        body.Position = 0;
        using var reader = new BinaryReader(body);
        var count = reader.Read7BitEncodedInt();
        var readers = new List<string>();
        for (var i = 0; i < count; i++) { readers.Add(reader.ReadString()); reader.ReadInt32(); }
        var shared = reader.Read7BitEncodedInt();
        var objectType = reader.Read7BitEncodedInt();
        var payloadOffset = body.Position;
        var head = reader.ReadBytes((int)Math.Min(48, body.Length - body.Position));
        summary.Add(new { file = relative, platform, version, flags, readers, shared, objectType, payloadOffset,
            payload = Convert.ToHexString(head), body_length = body.Length });
    }
    File.WriteAllText(Path.Combine(output, "inventory.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Inspected {summary.Count} XNB files: {output}");
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
}
}
