using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Text;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Erkannter Text einer gespeicherten Seite.
/// </summary>
/// <param name="PageIndex">Position der Seite (0-basiert).</param>
/// <param name="Text">Erkannter Text (Koordinaten der ungedrehten Seite, ohne geschwärzte Wörter).</param>
/// <param name="Rotation">Drehung aus der Bearbeitung.</param>
/// <param name="Width">Breite der ungedrehten Seite (mit Verlängerung).</param>
/// <param name="Height">Höhe der ungedrehten Seite (mit Verlängerung).</param>
internal sealed record PageRecognizedText(int PageIndex, RecognizedPage Text, PageRotation Rotation, double Width, double Height);

/// <summary>
/// Schreibt erkannten Text als unsichtbare Textebene (Textmodus 3) in eine Seite (A53): zeilenweise in Helvetica, jedes
/// Wort auf seine Breite gestreckt, damit Markieren, Kopieren und Suchen in jedem Betrachter an der richtigen Stelle
/// und in der richtigen Reihenfolge wirken.
/// </summary>
internal static class RecognizedTextWriter
{
    /// <summary>Name der Schrift in den Seitenressourcen.</summary>
    private const string FontName = "/PUocr";

    /// <summary>
    /// Schreibt den Text in die Seite; der bisherige Seiteninhalt wird in q/Q eingeschlossen.
    /// </summary>
    /// <param name="page">Die Seite der Zieldatei (mit ihrer endgültigen Drehung).</param>
    /// <param name="geometry">Geometrie der Seite mit endgültiger Drehung.</param>
    /// <param name="text">Der erkannte Text.</param>
    public static void Write(PdfPage page, PageGeometry geometry, PageRecognizedText text)
    {
        if (text.Text.Words.Count == 0)
        {
            return;
        }

        var content = new ContentBuilder();
        foreach (var line in text.Text.Words.GroupBy(word => word.Line))
        {
            WriteLine(content, geometry, text, line.ToList());
        }

        AddFont(page);
        SetBytes(page.Contents.PrependContent(), "q\n"u8.ToArray());
        SetBytes(page.Contents.AppendContent(), [.. "Q\n"u8.ToArray(), .. content.ToBytes()]);
    }

    /// <summary>
    /// Eine Zeile: alle Wörter auf einer gemeinsamen Grundlinie in einer gemeinsamen Schriftgröße, damit Betrachter
    /// sie beim Kopieren und Auslesen als eine Zeile in Leserichtung erkennen (P14). Jedes Wort steht an seiner
    /// erkannten Stelle und ist auf seine Breite gestreckt.
    /// </summary>
    private static void WriteLine(ContentBuilder content, PageGeometry geometry, PageRecognizedText text, IReadOnlyList<RecognizedWord> words)
    {
        var (across, along) = TextAnnotation.Axes(text.Text.Orientation);
        // Lage der Zeile quer zur Leserichtung: oberster und unterster Rand aller Wörter
        var corners = words.Select(word => Corners(word.Bounds)).ToList();
        var top = corners.SelectMany(points => points).Min(point => Dot(point, along));
        var bottom = corners.SelectMany(points => points).Max(point => Dot(point, along));
        var height = bottom - top;
        if (height <= 0)
        {
            return;
        }

        // Ober- und Unterlänge der Helvetica füllen die Zeilenhöhe
        var size = height * 1000 / (Helvetica.Ascent + Helvetica.Descent);
        var baseline = top + size * Helvetica.Ascent / 1000;
        content.Op($"BT 3 Tr {FontName} {ContentBuilder.N(size)} Tf");
        for (var i = 0; i < words.Count; i++)
        {
            var from = corners[i].Min(point => Dot(point, across));
            var to = corners[i].Max(point => Dot(point, across));
            WriteWord(content, geometry, text, words[i].Text, Point(across, along, from, baseline), Point(across, along, to, baseline), size);
        }

        content.Op("ET");
    }

    /// <summary>
    /// Ein Wort von <paramref name="start"/> bis <paramref name="end"/> auf der Grundlinie (Koordinaten der ungedrehten
    /// Seite), unsichtbar und auf die Wortlänge gestreckt.
    /// </summary>
    private static void WriteWord(ContentBuilder content, PageGeometry geometry, PageRecognizedText text, string word, PdfPoint start, PdfPoint end, double size)
    {
        var from = geometry.ToUser(PageTransform.Rotate(start, text.Rotation, text.Width, text.Height));
        var to = geometry.ToUser(PageTransform.Rotate(end, text.Rotation, text.Width, text.Height));
        var distance = from.DistanceTo(to);
        var natural = StandardFont.Width(word, size);
        if (distance <= 0 || natural <= 0)
        {
            return;
        }

        var (dx, dy) = ((to.X - from.X) / distance, (to.Y - from.Y) / distance);
        content.Op($"{ContentBuilder.N(Math.Clamp(100 * distance / natural, 1, 1000))} Tz "
            + $"{ContentBuilder.N(dx)} {ContentBuilder.N(dy)} {ContentBuilder.N(-dy)} {ContentBuilder.N(dx)} {ContentBuilder.N(from.X)} {ContentBuilder.N(from.Y)} Tm "
            + $"{StandardFont.ToPdfString(word)} Tj");
    }

    /// <summary>Punkt aus Lage in Leserichtung und quer dazu (die Achsen stehen senkrecht aufeinander).</summary>
    private static PdfPoint Point(PdfPoint across, PdfPoint along, double alongText, double acrossText) =>
        new(across.X * alongText + along.X * acrossText, across.Y * alongText + along.Y * acrossText);

    /// <summary>Ecken eines Rechtecks.</summary>
    private static PdfPoint[] Corners(PdfRect rect) =>
        [new(rect.X, rect.Y), new(rect.Right, rect.Y), new(rect.Right, rect.Bottom), new(rect.X, rect.Bottom)];

    /// <summary>Trägt Helvetica als Schrift in die Seitenressourcen ein.</summary>
    private static void AddFont(PdfPage page)
    {
        var document = page.Owner;
        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", "/Helvetica");
        font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        document.Internals.AddObject(font);

        var resources = page.Resources;
        if (resources.Elements.GetDictionary("/Font") is not { } fonts)
        {
            fonts = new PdfDictionary(document);
            resources.Elements["/Font"] = fonts;
        }

        fonts.Elements[FontName] = font.Reference;
    }

    /// <summary>Setzt den Inhalt eines Inhaltsstroms.</summary>
    private static void SetBytes(PdfContent content, byte[] bytes)
    {
        if (content.Stream is null)
        {
            content.CreateStream(bytes);
        }
        else
        {
            content.Stream.Value = bytes;
        }
    }

    private static double Dot(PdfPoint point, PdfPoint axis) => point.X * axis.X + point.Y * axis.Y;
}
