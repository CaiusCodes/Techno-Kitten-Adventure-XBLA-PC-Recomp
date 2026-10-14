using System.Diagnostics;
using System.Text.Json;

namespace Tka.Installer;

internal sealed class InstallEngine(Action<int, string> progress, Action<string> log, Action? beforePromotion = null, Action? beforeLauncherPromotion = null)
{
    public string Install(string package, string destination, CancellationToken cancel, bool unlockAll = false)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        var resources = PortableLayout.Resources(root);
        Directory.CreateDirectory(resources);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 ||
            (File.GetAttributes(resources) & FileAttributes.ReparsePoint) != 0) throw new IOException("Use a regular writable portable folder.");
        using var importLock = new FileStream(Path.Combine(resources,"import.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var game = PortableLayout.Data(root);
        var previous = game;
        PortableLayout.RequireOwned(previous);
        RuntimeManifest.Validate(root,cancel);
        StfsPackage.Validate(package, cancel);
        var staging = Path.Combine(resources, ".install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var extracted = Path.Combine(staging, "original");
        var inspection = Path.Combine(staging, "inspection");
        var shaders = Path.Combine(staging, "shaders");
        var effects = Path.Combine(staging, "effects");
        var textures = Path.Combine(staging, "textures");
        var ready = Path.Combine(staging, "data");
        string? backup = null;
        var promoted = false;
        try
        {
            progress(2, "Checking your package…");
            StfsPackage.Extract(package, extracted, cancel, progress);
            cancel.ThrowIfCancellationRequested();
            progress(32, "Preparing the Windows game…");
            Directory.CreateDirectory(ready);
            if (AssemblyRetargeter.Run([Path.Combine(extracted, "584E07D1/Helicopter.exe"), Path.Combine(ready, "Helicopter.dll")]) != 0)
                throw new InvalidDataException("The game program could not be converted.");
            var content = Path.Combine(extracted, "584E07D1/Content");
            Command([content, inspection]);
            cancel.ThrowIfCancellationRequested();
            progress(48, "Restoring the original colors and fonts…");
            Command(["--textures", inspection, textures]);
            Command(["--effects", Path.Combine(inspection, "Effects"), shaders]);
            progress(64, "Converting the visual effects…");
            foreach (var file in Directory.EnumerateFiles(shaders, "*.xenos").Order())
            {
                cancel.ThrowIfCancellationRequested();
                Run(Path.Combine(resources, "translator/XenosRecomp.exe"), [file, file + ".hlsl", Path.Combine(resources, "translator/shader_common.h")]);
            }
            EffectCompiler.CompilerPrefixArguments = ["--shader-compiler", Path.Combine(resources, "compiler/mgfxc.dll")];
            Command(["--compile-effects", Path.Combine(inspection, "Effects"), shaders, Environment.ProcessPath!, effects]);
            cancel.ThrowIfCancellationRequested();
            progress(82, "Installing your game files…");
            CopyTree(content, Path.Combine(ready, "Content"), cancel);
            foreach (var file in Directory.EnumerateFiles(textures, "*.xnb", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(ready, "Content", Path.GetRelativePath(textures, file)), true);
            foreach (var file in Directory.EnumerateFiles(effects, "*.xnb")) File.Copy(file, Path.Combine(ready, "Content/Effects", Path.GetFileName(file)), true);
            // Saves/settings are copied into the completed staging tree before
            // either rename. Existing installation remains usable until commit.
            if (Directory.Exists(Path.Combine(previous, "userdata"))) CopyTree(Path.Combine(previous, "userdata"), Path.Combine(ready, "userdata"), cancel);
            if (unlockAll) InstallerExtras.UnlockAll(ready);
            var reports = Path.Combine(ready, "userdata/cache/import"); Directory.CreateDirectory(reports);
            foreach (var report in new[] { Path.Combine(extracted, "extraction-manifest.json"), Path.Combine(effects, "conversion-report.json"),
                Path.Combine(shaders, "shader-manifest.json"), Path.Combine(textures, "texture-report.json") }) File.Copy(report, Path.Combine(reports, Path.GetFileName(report)), true);
            File.WriteAllText(Path.Combine(ready, "tka-install.json"), JsonSerializer.Serialize(new {
                title = "Techno Kitten Adventure!", package_sha256 = StfsPackage.ExpectedHash,
                installed_utc = DateTime.UtcNow, mode = "full", format = 2
            }, new JsonSerializerOptions { WriteIndented = true }));
            cancel.ThrowIfCancellationRequested();
            progress(96, "Finishing installation…");
            if (Directory.Exists(previous))
            {
                var backupRoot = Path.Combine(resources, "backups"); Directory.CreateDirectory(backupRoot);
                if ((File.GetAttributes(backupRoot) & FileAttributes.ReparsePoint) != 0) throw new IOException("Backup folders must not redirect elsewhere.");
                backup = Path.Combine(backupRoot, "Game-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
                Directory.Move(previous, backup);
                log("Previous installation retained: " + backup);
            }
            var runtimePromoted = false;
            try
            {
                beforePromotion?.Invoke();
                Directory.Move(ready, game); runtimePromoted = true;
                beforeLauncherPromotion?.Invoke();
                promoted = true;
            }
            catch
            {
                if (runtimePromoted) Directory.Move(game, ready);
                if (backup != null && !Directory.Exists(previous)) Directory.Move(backup, previous);
                throw;
            }
            progress(100, "Ready to play!");
            return Path.Combine(root, "Techno Kitten Adventure.exe");
        }
        finally
        {
            // This path is a fresh, fixed-prefix child created by this invocation.
            // Refuse recursive cleanup if a path has been redirected meanwhile.
            try
            {
                if (Path.GetDirectoryName(Path.GetFullPath(staging)) == resources &&
                    Path.GetFileName(staging).StartsWith(".install-", StringComparison.Ordinal) &&
                    Directory.Exists(staging) && !HasReparsePoint(staging)) Directory.Delete(staging, true);
                else log("Staging retained for inspection: " + staging);
            }
            catch (Exception error) { log("Staging cleanup: " + error.Message); }
            if (!promoted) log("Installation was not committed; previous game and package preserved.");
        }
    }

    private static void Command(string[] arguments)
    {
        if (AssetCommands.Run(arguments) != 0) throw new InvalidDataException("An asset conversion failed. See the installer log.");
    }
    private void Run(string executable, string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        log(stdout.GetAwaiter().GetResult()); log(stderr.GetAwaiter().GetResult());
        if (process.ExitCode != 0) throw new InvalidDataException("Shader translation failed.");
    }
    private static bool HasReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return true;
        foreach (var child in Directory.EnumerateFileSystemEntries(path))
        {
            var attributes = File.GetAttributes(child);
            if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.Directory) != 0 && HasReparsePoint(child)) return true;
        }
        return false;
    }
    private static void CopyTree(string source, string destination, CancellationToken cancel)
    {
        if (HasReparsePoint(source)) throw new IOException("A source folder contains redirected paths.");
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            cancel.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, false);
        }
    }
}
