using System.Globalization;
using System.Text;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Baut einen PDF-Inhaltsstrom (Zeichenbefehle) für Erscheinungsbilder von Annotationen und für Testdateien.
/// Koordinaten im PDF-System: Ursprung links unten, Y nach oben, Einheit Punkt.
/// </summary>
internal sealed class ContentBuilder
{
    private const double CircleKappa = 0.5523;

    private readonly StringBuilder _content = new();

    /// <summary>
    /// Setzt die Strichfarbe.
    /// </summary>
    /// <param name="hex">Farbe <c>#RRGGBB</c>.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder StrokeColor(string hex) => Op($"{Rgb(hex)} RG");

    /// <summary>
    /// Setzt die Füllfarbe.
    /// </summary>
    /// <param name="hex">Farbe <c>#RRGGBB</c>.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder FillColor(string hex) => Op($"{Rgb(hex)} rg");

    /// <summary>
    /// Setzt die Strichstärke.
    /// </summary>
    /// <param name="width">Stärke in Punkten.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder LineWidth(double width) => Op($"{N(width)} w");

    /// <summary>
    /// Runde Linienenden und -verbindungen (für Freihand).
    /// </summary>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder RoundCaps() => Op("1 J 1 j");

    /// <summary>
    /// Gestrichelte Linie.
    /// </summary>
    /// <param name="dash">Strichlänge.</param>
    /// <param name="gap">Lückenlänge.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Dash(double dash, double gap) => Op($"[{N(dash)} {N(gap)}] 0 d");

    /// <summary>
    /// Aktiviert einen Grafikzustand (z. B. für Deckkraft), der in den Ressourcen definiert ist.
    /// </summary>
    /// <param name="name">Ressourcenname, z. B. <c>/GS0</c>.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder GraphicsState(string name) => Op($"{name} gs");

    /// <summary>Sichert den Grafikzustand.</summary>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Save() => Op("q");

    /// <summary>Stellt den Grafikzustand wieder her.</summary>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Restore() => Op("Q");

    /// <summary>
    /// Beginnt einen Pfad.
    /// </summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder MoveTo(double x, double y) => Op($"{N(x)} {N(y)} m");

    /// <summary>
    /// Gerade Linie zum Punkt.
    /// </summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder LineTo(double x, double y) => Op($"{N(x)} {N(y)} l");

    /// <summary>
    /// Kubische Bézier-Kurve.
    /// </summary>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder CurveTo(PdfPoint control1, PdfPoint control2, PdfPoint end) =>
        Op($"{N(control1.X)} {N(control1.Y)} {N(control2.X)} {N(control2.Y)} {N(end.X)} {N(end.Y)} c");

    /// <summary>
    /// Rechteck als Pfad.
    /// </summary>
    /// <param name="x">Linker Rand.</param>
    /// <param name="y">Unterer Rand.</param>
    /// <param name="width">Breite.</param>
    /// <param name="height">Höhe.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Rectangle(double x, double y, double width, double height) =>
        Op($"{N(x)} {N(y)} {N(width)} {N(height)} re");

    /// <summary>
    /// Kreis als Pfad aus vier Bézier-Kurven.
    /// </summary>
    /// <param name="centerX">Mittelpunkt X.</param>
    /// <param name="centerY">Mittelpunkt Y.</param>
    /// <param name="radius">Radius.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Circle(double centerX, double centerY, double radius)
    {
        var k = radius * CircleKappa;
        MoveTo(centerX + radius, centerY);
        CurveTo(new(centerX + radius, centerY + k), new(centerX + k, centerY + radius), new(centerX, centerY + radius));
        CurveTo(new(centerX - k, centerY + radius), new(centerX - radius, centerY + k), new(centerX - radius, centerY));
        CurveTo(new(centerX - radius, centerY - k), new(centerX - k, centerY - radius), new(centerX, centerY - radius));
        CurveTo(new(centerX + k, centerY - radius), new(centerX + radius, centerY - k), new(centerX + radius, centerY));
        return Op("h");
    }

    /// <summary>Zeichnet den Pfad als Linie.</summary>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Stroke() => Op("S");

    /// <summary>Füllt den Pfad.</summary>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Fill() => Op("f");

    /// <summary>Füllt den Pfad und zeichnet seinen Rand.</summary>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder FillAndStroke() => Op("B");

    /// <summary>
    /// Schreibt eine Textzeile in der Standardschrift.
    /// </summary>
    /// <param name="x">Linker Rand der Grundlinie.</param>
    /// <param name="y">Grundlinie.</param>
    /// <param name="text">Der Text.</param>
    /// <param name="size">Schriftgröße.</param>
    /// <param name="bold">Fette Schrift.</param>
    /// <returns>Dieser Builder.</returns>
    public ContentBuilder Text(double x, double y, string text, double size, bool bold = false) =>
        Op($"BT {(bold ? StandardFont.Bold : StandardFont.Regular)} {N(size)} Tf {N(x)} {N(y)} Td {StandardFont.ToPdfString(text)} Tj ET");

    /// <summary>
    /// Liefert den Inhaltsstrom als Bytes (nur ASCII).
    /// </summary>
    /// <returns>Die Bytes.</returns>
    public byte[] ToBytes() => Encoding.ASCII.GetBytes(_content.ToString());

    /// <inheritdoc />
    public override string ToString() => _content.ToString();

    /// <summary>
    /// Formatiert eine Zahl kulturunabhängig mit höchstens drei Nachkommastellen.
    /// </summary>
    /// <param name="value">Die Zahl.</param>
    /// <returns>Der Text.</returns>
    public static string N(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Farbe als drei Anteile 0–1.
    /// </summary>
    /// <param name="hex">Farbe <c>#RRGGBB</c>.</param>
    /// <returns>Der Text, z. B. <c>0.31 0.275 0.898</c>.</returns>
    public static string Rgb(string hex)
    {
        var (r, g, b) = Components(hex);
        return $"{N(r)} {N(g)} {N(b)}";
    }

    /// <summary>
    /// Zerlegt eine Farbe in Anteile 0–1.
    /// </summary>
    /// <param name="hex">Farbe <c>#RRGGBB</c>.</param>
    /// <returns>Rot, Grün, Blau.</returns>
    public static (double R, double G, double B) Components(string hex)
    {
        var value = hex.Trim().TrimStart('#');
        double Part(int i) => int.Parse(value.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return (Part(0), Part(1), Part(2));
    }

    /// <summary>
    /// Hängt einen Befehl an.
    /// </summary>
    private ContentBuilder Op(string op)
    {
        _content.Append(op).Append('\n');
        return this;
    }
}
