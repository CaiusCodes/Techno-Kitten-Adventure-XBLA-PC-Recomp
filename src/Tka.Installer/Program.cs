using System.Reflection;
using System.Runtime.Loader;

namespace Tka.Installer;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--shader-compiler")
        {
            try
            {
                var context = new CompilerContext(Path.GetFullPath(args[1]));
                using var reflectionScope = context.EnterContextualReflection();
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
                var result = assembly.EntryPoint!.Invoke(null, [args.Skip(2).ToArray()]);
                return result is int status ? status : 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        var root = AppContext.BaseDirectory;
        if (args.Length >= 2 && args[0] == "--root") { root = Path.GetFullPath(args[1]); args = args.Skip(2).ToArray(); }
        var manualSetup = args.Contains("--setup",StringComparer.Ordinal);
        args = args.Where(a => a != "--setup").ToArray();
        var logs = Path.Combine(PortableLayout.Resources(root), "logs");
        Directory.CreateDirectory(logs);
        using var stream = new StreamWriter(Path.Combine(logs, "installer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log")) { AutoFlush = true };
        var log = TextWriter.Synchronized(stream);
        Console.SetOut(log); Console.SetError(log);
        void Write(string text) { if (!string.IsNullOrWhiteSpace(text)) log.WriteLine($"{DateTime.UtcNow:O} {text}"); }
        try
        {
            if (args.Length == 1 && args[0] == "--migrate") { PortableLayout.Migrate(root,Write); return PortableLayout.Ready(root) ? 0 : 2; }
            if (args.Length == 1 && args[0] == "--test-ready") return PortableLayout.Ready(root) ? 0 : 2;
            if (args.Length == 2 && args[0] == "--test-extras")
            {
                PlayExtras.ApplyFullscreen(root); PlayExtras.CreateShortcut(root,Path.GetFullPath(args[1])); return 0;
            }
            if (args.Length == 0 && !manualSetup)
            {
                PortableLayout.Migrate(root,Write);
                if (PortableLayout.Ready(root)) { using var game = PortableLayout.StartGame(root); game.WaitForExit(); return game.ExitCode; }
            }
            if (args.Length >= 2 && args[0] == "--install-here")
            {
                new InstallEngine((value, message) => Write($"{value}% {message}"), Write)
                    .Install(args[1], root, CancellationToken.None, args.Contains("--unlock-all"));
                return 0;
            }
            if (args.Length >= 3 && args[0] == "--install")
            {
                // Explicit development fault injection exercises the same
                // transaction path; these switches are never used by the UI.
                using var cancel = new CancellationTokenSource();
                var engine = new InstallEngine((value, message) =>
                {
                    Write($"{value}% {message}");
                    if (value >= 82 && args.Contains("--test-cancel")) cancel.Cancel();
                }, Write, args.Contains("--test-rollback") ? () => throw new IOException("TEST: simulated promotion failure.") : null,
                    args.Contains("--test-launcher-rollback") ? () => throw new IOException("TEST: simulated launcher promotion failure.") : null);
                engine.Install(args[1], args[2], cancel.Token, args.Contains("--unlock-all"));
                return 0;
            }
            Write("Initializing setup display.");
            if (args.Any(a => a.StartsWith("--preview", StringComparison.Ordinal)) || args.Contains("--ui-smoke"))
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            ApplicationConfiguration.Initialize();
            Write("Creating setup window.");
            using var form = new InstallerForm(root, Write);
            Write("Setup window created.");
            if (args.Length == 1 && args[0] == "--test-package-picker")
            {
                using var picker = InstallerForm.CreatePackagePicker(root);
                if (!string.Equals(picker.InitialDirectory, root, StringComparison.OrdinalIgnoreCase)
                    || !picker.RestoreDirectory || picker.ValidateNames ||
                    InstallerForm.ResolvePickerPath(Path.Combine(root,"Use this folder")) != root)
                    throw new InvalidOperationException("Package picker directory/flags mismatch.");
                Write("Package picker PASS: Setup EXE directory; working directory=" + Environment.CurrentDirectory);
                return 0;
            }
            if (args.FirstOrDefault() is "--ui-auto-test" or "--ui-choice-test" or "--ui-drop-test" or "--ui-play-test")
            {
                Exception? failure = null;
                form.Shown += async (_,_) =>
                {
                    try
                    {
                        if (!form.ExtrasUnchecked) throw new InvalidDataException("Extras must start unchecked.");
                        if (args[0] == "--ui-drop-test") await form.TestDropAsync(args[1]); else await form.DiscoverAsync();
                        if (args[0] == "--ui-choice-test") { if (form.ChoiceCount != 2 || form.IsReady) throw new InvalidDataException("Multiple-package choice failed."); }
                        else if (!form.IsReady || form.BrowseEnabled || !form.PlayEnabled || (args[0] != "--ui-play-test" && Directory.Exists(Path.Combine(PortableLayout.Data(root),"logs"))))
                            throw new InvalidDataException("Ready state, disabled Browse, or wait-for-Play failed.");
                        if (args[0] == "--ui-play-test") form.TestPlay();
                        Write("Setup flow PASS: " + args[0]);
                    }
                    catch (Exception error) { failure = error; }
                    finally { form.Close(); }
                };
                Application.Run(form); if (failure is not null) throw failure; return 0;
            }
            if (args.Length == 1 && args[0] == "--ui-smoke")
            {
                using var timer = new System.Windows.Forms.Timer { Interval = 1500 };
                timer.Tick += (_, _) => { timer.Stop(); form.Close(); };
                form.Shown += (_, _) => timer.Start();
                Application.Run(form);
                Write("Setup message-loop smoke check passed."); return 0;
            }
            if (args.Length is 3 or 4 && args[0] == "--preview-dpi")
            {
                form.Show();
                if (args.Length == 4 && args[3] == "complete") form.PreviewCompletedState();
                // Repeated transitions catch stale font/bounds/progress state.
                foreach (var dpi in new[] { 96, 192, 144, 96 }) form.PreviewDpi(dpi);
                form.PreviewDpi(int.Parse(args[1]));
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.GetFullPath(args[2]));
                form.Close(); return 0;
            }
            if (args.Length == 2 && args[0] is ("--preview" or "--preview-complete"))
            {
                form.Show();
                if (args[0] == "--preview-complete") form.PreviewCompletedState();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.GetFullPath(args[1]));
                form.Close();
                return 0;
            }
            form.Shown += async (_,_) => await form.DiscoverAsync();
            Application.Run(form);
            return 0;
        }
        catch (Exception error)
        {
            Write(error.ToString());
            if (args.Length == 0) MessageBox.Show(error.Message, "Techno Kitten Adventure Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private sealed class CompilerContext(string path) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver resolver = new(path);
        protected override Assembly? Load(AssemblyName name)
        {
            var dependency = resolver.ResolveAssemblyToPath(name);
            return dependency == null ? null : LoadFromAssemblyPath(dependency);
        }
        protected override nint LoadUnmanagedDll(string name)
        {
            var dependency = resolver.ResolveUnmanagedDllToPath(name);
            return dependency == null ? 0 : LoadUnmanagedDllFromPath(dependency);
        }
    }
}
