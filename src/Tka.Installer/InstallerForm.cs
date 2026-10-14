using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Tka.Installer;

internal sealed class InstallerForm : Form
{
    internal static OpenFileDialog CreatePackagePicker(string? root = null) => new() {
        Title = "Choose a package, or open its folder and choose Open", Filter = "Xbox 360 package|*.*", FileName = "Use this folder", CheckFileExists = false, ValidateNames = false,
        InitialDirectory = root ?? Path.GetDirectoryName(Application.ExecutablePath), RestoreDirectory = true };
    internal static string ResolvePickerPath(string path) => !File.Exists(path) && Path.GetFileName(path) == "Use this folder" ? Path.GetDirectoryName(path)! : path;
    private static readonly Color Pink = Color.FromArgb(255, 123, 167);
    private static readonly Color Cyan = Color.FromArgb(86, 234, 243);
    private readonly TextBox package = new();
    private readonly Button browsePackage;
    private readonly Button install;
    private readonly Button cancel;
    private readonly Label status;
    private readonly Label percent;
    private readonly Panel progressFill;
    private readonly CheckBox fullscreen = new();
    private readonly CheckBox desktopShortcut = new();
    private readonly ComboBox choices = new();
    private bool discovering;
    private sealed record PackageChoice(string FullPath)
    {
        public override string ToString() => Path.GetFileName(FullPath)+" — "+Path.GetDirectoryName(FullPath);
    }
    private Task? droppedInput;
    private string[]? playProbeArguments;
    private readonly Bitmap setupArtwork = LoadSetupArtwork();
    private readonly string root;
    private readonly Action<string> log;
    private CancellationTokenSource? cancellation;
    private string? gameExecutable;
    private sealed record LayoutItem(Control Control, Rectangle Bounds, string Family, float Points, FontStyle Style);
    private readonly List<LayoutItem> logicalLayout = [];
    private readonly Dictionary<(string Family, float Pixels, FontStyle Style), Font> layoutFonts = [];
    private float layoutScale = 1;
    private int progressValue;

    public InstallerForm(string distributionRoot, Action<string> write)
    {
        SuspendLayout();
        root = distributionRoot; log = write;
        Text = "Techno Kitten Adventure";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        // This code-drawn form has one explicit 96-DPI coordinate system for
        // backgrounds, controls and fonts. Do not mix WinForms autoscaling with it.
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(800, 610);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(14, 23, 40); ForeColor = Color.White;
        Font = new Font("Consolas", 10);
        DoubleBuffered = true;
        LabelAt("Choose your package", 300, 40, 472, 38, Color.FromArgb(255, 240, 204), 18, FontStyle.Bold);
        LabelAt("Select your legally obtained\nXbox 360 game package.", 301, 87, 470, 52, Color.FromArgb(174, 190, 211), 11.25f);
        ConfigurePath(package, 318, 168, "No package selected");
        package.Text = "No package selected";
        browsePackage = ButtonAt("Browse Package", 300, 222, 472, 44, true, Cyan);
        browsePackage.BackColor = BackColor;
        browsePackage.Click += async (_, _) =>
        {
            using var picker = CreatePackagePicker(root);
            if (picker.ShowDialog(this) == DialogResult.OK) await ChooseInputAsync(ResolvePickerPath(picker.FileName));
        };
        LabelAt("After setup, open Techno Kitten Adventure.exe.\nAssets and saves stay in resources.", 301, 295, 470, 76, Color.FromArgb(174, 190, 211), 10.5f);
        foreach (var (control,text,y) in new[] { (fullscreen,"Start in fullscreen",397), (desktopShortcut,"Create desktop shortcut",428) })
        {
            control.Text = text; control.Location = new Point(316,y); control.Size = new Size(450,27);
            control.ForeColor = Color.White; control.BackColor = Color.Transparent;
            control.Font = new Font("Consolas",10.5f); control.Cursor = Cursors.Hand;
            Controls.Add(control);
        }
        choices.Location = package.Location; choices.Size = package.Size; choices.Font = package.Font;
        choices.DropDownStyle = ComboBoxStyle.DropDownList; choices.Visible = false; Controls.Add(choices);
        choices.SelectedIndexChanged += async (_, _) =>
        {
            if (!discovering && choices.SelectedItem is PackageChoice selected) { choices.Visible = false; package.Visible = true; package.Text = selected.FullPath; await InstallAsync(); }
        };
        AllowDrop = true; package.AllowDrop = true;
        DragEventHandler dragging = (_,e) => e.Effect = cancellation == null && gameExecutable == null && !discovering && e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragEventHandler dropping = async (_,e) =>
        {
            if (cancellation == null && gameExecutable == null && !discovering && e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length == 1)
                await (droppedInput = ChooseInputAsync(files[0]));
        };
        DragEnter += dragging; DragDrop += dropping; package.DragEnter += dragging; package.DragDrop += dropping;
        status = LabelAt("Choose a package to get started.", 301, 475, 410, 39, Color.FromArgb(174, 190, 211), 10.5f);
        percent = LabelAt("0%", 712, 485, 60, 27, Cyan, 11, FontStyle.Bold);
        percent.TextAlign = ContentAlignment.TopRight;
        var track = new Panel { Location = new Point(301, 517), Size = new Size(470, 5), BackColor = Color.FromArgb(48, 65, 93) };
        progressFill = new Panel { Location = Point.Empty, Size = new Size(0, 5), BackColor = Cyan };
        track.Controls.Add(progressFill); Controls.Add(track);
        LabelAt("Your package stays untouched.\nNo game assets included.", 301, 550, 226, 42, Color.FromArgb(174, 190, 211), 8.25f);
        cancel = ButtonAt("Close", 534, 544, 84, 44, false);
        cancel.Click += (_, _) => { if (cancellation != null) { cancellation.Cancel(); status.Text = "Cancelling safely…"; } else Close(); };
        install = ButtonAt("Import game", 633, 544, 139, 44, true);
        install.Enabled = false;
        package.TextChanged += (_, _) => { if (gameExecutable == null && cancellation == null) install.Enabled = File.Exists(package.Text); };
        AcceptButton = install;
        install.Click += async (_, _) => await InstallAsync();
        FormClosing += (_, e) =>
        {
            if (discovering) { e.Cancel = true; return; }
            if (cancellation != null) { e.Cancel = true; cancellation.Cancel(); status.Text = "Cancelling safely…"; }
        };
        CaptureLayout(this);
        ApplyLayoutDpi(DeviceDpi);
        ResumeLayout(false);
    }

    private void CaptureLayout(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            logicalLayout.Add(new(control, control.Bounds, control.Font.FontFamily.Name, control.Font.SizeInPoints, control.Font.Style));
            CaptureLayout(control);
        }
    }

    private void ApplyLayoutDpi(int dpi)
    {
        if (logicalLayout.Count == 0) return;
        SuspendLayout();
        layoutScale = dpi / 96f;
        foreach (var item in logicalLayout)
        {
            // Pixel fonts avoid a second implicit DPI multiplier.
            var key = (item.Family, item.Points * 96f / 72f * layoutScale, item.Style);
            if (!layoutFonts.TryGetValue(key, out var font))
                layoutFonts.Add(key, font = new Font(key.Family, key.Item2, key.Style, GraphicsUnit.Pixel));
            // WinForms can retain an equal previous Font instance. Keep every
            // cached instance alive until the controls have been disposed.
            item.Control.Font = font;
            var b = item.Bounds;
            item.Control.Bounds = new Rectangle(Scaled(b.X), Scaled(b.Y), Scaled(b.Width), Scaled(b.Height));
        }
        ClientSize = new Size(Scaled(800), Scaled(610));
        progressFill.Width = progressValue * progressFill.Parent!.ClientSize.Width / 100;
        ResumeLayout(false);
        Invalidate(true);
    }

    private int Scaled(int value) => (int)Math.Round(value * layoutScale);
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyLayoutDpi(DeviceDpi);
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        log("DPI changed: " + e.DeviceDpiNew);
        base.OnDpiChanged(e);
        ApplyLayoutDpi(e.DeviceDpiNew);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { foreach (var font in layoutFonts.Values) font.Dispose(); layoutFonts.Clear(); setupArtwork.Dispose(); }
    }

    // Bounded preview path: exercise the same layout at a chosen effective DPI
    // without changing the user's Windows display settings.
    internal void PreviewDpi(int dpi)
    {
        if (dpi is < 96 or > 288) throw new ArgumentOutOfRangeException(nameof(dpi));
        MinimumSize = SizeFromClientSize(new Size(800 * dpi / 96, 610 * dpi / 96));
        ApplyLayoutDpi(dpi);
        foreach (var item in logicalLayout.Where(x => x.Control.Parent == this))
        {
            if (!ClientRectangle.Contains(item.Control.Bounds)) throw new InvalidDataException("DPI layout clips a control: " + item.Control.Text);
            if (item.Control is Label label)
            {
                var measured = TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, int.MaxValue), TextFormatFlags.WordBreak);
                if (measured.Height > label.Height) throw new InvalidDataException("DPI layout clips text: " + label.Text);
            }
        }
        if (Math.Abs(ClientSize.Width - Scaled(800)) > 1 || Math.Abs(ClientSize.Height - Scaled(610)) > 1)
            throw new InvalidDataException("DPI preview client was constrained.");
        log($"DPI preview passed: {dpi}; client {ClientSize.Width}x{ClientSize.Height}; controls {logicalLayout.Count}.");
    }

    internal bool IsReady => gameExecutable != null;
    internal bool BrowseEnabled => browsePackage.Enabled;
    internal bool PlayEnabled => install.Enabled && gameExecutable != null;
    internal bool ExtrasUnchecked => !fullscreen.Checked && !desktopShortcut.Checked;
    internal int ChoiceCount => choices.Items.Count;
    internal void TestPlay()
    {
        if (!IsReady || !PlayEnabled) throw new InvalidDataException("Play isn't ready.");
        fullscreen.Checked = true;
        var data = PortableLayout.Data(root);
        playProbeArguments = [Path.Combine(data,"Helicopter.dll"),Path.Combine(data,"Content"),"6","--fixed60","--scripted"];
        install.PerformClick();
    }
    internal async Task TestDropAsync(string path)
    {
        var data = new DataObject(DataFormats.FileDrop,new[] { path });
        var enter = new DragEventArgs(data,0,0,0,DragDropEffects.Copy,DragDropEffects.None);
        OnDragEnter(enter);
        if (enter.Effect != DragDropEffects.Copy) throw new InvalidDataException("Drop acceptance failed.");
        OnDragDrop(enter);
        if (droppedInput is null) throw new InvalidDataException("Drop handler wasn't invoked.");
        await droppedInput;
    }

    internal async Task DiscoverAsync()
    {
        if (cancellation != null) return;
        discovering = true; browsePackage.Enabled = false; status.Text = "Checking nearby packages…";
        try
        {
            await Task.Run(() => PortableLayout.Migrate(root,log));
            if (PortableLayout.Ready(root)) { Ready(); return; }
            var found = await Task.Run(() => PackageDiscovery.Nearby(root,CancellationToken.None));
            discovering = false;
            await OfferAsync(found);
        }
        catch (Exception error) { log(error.ToString()); status.Text = "Choose or drop your supported package."; }
        finally { discovering = false; browsePackage.Enabled = !IsReady && cancellation == null; }
    }

    internal async Task ChooseInputAsync(string source)
    {
        if (cancellation != null || IsReady || discovering) return;
        discovering = true; browsePackage.Enabled = false; status.Text = "Checking your package…";
        try
        {
            var found = await Task.Run(() => PackageDiscovery.FromInput(source,CancellationToken.None));
            discovering = false;
            await OfferAsync(found);
        }
        catch (Exception error)
        {
            log(error.ToString()); status.Text = error.Message;
            MessageBox.Show(this,error.Message,"Choose a supported package",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        finally { discovering = false; browsePackage.Enabled = !IsReady && cancellation == null; }
    }

    private async Task OfferAsync(List<string> found)
    {
        if (found.Count == 1) { choices.Visible = false; package.Visible = true; package.Text = found[0]; await InstallAsync(); }
        else if (found.Count > 1)
        {
            discovering = true; choices.Items.Clear(); choices.Items.AddRange(found.Select(path => new PackageChoice(path)).ToArray()); choices.SelectedIndex = -1;
            choices.Visible = true; package.Visible = false; discovering = false;
            status.Text = "More than one package found. Choose one above.";
        }
        else status.Text = "Browse or drop your supported package here.";
    }

    private void Ready()
    {
        gameExecutable = Path.Combine(root,"Techno Kitten Adventure.exe");
        browsePackage.Enabled = false; choices.Visible = false; package.Visible = true;
        package.Text = "Your game is ready"; install.Text = "Play"; install.Enabled = true;
        status.Text = "Ready to play. Choose Play when you're ready."; percent.Text = "100%"; progressValue = 100;
        progressFill.Width = progressFill.Parent!.ClientSize.Width;
        fullscreen.Enabled = true; desktopShortcut.Enabled = true;
    }

    private async Task InstallAsync()
    {
        if (gameExecutable != null)
        {
            try
            {
                if (desktopShortcut.Checked)
                {
                    var path = PlayExtras.ShortcutPath;
                    if (File.Exists(path))
                        MessageBox.Show(this,"A desktop shortcut already exists. It has been left unchanged.","Desktop shortcut",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    else PlayExtras.CreateShortcut(root,path);
                }
                if (fullscreen.Checked) PlayExtras.ApplyFullscreen(root);
                using var game = PortableLayout.StartGame(root,playProbeArguments);
                Close();
            }
            catch (Exception error) { log(error.ToString()); MessageBox.Show(this,error.Message,"Could not start",MessageBoxButtons.OK,MessageBoxIcon.Error); }
            return;
        }
        if (!File.Exists(package.Text)) return;
        var source = package.Text;
        cancellation = new CancellationTokenSource();
        install.Enabled = false; browsePackage.Enabled = false; choices.Enabled = false;
        fullscreen.Enabled = false; desktopShortcut.Enabled = false; cancel.Text = "Cancel";
        var progress = new Progress<(int value,string text)>(p => { progressValue = p.value; progressFill.Width = p.value * progressFill.Parent!.ClientSize.Width / 100; percent.Text = p.value+"%"; status.Text = p.text; });
        try
        {
            await Task.Run(() => new InstallEngine((value,message) => { log($"{value}% {message}"); ((IProgress<(int,string)>)progress).Report((value,message)); },log)
                .Install(source,root,cancellation.Token));
            Ready(); // Deliberately stay open: only Play launches the game.
        }
        catch (OperationCanceledException) { status.Text = "Cancelled. Your game, saves and package are safe."; }
        catch (Exception error)
        {
            log(error.ToString()); status.Text = "Import stopped. See resources/logs for details.";
            MessageBox.Show(this,error.Message,"Import stopped",MessageBoxButtons.OK,MessageBoxIcon.Error);
        }
        finally
        {
            cancellation.Dispose(); cancellation = null;
            install.Enabled = IsReady || File.Exists(package.Text); browsePackage.Enabled = !IsReady; choices.Enabled = true;
            fullscreen.Enabled = true; desktopShortcut.Enabled = true; cancel.Text = "Close";
        }
    }
    private Label LabelAt(string text, int x, int y, int width, int height, Color color, float size, FontStyle style = FontStyle.Regular)
    {
        var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = color, BackColor = Color.Transparent, Font = new Font("Consolas", size, style) };
        Controls.Add(label); return label;
    }
    private void ConfigurePath(TextBox box, int x, int y, string placeholder)
    {
        box.Location = new Point(x, y); box.Size = new Size(438, 28); box.ReadOnly = true; box.TabStop = false;
        box.BackColor = Color.FromArgb(10, 17, 32); box.ForeColor = Color.FromArgb(174, 190, 211); box.BorderStyle = BorderStyle.None;
        box.PlaceholderText = placeholder; box.Font = new Font("Consolas", 10.5f); Controls.Add(box);
    }
    private Button ButtonAt(string text, int x, int y, int width, int height, bool primary, Color? accent = null)
    {
        var button = new SetupButton(primary, accent ?? Pink) { Text = text, Location = new Point(x, y), Size = new Size(width, height), FlatStyle = FlatStyle.Flat,
            BackColor = BackColor, ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Consolas", 10.5f, FontStyle.Bold), UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = primary ? 0 : 1; button.FlatAppearance.BorderColor = Color.FromArgb(91, 64, 126);
        Controls.Add(button); return button;
    }
    private static Bitmap LoadSetupArtwork()
    {
        using var stream = typeof(InstallerForm).Assembly.GetManifestResourceStream("Tka.SetupArtwork")
            ?? throw new InvalidDataException("Setup artwork is missing.");
        using var image = new Bitmap(stream);
        return new Bitmap(image);
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
        e.Graphics.ScaleTransform(layoutScale, layoutScale);
        using var sky = new SolidBrush(Color.FromArgb(22, 44, 65));
        e.Graphics.FillRectangle(sky, 0, 0, 274, 610);
        using var stars = new SolidBrush(Color.FromArgb(87, 80, 110));
        for (var i = 0; i < 24; i++)
            e.Graphics.FillRectangle(stars, 12 + (i*67+19)%249, 12 + (i*41+17)%330, 2, 2);
        DrawPixelTitle(e.Graphics, "TECHNO KITTEN", 20, 38, Color.FromArgb(255, 240, 204));
        DrawPixelTitle(e.Graphics, "ADVENTURE!", 20, 73, Pink);
        using var sun = new SolidBrush(Color.FromArgb(255, 191, 105));
        using var sunset = new SolidBrush(Pink);
        foreach (var (y, x, width) in new[] { (150,88,92), (162,72,124), (174,60,148), (186,52,164),
                     (198,52,164), (210,52,164), (222,60,148), (234,72,124), (246,88,92) })
            e.Graphics.FillRectangle(y < 198 ? sun : sunset, x, y, width, 12);
        using var peak = new SolidBrush(Color.FromArgb(71, 83, 118));
        using var peakLight = new SolidBrush(Color.FromArgb(183, 103, 152));
        for (var row = 0; row < 12; row++)
        {
            e.Graphics.FillRectangle(peak, 28-row*3, 246+row*5, 6+row*6, 5);
            e.Graphics.FillRectangle(peakLight, 31, 246+row*5, 3+row*3, 5);
        }
        using var sea = new SolidBrush(Color.FromArgb(23, 70, 90));
        e.Graphics.FillRectangle(sea, 0, 306, 274, 304);
        using var ripple = new SolidBrush(Color.FromArgb(40, 102, 123));
        for (var row = 0; row < 10; row++)
            e.Graphics.FillRectangle(ripple, 15+(row%3)*18, 317+row*24, 228-(row%3)*24, 2);
        e.Graphics.DrawImage(setupArtwork, new Rectangle(18, 195, 242, 242));
        using var divider = new SolidBrush(Cyan);
        e.Graphics.FillRectangle(divider, 272, 0, 3, 610);
        using var field = PixelFrame(new Rectangle(300,148,472,58), 5);
        using var fieldFill = new SolidBrush(Color.FromArgb(10, 17, 32));
        using var border = new Pen(Color.FromArgb(53, 70, 94), 2);
        e.Graphics.FillPath(fieldFill, field); e.Graphics.DrawPath(border, field);
        using var extras = PixelFrame(new Rectangle(300,382,472,75), 5);
        using var extraFill = new SolidBrush(Color.FromArgb(27, 41, 64));
        e.Graphics.FillPath(extraFill, extras);
    }
    private static void DrawStar(Graphics g, Brush brush, float x, float y, float size)
    {
        g.FillRectangle(brush, x-size, y, size*3, size);
        g.FillRectangle(brush, x, y-size, size, size*3);
    }
    private static readonly Dictionary<char, string[]> TitlePixels = new()
    {
        ['A'] = ["01110", "11011", "11011", "11111", "11011", "11011", "11011"],
        ['C'] = ["01111", "11000", "11000", "11000", "11000", "11000", "01111"],
        ['D'] = ["11110", "11011", "11011", "11011", "11011", "11011", "11110"],
        ['E'] = ["11111", "11000", "11000", "11110", "11000", "11000", "11111"],
        ['H'] = ["11011", "11011", "11011", "11111", "11011", "11011", "11011"],
        ['I'] = ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
        ['K'] = ["11011", "11011", "11110", "11100", "11110", "11011", "11011"],
        ['N'] = ["11001", "11101", "11101", "11011", "11011", "11001", "11001"],
        ['O'] = ["01110", "11011", "11011", "11011", "11011", "11011", "01110"],
        ['R'] = ["11110", "11011", "11011", "11110", "11100", "11011", "11011"],
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['U'] = ["11011", "11011", "11011", "11011", "11011", "11011", "01110"],
        ['V'] = ["11011", "11011", "11011", "11011", "11011", "01010", "00100"],
        ['!'] = ["00100", "00100", "00100", "00100", "00100", "00000", "00100"]
    };
    private static void DrawPixelTitle(Graphics g, string text, int x, int y, Color color)
    {
        using var ink = new SolidBrush(color);
        foreach (var letter in text)
        {
            if (TitlePixels.TryGetValue(letter, out var rows))
                for (var row = 0; row < 7; row++)
                    for (var column = 0; column < 5; column++)
                        if (rows[row][column] == '1')
                            g.FillRectangle(ink, x+column*3, y+row*3, 3, 3);
            x += 18;
        }
    }
    internal static GraphicsPath PixelFrame(Rectangle bounds, int step)
    {
        var path = new GraphicsPath();
        var x = bounds.X; var y = bounds.Y; var right = bounds.Right; var bottom = bounds.Bottom;
        path.AddPolygon(new Point[] { new(x+step,y), new(right-step,y), new(right-step,y+step), new(right,y+step),
            new(right,bottom-step), new(right-step,bottom-step), new(right-step,bottom), new(x+step,bottom),
            new(x+step,bottom-step), new(x,bottom-step), new(x,y+step), new(x+step,y+step) });
        path.CloseFigure(); return path;
    }
    // Render the completed screen with real controls, without importing a package.
    internal void PreviewCompletedState()
    {
        package.Text = "Techno Kitten Adventure package";
        browsePackage.Enabled = false;
        cancel.Text = "Cancel"; cancel.Refresh(); cancel.Text = "Close";
        install.Enabled = true; install.Text = "Play";
        status.Text = "Ready to play. Choose Play when you're ready."; percent.Text = "100%";
        progressValue = 100;
        progressFill.Width = progressFill.Parent!.ClientSize.Width;
        cancel.Focus();
    }

    private sealed class SetupButton : Button
    {
        private readonly bool primary;
        private readonly Color accent;
        private bool hover;
        public SetupButton(bool primary, Color accent)
        {
            this.primary = primary;
            this.accent = accent;
            // Own every pixel, including the stepped corners. Native flat-button
            // background/focus painting can otherwise leave rectangular remnants
            // when a disabled or default button changes state.
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            var scale = Height / 44f;
            var inset = Math.Max(1, (int)Math.Round(scale));
            using var shape = PixelFrame(new Rectangle(inset, inset, Width-inset*3, Height-inset*3), Math.Max(1, (int)Math.Round(4 * scale)));
            var color = !Enabled ? Color.FromArgb(42, 51, 69) : primary ? (hover ? ControlPaint.Light(accent, .15f) : accent) : hover ? Color.FromArgb(49, 70, 97) : Color.FromArgb(37, 56, 83);
            using var shadowShape = PixelFrame(new Rectangle(inset*3, inset*4, Width-inset*5, Height-inset*5), Math.Max(1, (int)Math.Round(4 * scale)));
            using var shadow = new SolidBrush(Color.FromArgb(6, 12, 28));
            e.Graphics.FillPath(shadow, shadowShape);
            using var fill = new SolidBrush(color); e.Graphics.FillPath(fill, shape);
            using var border = new Pen(Focused ? Color.White : primary && Enabled ? accent : Color.FromArgb(55, 74, 99), Math.Max(1, scale));
            e.Graphics.DrawPath(border, shape);
            using var shine = new SolidBrush(Enabled ? Color.FromArgb(228, 230, 255) : Color.FromArgb(78, 88, 108));
            e.Graphics.FillRectangle(shine, inset*6, inset*2, Width-inset*13, inset);
            var textColor = !Enabled ? Color.FromArgb(146, 157, 178) : primary ? Color.FromArgb(16, 32, 48) : Color.FromArgb(225, 231, 243);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
