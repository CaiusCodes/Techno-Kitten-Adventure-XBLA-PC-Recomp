using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tka.Installer;

internal static class PortableLayout
{
    public static string Resources(string root) => Path.Combine(root, "resources");
    public static string Data(string root) => Path.Combine(Resources(root), "data");
    // These are bootstrap essentials, not an expensive per-launch content scan.
    internal static readonly string[] EssentialData = ["Helicopter.dll", "tka-install.json", "Content/font.xnb", "Content/Fonts/MenuFont.xnb", "Content/Graphics/cats.xnb",
        "Content/Effects/effect0.xnb","Content/Effects/effect1.xnb","Content/Effects/effect2.xnb","Content/Effects/effect3.xnb","Content/Effects/effect4.xnb",
        "Content/Music/newXactProject.xgs","Content/Music/Sound Bank.xsb","Content/Music/Wave Bank.xwb","Content/Music/MenuSong.wma"];
    public static bool Ready(string root)
    {
        if (!EssentialData.All(name => File.Exists(Path.Combine(Data(root),name))) ||
            !new[] { "Techno Kitten Adventure.exe", "Techno Kitten Adventure.dll", "MonoGame.Framework.dll", "coreclr.dll" }.All(name => File.Exists(Path.Combine(Resources(root),"game",name)))) return false;
        try
        {
            var marker = Path.Combine(Data(root),"tka-install.json");
            if (new FileInfo(marker).Length > 65536) return false;
            using var json = JsonDocument.Parse(File.ReadAllText(marker));
            return json.RootElement.TryGetProperty("package_sha256",out var hash) && hash.GetString() == StfsPackage.ExpectedHash;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return false; }
    }

    public static void RequireOwned(string data)
    {
        if (!Directory.Exists(data)) return;
        EnsureRegularTree(data);
        var marker = Path.Combine(data, "tka-install.json");
        if (File.Exists(marker))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(marker));
            if (json.RootElement.TryGetProperty("package_sha256", out var hash) && hash.GetString() == StfsPackage.ExpectedHash) return;
        }
        // A missing marker can be recovered if the verified retarget report
        // independently proves which assembly was imported.
        var report = Path.Combine(data, "Helicopter.dll.retarget.json");
        if (File.Exists(report) && File.Exists(Path.Combine(data, "Helicopter.dll")))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(report));
            if (json.RootElement.GetProperty("input_sha256").GetString() == "12C05F08878273BB6BF9C006D379F81C81AAF07FCF32EB3AF2DCDBB1FAB6848D" &&
                json.RootElement.GetProperty("output_sha256").GetString() == Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(data,"Helicopter.dll"))))) return;
        }
        throw new IOException("The existing data folder isn't a recognised Techno Kitten Adventure installation. Use a separate portable folder.");
    }

    public static void EnsureRegularTree(string directory)
    {
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Redirected installation paths aren't supported.");
        foreach (var child in Directory.EnumerateFileSystemEntries(directory))
        {
            var attributes = File.GetAttributes(child);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Redirected installation paths aren't supported.");
            if ((attributes & FileAttributes.Directory) != 0) EnsureRegularTree(child);
        }
    }

    public static void CopyTree(string source, string destination, CancellationToken cancel)
    {
        EnsureRegularTree(source);
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancel.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, false);
        }
    }

    public static bool Migrate(string root, Action<string> log)
    {
        var data = Data(root);
        if (Directory.Exists(data)) return false;
        foreach (var source in new[] { Path.Combine(root, "Game"), Path.Combine(root, "runtime") })
        {
            if (!Directory.Exists(source)) continue;
            try
            {
                RequireOwned(source);
                if (!EssentialData.All(name => File.Exists(Path.Combine(source,name)))) continue;
                RuntimeManifest.Validate(root,CancellationToken.None);
                var stage = Path.Combine(Resources(root), ".migration-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(stage);
                foreach (var name in new[] { "Helicopter.dll", "Helicopter.dll.retarget.json", "tka-install.json" })
                    if (File.Exists(Path.Combine(source,name))) File.Copy(Path.Combine(source,name),Path.Combine(stage,name));
                CopyTree(Path.Combine(source,"Content"),Path.Combine(stage,"Content"),CancellationToken.None);
                if (Directory.Exists(Path.Combine(source,"userdata"))) CopyTree(Path.Combine(source,"userdata"),Path.Combine(stage,"userdata"),CancellationToken.None);
                Directory.Move(stage,data);
                log("Migrated recognised imported assets and userdata; original installation retained: " + source);
                return true;
            }
            catch (Exception error) { log("Legacy migration skipped; original retained: " + error.Message); }
        }
        return false;
    }

    public static Process StartGame(string root, string[]? arguments = null)
    {
        var data = Data(root);
        var start = new ProcessStartInfo(Path.Combine(Resources(root), "game/Techno Kitten Adventure.exe")) { UseShellExecute = false, WorkingDirectory = data };
        start.ArgumentList.Add("--game-root"); start.ArgumentList.Add(data);
        foreach (var arg in arguments ?? [Path.Combine(data,"Helicopter.dll"), Path.Combine(data,"Content"), "0", "--fixed60"]) start.ArgumentList.Add(arg);
        return Process.Start(start) ?? throw new IOException("The game could not start.");
    }
}
