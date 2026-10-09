using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Tka.Installer;

internal static class InstallerExtras
{
    private static readonly string[] UnlockFields =
        ["seaHigh_", "cloudHigh_", "lavaHigh_", "meatHigh_", "ronHigh_"];

    // The full game already exposes all five stages. Its kitten locks use
    // these five >=80,000 per-stage score thresholds in ScoreInfo. Work only
    // on the staged copy; the previous save remains in the install backup.
    public static void UnlockAll(string game)
    {
        var path = Path.Combine(game, "userdata", "profile", "Techno Kitten Adventure", "ScoreInfo");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        XDocument document;
        if (File.Exists(path))
        {
            if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException("ScoreInfo save is unexpectedly large.");
            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            document = XDocument.Load(reader);
        }
        else
        {
            document = new XDocument(new XElement("ScoreInfo",
                new XElement("highScore_", 0),
                UnlockFields.Select(name => new XElement(name, 0)),
                new XElement("stageIndexes_", new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4 }.Select(n => new XElement("int", n))),
                new XElement("catIndexes_", new[] { 0, 1, 3, 4, 6, 7, 9, 10, 12, 13 }.Select(n => new XElement("int", n))),
                new XElement("scores_", Enumerable.Range(0, 10).Select(_ => new XElement("int", 0)))));
        }
        var root = document.Root;
        if (root?.Name != "ScoreInfo") throw new InvalidDataException("Unsupported ScoreInfo save format.");
        foreach (var name in UnlockFields)
        {
            var field = root.Elements(name).SingleOrDefault() ?? throw new InvalidDataException("Missing ScoreInfo field: " + name);
            var score = int.Parse(field.Value, NumberStyles.None, CultureInfo.InvariantCulture);
            field.Value = Math.Max(score, 80_000).ToString(CultureInfo.InvariantCulture);
        }
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        document.Save(output);
        output.Flush(flushToDisk: true);
    }
}
