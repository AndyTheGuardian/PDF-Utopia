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
/// Schreibt erkannten Text als unsichtbare Textebene (Textmodus 3) in eine Seite (A53): je Wort eine Zeile in Helvetica,
/// auf die Wortbreite gestreckt, damit Markieren, Kopieren und Suchen in jedem Betrachter an der richtigen Stelle wirken.
/// </summary>
internal static class RecognizedTextWriter
{
    /// <summary>Name der Schrift in den Seitenressourcen.</summary>
    private const string FontName = "/PUocr";

    /// <summary>Anteil der Wortbox, um den die Grundlinie über dem unteren Rand liegt (Unterlängen).</summary>
    private const double BaselineRatio = 0.2;

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
        foreach (var word in text.Text.Words)
        {
            WriteWord(content, geometry, text, word);
        }

        AddFont(page);
        SetBytes(page.Contents.PrependContent(), "q\n"u8.ToArray());
        SetBytes(page.Contents.AppendContent(), [.. "Q\n"u8.ToArray(), .. content.ToBytes()]);
    }

    /// <summary>
    /// Ein Wort: Grundlinie und Leserichtung aus der Wortbox, unsichtbar, auf die Wortlänge gestreckt.
    /// </summary>
    private static void WriteWord(ContentBuilder content, PageGeometry geometry, PageRecognizedText text, RecognizedWord word)
    {
        var (across, along) = TextAnnotation.Axes(text.Text.Orientation);
        var bounds = word.Bounds;
        var horizontal = across.X != 0;
        var (length, height) = horizontal ? (bounds.Width, bounds.Height) : (bounds.Height, bounds.Width);
        if (length <= 0 || height <= 0)
        {
            return;
        }

        // Ecke links unten in Leserichtung: kleinste Lage in Leserichtung, größte in Zeilenrichtung
        PdfPoint[] corners = [new(bounds.X, bounds.Y), new(bounds.Right, bounds.Y), new(bounds.Right, bounds.Bottom), new(bounds.X, bounds.Bottom)];
        var corner = corners.OrderBy(point => Dot(point, across)).ThenByDescending(point => Dot(point, along)).First();
        var start = new PdfPoint(corner.X - along.X * height * BaselineRatio, corner.Y - along.Y * height * BaselineRatio);
        var end = new PdfPoint(start.X + across.X * length, start.Y + across.Y * length);

        var from = geometry.ToUser(PageTransform.Rotate(start, text.Rotation, text.Width, text.Height));
        var to = geometry.ToUser(PageTransform.Rotate(end, text.Rotation, text.Width, text.Height));
        var distance = from.DistanceTo(to);
        var size = Math.Max(1, height * 0.95);
        var natural = StandardFont.Width(word.Text, size);
        if (distance <= 0 || natural <= 0)
        {
            return;
        }

        var (dx, dy) = ((to.X - from.X) / distance, (to.Y - from.Y) / distance);
        content.Op($"BT 3 Tr {FontName} {ContentBuilder.N(size)} Tf {ContentBuilder.N(Math.Clamp(100 * distance / natural, 1, 1000))} Tz "
            + $"{ContentBuilder.N(dx)} {ContentBuilder.N(dy)} {ContentBuilder.N(-dy)} {ContentBuilder.N(dx)} {ContentBuilder.N(from.X)} {ContentBuilder.N(from.Y)} Tm "
            + $"{StandardFont.ToPdfString(word.Text)} Tj ET");
    }

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
