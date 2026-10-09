# Rasterize the original SVG paths into a Windows multi-size PNG-frame ICO.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assets = Join-Path $root 'src/Tka.Installer/Assets'
Add-Type -AssemblyName System.Drawing
if (-not ('TkaIconRenderer' -as [type])) {
    $references = @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName) + [System.Drawing.Bitmap].Assembly.Location + @(Get-ChildItem -LiteralPath $PSHOME -Filter 'System.Private.Windows.*.dll' | ForEach-Object FullName)
    Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.IO;
using System.Xml;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
public static class TkaIconRenderer {
    static float N(string s) => float.Parse(s, CultureInfo.InvariantCulture);
    static void Draw(XmlElement node, Graphics g) {
        if (node.LocalName == "g") {
            var state = g.Save();
            foreach (Match transform in Regex.Matches(node.GetAttribute("transform"), @"(translate|scale)\(([^)]+)\)")) {
                var values = Regex.Split(transform.Groups[2].Value.Trim(), @"[,\s]+").Select(N).ToArray();
                if (values.Length < 1 || values.Length > 2) throw new InvalidDataException("Invalid icon transform.");
                if (transform.Groups[1].Value == "translate") g.TranslateTransform(values[0], values.Length == 2 ? values[1] : 0);
                else g.ScaleTransform(values[0], values.Length == 2 ? values[1] : values[0]);
            }
            foreach (XmlNode child in node.ChildNodes) if (child is XmlElement element) Draw(element, g);
            g.Restore(state); return;
        }
        if (node.LocalName != "path") throw new InvalidDataException("Unsupported original SVG element.");
        var t = Regex.Matches(node.GetAttribute("d"), @"[MLCZ]|-?\d+(?:\.\d+)?").Select(m => m.Value).ToArray();
        using var p = new GraphicsPath(); float x=0,y=0; int i=0;
        while(i<t.Length) {
            var command=t[i++];
            if(command=="M") { x=N(t[i++]); y=N(t[i++]); p.StartFigure(); }
            else if(command=="L") { float nx=N(t[i++]),ny=N(t[i++]); p.AddLine(x,y,nx,ny); x=nx;y=ny; }
            else if(command=="C") { float a=N(t[i++]),b=N(t[i++]),c=N(t[i++]),d=N(t[i++]),nx=N(t[i++]),ny=N(t[i++]); p.AddBezier(x,y,a,b,c,d,nx,ny);x=nx;y=ny; }
            else if(command=="Z") p.CloseFigure();
            else throw new InvalidDataException("Unsupported original SVG command.");
        }
        var fill=node.GetAttribute("fill");
        if(fill!="none") { using var brush=new SolidBrush(ColorTranslator.FromHtml(fill)); g.FillPath(brush,p); }
        if(node.HasAttribute("stroke")) { using var pen=new Pen(ColorTranslator.FromHtml(node.GetAttribute("stroke")),N(node.GetAttribute("stroke-width"))) { LineJoin=LineJoin.Round, StartCap=LineCap.Round,EndCap=LineCap.Round }; g.DrawPath(pen,p); }
    }
    public static byte[] Render(string svg, int size) {
        var doc = new XmlDocument(); doc.Load(svg);
        var pixelArt = int.TryParse(doc.DocumentElement.GetAttribute("data-pixel-grid"), out int grid);
        if (pixelArt && (grid < 16 || grid > 256)) throw new InvalidDataException("Invalid pixel-art grid.");
        int canvas = pixelArt ? grid : size;
        using var original = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
        using (var drawing = Graphics.FromImage(original)) {
            drawing.Clear(Color.Transparent);
            drawing.SmoothingMode = pixelArt ? SmoothingMode.None : SmoothingMode.AntiAlias;
            if (!pixelArt) drawing.ScaleTransform(size / 256f, size / 256f);
            foreach (XmlNode child in doc.DocumentElement.ChildNodes) if (child is XmlElement element) Draw(element, drawing);
        }
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(original, new Rectangle(0, 0, size, size), 0, 0, canvas, canvas, GraphicsUnit.Pixel);
        using var stream=new MemoryStream(); bitmap.Save(stream,ImageFormat.Png);return stream.ToArray();
    }
}
'@
}
foreach ($icon in @(@('src/Tka.Installer/Assets', 'techno-kitty'), @('src/Tka.Host/Assets', 'game-kitty'))) {
$assets = Join-Path $root $icon[0]
$name = $icon[1]
$svg = Join-Path $assets ($name + '.svg')
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @($sizes | ForEach-Object { ,([TkaIconRenderer]::Render($svg, $_)) })
$stream = [IO.File]::Create((Join-Path $assets ($name + '.ico')))
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset); $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
New-Item -ItemType Directory -Path (Join-Path $root 'out') -Force | Out-Null
[IO.File]::WriteAllBytes((Join-Path $root ('out/' + $name + '-icon.png')), [TkaIconRenderer]::Render($svg, 512))
Write-Output ($name + ' icon built: 9 sizes, 16–256 pixels.')
}
