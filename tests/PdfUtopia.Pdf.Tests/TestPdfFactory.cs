using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfUtopia.Pdf.Export;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Erzeugt die PDF-Testdatei (P1): A4-Seiten mit der Überschrift „Seite N“, vier Absätzen und einer grauen
/// Abbildung. Eine Seite ist in der Datei um 90° gedreht, optional ist die Datei mit Passwort geschützt.
/// Text in Helvetica, damit PDFium ihn wieder auslesen kann (Prüfung der Seitenreihenfolge).
/// </summary>
internal static class TestPdfFactory
{
    /// <summary>Seitenanzahl der Testdatei.</summary>
    public const int PageCount = 14;

    /// <summary>Seite, die in der Datei um 90° gedreht ist.</summary>
    public const int RotatedPage = 3;

    /// <summary>A4-Breite in Punkten (gerundet).</summary>
    public const double Width = 595;

    /// <summary>A4-Höhe in Punkten (gerundet).</summary>
    public const double Height = 842;

    /// <summary>Passwort der geschützten Testdatei.</summary>
    public const string Password = "utopia";

    private const string Paragraph =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Integer posuere erat a ante venenatis dapibus.";

    /// <summary>
    /// Schreibt die Testdatei.
    /// </summary>
    /// <param name="path">Zieldatei.</param>
    /// <param name="password">Passwort oder <c>null</c> für eine ungeschützte Datei.</param>
    /// <returns>Der Pfad.</returns>
    public static string Create(string path, string? password = null)
    {
        using var document = new PdfDocument();
        var font = Font(document, "/Helvetica");
        var bold = Font(document, "/Helvetica-Bold");
        for (var number = 1; number <= PageCount; number++)
        {
            AddPage(document, number, font, bold);
        }

        if (password is not null)
        {
            document.SecuritySettings.UserPassword = password;
            document.SecuritySettings.OwnerPassword = password;
        }

        document.Save(path);
        return path;
    }

    /// <summary>
    /// Testdatei mit Lesezeichen: Kapitel 1 (S. 1) mit Abschnitt 1.1 (S. 2), Kapitel 2 (S. 5), Gliederungspunkt
    /// „Anhang“ ohne Ziel mit einem Unterpunkt auf S. 14.
    /// </summary>
    /// <param name="path">Zieldatei.</param>
    /// <returns>Der Pfad.</returns>
    public static string CreateWithBookmarks(string path)
    {
        var plain = Create(path + ".ohne.pdf");
        using (var document = PdfReader.Open(plain, PdfDocumentOpenMode.Modify))
        {
            var chapter = document.Outlines.Add("Kapitel 1", document.Pages[0], true);
            chapter.Outlines.Add("Abschnitt 1.1", document.Pages[1]);
            document.Outlines.Add("Kapitel 2 – Übersicht", document.Pages[4]);
            var appendix = new PdfOutline { Title = "Anhang", Opened = true };
            document.Outlines.Add(appendix);
            appendix.Outlines.Add("Letzte Seite", document.Pages[13]);
            document.Save(path);
        }

        File.Delete(plain);
        return path;
    }

    /// <summary>
    /// Fügt eine Seite hinzu.
    /// </summary>
    private static void AddPage(PdfDocument document, int number, PdfDictionary font, PdfDictionary bold)
    {
        var page = document.AddPage();
        page.MediaBox = new PdfRectangle(new XPoint(0, 0), new XPoint(Width, Height));
        if (number == RotatedPage)
        {
            page.Rotate = 90;
        }

        var fonts = new PdfDictionary(document);
        fonts.Elements[StandardFont.Regular] = font.Reference;
        fonts.Elements[StandardFont.Bold] = bold.Reference;
        page.Resources.Elements["/Font"] = fonts;

        var content = new ContentBuilder().FillColor("#222222").Text(70, 770, $"Seite {number}", 24, bold: true);
        for (var i = 0; i < 4; i++)
        {
            content.Text(70, 720 - i * 40, $"{i + 1}. {Paragraph}"[..60], 11);
        }

        content.FillColor("#D9DEEA").Rectangle(70, 420, 455, 120).Fill();
        page.Contents.AppendContent().CreateStream(content.ToBytes());
    }

    /// <summary>
    /// Standardschrift als Schriftobjekt.
    /// </summary>
    private static PdfDictionary Font(PdfDocument document, string baseFont)
    {
        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", baseFont);
        font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        document.Internals.AddObject(font);
        return font;
    }
}
