using System.Security.Cryptography;

namespace Tka.Installer;

internal static class LauncherPayload
{
    public static byte[] Read()
    {
        using var source = typeof(LauncherPayload).Assembly.GetManifestResourceStream("Tka.PlayLauncher")
            ?? throw new InvalidDataException("The installer has no embedded play launcher.");
        using var bytes = new MemoryStream(); source.CopyTo(bytes); return bytes.ToArray();
    }

    public static bool CanReplace(string file, byte[] current)
    {
        using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        // Independently hashed native launchers shipped in the two runtime-layout
        // releases. Only these known predecessors or this exact payload may be replaced.
        return hash == Convert.ToHexString(SHA256.HashData(current)) || hash is
            "CAF82A27E33FF6CED33D9EECE55C9775FDF2CF0CFEDA295B9DE5E5737E37F93F" or
            "9EA0B4C71E4E767DD5CD91FFEFF8C066AB19DB8911A3075046DEA7AEE16A6664" or
            "7E0521168FBFE1709AE3BEBBFA655538FA8C8244224323C712D8283F712924E7" or
            "1562F47355AFF16C5CA6EB2E8874C39FDE54A1D52C0D9806CE89FFC6F5E08615";
    }
}
