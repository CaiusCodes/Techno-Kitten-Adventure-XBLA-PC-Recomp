using System.Diagnostics;
using System.Text.Json;

namespace Tka.Installer;

internal sealed class InstallEngine(string distributionRoot, Action<int, string> progress, Action<string> log, Action? beforePromotion = null, Action? beforeLauncherPromotion = null)
{
    public string Install(string package, string destination, CancellationToken cancel)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new IOException("Choose a regular writable folder for installation.");
        var resources = Path.Combine(distributionRoot, "resources");
        var payload = Path.Combine(resources, "game");
        if (!File.Exists(Path.Combine(payload, "Techno Kitten Adventure.exe"))) throw new FileNotFoundException("Installer resources are missing. Extract the complete download first.");
        var game = Path.Combine(root, "Game");
        var legacy = Path.Combine(root, "runtime");
        var launcher = Path.Combine(root, "Techno Kitten Adventure.exe");
        var oldLauncher = Path.Combine(root, "TechnoKittenAdventure.exe");
        var launcherBytes = LauncherPayload.Read();
        foreach (var candidate in new[] { launcher, oldLauncher }) ValidateLauncher(candidate, launcherBytes);
        foreach (var child in new[] { game, legacy, Path.Combine(root, "backups") })
            if (Directory.Exists(child) && (File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) throw new IOException("Installation folders must not redirect elsewhere.");
        if (Directory.Exists(game) && Directory.Exists(legacy)) throw new IOException("Both runtime and Game folders exist. Keep the installation you want to update in this folder; move the other installation elsewhere first.");
        var previous = Directory.Exists(game) ? game : legacy;
        if (Directory.Exists(previous)) ValidateInstallation(previous);
        foreach (var name in new[] { "Techno Kitten Adventure.exe", "TechnoKittenAdventure.exe" })
        {
            var host = Path.Combine(previous, name);
            if (File.Exists(host)) { using var check = new FileStream(host, FileMode.Open, FileAccess.Read, FileShare.None); }
        }
        var staging = Path.Combine(root, ".install-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var extracted = Path.Combine(staging, "original");
        var inspection = Path.Combine(staging, "inspection");
        var shaders = Path.Combine(staging, "shaders");
        var effects = Path.Combine(staging, "effects");
        var textures = Path.Combine(staging, "textures");
        var ready = Path.Combine(staging, "Game");
        var readyLauncher = Path.Combine(staging, "Techno Kitten Adventure.exe");
        string? backup = null;
        var promoted = false;
        try
        {
            File.WriteAllBytes(readyLauncher, launcherBytes);
            progress(2, "Checking your package…");
            StfsPackage.Extract(package, extracted, cancel, progress);
            cancel.ThrowIfCancellationRequested();
            progress(32, "Preparing the Windows game…");
            CopyTree(payload, ready, cancel);
            GamePayload.RestoreShared(resources, ready, cancel);
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
            var reports = Path.Combine(ready, "userdata/cache/import"); Directory.CreateDirectory(reports);
            foreach (var report in new[] { Path.Combine(extracted, "extraction-manifest.json"), Path.Combine(effects, "conversion-report.json"),
                Path.Combine(shaders, "shader-manifest.json"), Path.Combine(textures, "texture-report.json") }) File.Copy(report, Path.Combine(reports, Path.GetFileName(report)), true);
            File.WriteAllText(Path.Combine(ready, "tka-install.json"), JsonSerializer.Serialize(new {
                title = "Techno Kitten Adventure!", package_sha256 = StfsPackage.ExpectedHash,
                installed_utc = DateTime.UtcNow, mode = "full", format = 1
            }, new JsonSerializerOptions { WriteIndented = true }));
            cancel.ThrowIfCancellationRequested();
            progress(96, "Finishing installation…");
            if (Directory.Exists(previous))
            {
                var backupRoot = Path.Combine(root, "backups"); Directory.CreateDirectory(backupRoot);
                backup = Path.Combine(backupRoot, "Game-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
                Directory.Move(previous, backup);
                log("Previous installation retained: " + backup);
            }
            var runtimePromoted = false;
            string? oldLauncherBackup = null;
            try
            {
                beforePromotion?.Invoke();
                Directory.Move(ready, game); runtimePromoted = true;
                // Move only a recognized predecessor, preserving it until commit.
                ValidateLauncher(oldLauncher, launcherBytes);
                if (File.Exists(oldLauncher))
                {
                    var backupRoot = Path.Combine(root, "backups"); Directory.CreateDirectory(backupRoot);
                    oldLauncherBackup = Path.Combine(backupRoot, "Launcher-" + Guid.NewGuid().ToString("N") + ".exe");
                    File.Move(oldLauncher, oldLauncherBackup);
                }
                beforeLauncherPromotion?.Invoke();
                ValidateLauncher(launcher, launcherBytes);
                if (File.Exists(launcher))
                {
                    if (!LauncherPayload.CanReplace(launcher, launcherBytes)) throw new IOException("The play launcher changed during installation.");
                    var backupRoot = Path.Combine(root, "backups"); Directory.CreateDirectory(backupRoot);
                    File.Replace(readyLauncher, launcher, Path.Combine(backupRoot, "Launcher-" + Guid.NewGuid().ToString("N") + ".exe"));
                }
                else File.Move(readyLauncher, launcher);
                promoted = true;
            }
            catch
            {
                if (oldLauncherBackup != null) File.Move(oldLauncherBackup, oldLauncher);
                if (runtimePromoted) Directory.Move(game, ready);
                if (backup != null && !Directory.Exists(previous)) Directory.Move(backup, previous);
                throw;
            }
            progress(100, "Ready to play!");
            return launcher;
        }
        finally
        {
            // This path is a fresh, fixed-prefix child created by this invocation.
            // Refuse recursive cleanup if a path has been redirected meanwhile.
            try
            {
                if (Path.GetDirectoryName(Path.GetFullPath(staging)) == root &&
                    Path.GetFileName(staging).StartsWith(".install-", StringComparison.Ordinal) &&
                    Directory.Exists(staging) && !HasReparsePoint(staging)) Directory.Delete(staging, true);
                else log("Staging retained for inspection: " + staging);
            }
            catch (Exception error) { log("Staging cleanup: " + error.Message); }
            if (!promoted) log("Installation was not committed; previous game and package preserved.");
        }
    }

    private static void ValidateLauncher(string file, byte[] payload)
    {
        if (Directory.Exists(file) || File.Exists(file) &&
            ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || !LauncherPayload.CanReplace(file, payload)))
            throw new IOException(Path.GetFileName(file) + " already exists and does not match this setup. Extract the complete installer into a separate folder.");
    }

    private static void ValidateInstallation(string folder)
    {
        var marker = Path.Combine(folder, "tka-install.json");
        if (!File.Exists(marker)) throw new IOException("The " + Path.GetFileName(folder) + " folder contains unrelated files. Move Setup to a separate folder.");
        using var document = JsonDocument.Parse(File.ReadAllText(marker));
        if (!document.RootElement.TryGetProperty("package_sha256", out var hash) || hash.GetString() != StfsPackage.ExpectedHash)
            throw new IOException("The existing installation does not match this game package.");
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
