using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Tka.Installer;

internal sealed class InstallerForm : Form
{
    private static readonly Color Pink = Color.FromArgb(255, 63, 162);
    private static readonly Color Cyan = Color.FromArgb(86, 234, 243);
    private readonly TextBox package = new();
    private readonly Button browsePackage;
    private readonly Button install;
    private readonly Button cancel;
    private readonly Label status;
    private readonly Label percent;
    private readonly Panel progressFill;
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
        Text = "Setup";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        // This code-drawn form has one explicit 96-DPI coordinate system for
        // backgrounds, controls and fonts. Do not mix WinForms autoscaling with it.
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(800, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(15, 16, 27); ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
        LabelAt("TECHNO KITTEN", 30, 22, 540, 55, Color.White, 28, FontStyle.Bold);
        LabelAt("ADVENTURE!", 31, 77, 540, 53, Pink, 25, FontStyle.Bold);
        LabelAt("Choose your game package", 53, 179, 655, 34, Color.White, 18, FontStyle.Bold);
        LabelAt("Select your legally obtained Xbox 360 package.", 56, 219, 655, 25, Color.FromArgb(187, 188, 209), 10.5f);
        ConfigurePath(package, 96, 267, "No package selected");
        package.Text = "No package selected";
        browsePackage = ButtonAt("Choose file…", 600, 255, 144, 44, true, Cyan);
        browsePackage.BackColor = Color.FromArgb(24, 25, 42);
        browsePackage.Click += (_, _) =>
        {
            using var picker = new OpenFileDialog { Title = "Select your Techno Kitten Adventure package", Filter = "Xbox 360 package|*.*", CheckFileExists = true };
            if (picker.ShowDialog(this) == DialogResult.OK) package.Text = picker.FileName;
        };
        LabelAt("To play, launch Techno Kitten Adventure.exe after game installation.\nYour game assets and saves will be saved within the Game folder.", 78, 345, 674, 48, Color.FromArgb(199, 201, 221), 10);
        status = LabelAt("Choose a package to get started.", 32, 424, 650, 30, Color.FromArgb(210, 213, 228), 10.5f);
        percent = LabelAt("0%", 704, 424, 64, 27, Cyan, 11, FontStyle.Bold);
        percent.TextAlign = ContentAlignment.TopRight;
        var track = new Panel { Location = new Point(32, 460), Size = new Size(736, 5), BackColor = Color.FromArgb(43, 43, 65) };
        progressFill = new Panel { Location = Point.Empty, Size = new Size(0, 6), BackColor = Pink };
        track.Controls.Add(progressFill); Controls.Add(track);
        LabelAt("Your original package stays untouched.\nNo game assets included.", 32, 503, 452, 45, Color.FromArgb(163, 165, 187), 9.5f);
        cancel = ButtonAt("Close", 510, 498, 100, 44, false);
        cancel.Click += (_, _) => { if (cancellation != null) { cancellation.Cancel(); status.Text = "Cancelling safely…"; } else Close(); };
        install = ButtonAt("Install game", 622, 498, 146, 44, true);
        install.Enabled = false;
        package.TextChanged += (_, _) => { install.Enabled = File.Exists(package.Text) && cancellation == null; status.Text = "Ready to install."; };
        AcceptButton = install;
        install.Click += async (_, _) => await InstallAsync();
        FormClosing += (_, e) =>
        {
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
        ClientSize = new Size(Scaled(800), Scaled(560));
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
        if (disposing) { foreach (var font in layoutFonts.Values) font.Dispose(); layoutFonts.Clear(); }
    }

    // Bounded preview path: exercise the same layout at a chosen effective DPI
    // without changing the user's Windows display settings.
    internal void PreviewDpi(int dpi)
    {
        if (dpi is < 96 or > 288) throw new ArgumentOutOfRangeException(nameof(dpi));
        MinimumSize = SizeFromClientSize(new Size(800 * dpi / 96, 560 * dpi / 96));
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
        if (Math.Abs(ClientSize.Width - Scaled(800)) > 1 || Math.Abs(ClientSize.Height - Scaled(560)) > 1)
            throw new InvalidDataException("DPI preview client was constrained.");
        log($"DPI preview passed: {dpi}; client {ClientSize.Width}x{ClientSize.Height}; controls {logicalLayout.Count}.");
    }

    private async Task InstallAsync()
    {
        if (gameExecutable != null)
        {
            Process.Start(new ProcessStartInfo(gameExecutable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(gameExecutable)! });
            Close(); return;
        }
        if (!File.Exists(package.Text)) { MessageBox.Show(this, "Select your game package first.", "Choose a package", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var source = package.Text;
        cancellation = new CancellationTokenSource();
        install.Enabled = false; browsePackage.Enabled = false; cancel.Text = "Cancel";
        var progress = new Progress<(int value, string text)>(p => { progressValue = p.value; progressFill.Width = p.value * progressFill.Parent!.ClientSize.Width / 100; percent.Text = p.value + "%"; status.Text = p.text; });
        try
        {
            gameExecutable = await Task.Run(() => new InstallEngine(root,
                (value, message) => { log($"{value}% {message}"); ((IProgress<(int, string)>)progress).Report((value, message)); }, log)
                .Install(source, root, cancellation.Token));
            install.Text = "Play now";
            status.Text = "Installed. Ready to play!";
        }
        catch (OperationCanceledException) { status.Text = "Cancelled. Your previous game and package are safe."; }
        catch (Exception error)
        {
            log(error.ToString()); status.Text = "Installation could not finish. See the logs folder for details.";
            MessageBox.Show(this, error.Message, "Installation stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            cancellation.Dispose(); cancellation = null;
            install.Enabled = true; browsePackage.Enabled = gameExecutable == null; cancel.Text = "Close";
        }
    }

    private Label LabelAt(string text, int x, int y, int width, int height, Color color, float size, FontStyle style = FontStyle.Regular)
    {
        var label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height), ForeColor = color, BackColor = Color.Transparent, Font = new Font("Segoe UI", size, style) };
        Controls.Add(label); return label;
    }
    private void ConfigurePath(TextBox box, int x, int y, string placeholder)
    {
        box.Location = new Point(x, y); box.Size = new Size(472, 28); box.ReadOnly = true; box.TabStop = false;
        box.BackColor = Color.FromArgb(16, 18, 31); box.ForeColor = Color.FromArgb(212, 216, 234); box.BorderStyle = BorderStyle.None;
        box.PlaceholderText = placeholder; box.Font = new Font("Segoe UI", 10.5f); Controls.Add(box);
    }
    private Button ButtonAt(string text, int x, int y, int width, int height, bool primary, Color? accent = null)
    {
        var button = new SetupButton(primary, accent ?? Pink) { Text = text, Location = new Point(x, y), Size = new Size(width, height), FlatStyle = FlatStyle.Flat,
            BackColor = BackColor, ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), UseVisualStyleBackColor = false };
        button.FlatAppearance.BorderSize = primary ? 0 : 1; button.FlatAppearance.BorderColor = Color.FromArgb(91, 64, 126);
        Controls.Add(button); return button;
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.ScaleTransform(layoutScale, layoutScale);
        using var hero = new LinearGradientBrush(new Rectangle(0, 0, 800, 136), Color.FromArgb(31, 24, 49), BackColor, 0f);
        e.Graphics.FillRectangle(hero, 0, 0, 800, 136);
        using var line = new Pen(Color.FromArgb(48, 48, 72));
        e.Graphics.DrawLine(line, 32, 136, 768, 136);
        {
            using var card = Rounded(new Rectangle(32, 160, 736, 156), 16);
            using var fill = new SolidBrush(Color.FromArgb(24, 25, 42));
            e.Graphics.FillPath(fill, card); e.Graphics.DrawPath(line, card);
            using var field = Rounded(new Rectangle(52, 254, 532, 46), 10);
            using var fieldFill = new SolidBrush(Color.FromArgb(16, 18, 31));
            e.Graphics.FillPath(fieldFill, field); e.Graphics.DrawPath(line, field);
            using var filePen = new Pen(Color.FromArgb(144, 159, 190), 1.5f);
            e.Graphics.DrawLines(filePen, new Point[] { new(67,266),new(77,266),new(83,272),new(83,286),new(67,286),new(67,266) });
            e.Graphics.DrawLines(filePen, new Point[] { new(77,266),new(77,272),new(83,272) });
            using var note = Rounded(new Rectangle(32, 334, 736, 70), 12);
            using var noteFill = new SolidBrush(Color.FromArgb(20, 24, 38)); e.Graphics.FillPath(noteFill, note);
            using var infoPen = new Pen(Cyan, 1.5f); e.Graphics.DrawEllipse(infoPen, 50, 351, 16, 16);
            e.Graphics.DrawLine(infoPen, 58, 358, 58, 363);
            using var infoDot = new SolidBrush(Cyan); e.Graphics.FillEllipse(infoDot, 57, 354, 2, 2);
        }
        Color[] colors = [Pink, Color.FromArgb(255, 174, 65), Color.FromArgb(251, 242, 108), Color.FromArgb(104, 230, 151), Cyan, Color.FromArgb(170, 123, 255)];
        for (var i = 0; i < colors.Length; i++)
        {
            using var pen = new Pen(colors[i], 5);
            e.Graphics.DrawArc(pen, 602 + i * 7, 26 + i * 7, 160 - i * 14, 160 - i * 14, 180, 180);
        }
        using var star = new SolidBrush(Cyan);
        DrawStar(e.Graphics, star, 682, 104, 9);
        e.Graphics.DrawLine(line, 32, 486, 768, 486);
    }
    private static void DrawStar(Graphics g, Brush brush, float x, float y, float size)
    {
        g.FillPolygon(brush, [new PointF(x, y-size), new(x+size*.25f,y-size*.25f),new(x+size,y),new(x+size*.25f,y+size*.25f),
            new(x,y+size),new(x-size*.25f,y+size*.25f),new(x-size,y),new(x-size*.25f,y-size*.25f)]);
    }
    internal static GraphicsPath Rounded(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath(); var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90); path.AddArc(bounds.Right-d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right-d, bounds.Bottom-d, d, d, 0, 90); path.AddArc(bounds.X, bounds.Bottom-d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    // Render the completed screen with real controls, without importing a package.
    internal void PreviewCompletedState()
    {
        package.Text = "Techno Kitten Adventure package";
        browsePackage.Enabled = false;
        cancel.Text = "Cancel"; cancel.Refresh(); cancel.Text = "Close";
        install.Enabled = true; install.Text = "Play now";
        status.Text = "Installed. Ready to play!"; percent.Text = "100%";
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
            // Own every pixel, including the rounded corners. Native flat-button
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
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var scale = Height / 44f;
            var inset = Math.Max(1, (int)Math.Round(scale));
            using var shape = Rounded(new Rectangle(inset, inset, Width-inset*3, Height-inset*3), Math.Max(1, (int)Math.Round(10 * scale)));
            var color = !Enabled ? Color.FromArgb(53, 43, 65) : primary ? (hover ? ControlPaint.Light(accent, .15f) : accent) : hover ? Color.FromArgb(44, 45, 65) : Color.FromArgb(29, 30, 46);
            using var fill = new SolidBrush(color); e.Graphics.FillPath(fill, shape);
            if (!primary || Focused) { using var border = new Pen(Focused ? Color.White : Color.FromArgb(67, 68, 91)); e.Graphics.DrawPath(border, shape); }
            var textColor = !Enabled ? Color.FromArgb(184, 170, 193) : primary && accent == Cyan ? Color.FromArgb(9, 25, 33) : Color.White;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
