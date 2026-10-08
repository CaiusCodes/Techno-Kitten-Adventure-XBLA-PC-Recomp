using SharpDX.Direct3D;
using SharpDX.Direct3D11;

internal static class EffectValidator
{
    public static int Validate(string directory)
    {
        using var device = new Device(DriverType.Hardware, DeviceCreationFlags.Debug);
        using var info = device.QueryInterface<InfoQueue>();
        Console.WriteLine("D3D feature level: " + device.FeatureLevel);
        var failed = false;
        foreach (var file in Directory.EnumerateFiles(directory, "*.mgfxo").Order())
        {
            info.ClearStoredMessages();
            var bytes = File.ReadAllBytes(file);
            var offset = bytes.AsSpan().IndexOf("DXBC"u8);
            if (offset < 0) throw new InvalidDataException("DXBC not found.");
            var length = BitConverter.ToInt32(bytes, offset + 24);
            try { using var shader = new PixelShader(device, bytes.AsSpan(offset, length).ToArray()); Console.WriteLine(Path.GetFileName(file) + " OK"); }
            catch (Exception exception) { Console.WriteLine(Path.GetFileName(file) + ": " + exception.Message); failed = true; }
            for (var index = 0L; index < info.NumStoredMessages; index++) Console.WriteLine(info.GetMessage(index).Description);
        }
        return failed ? 1 : 0;
    }
}
