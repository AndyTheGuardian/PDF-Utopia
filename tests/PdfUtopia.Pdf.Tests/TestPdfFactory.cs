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
    /// Testdatei mit Formular (A50): Textfeld „Name“ auf S. 1 (Wert „Max Muster“), Kontrollkästchen „Newsletter“ auf
    /// S. 2 (angekreuzt), Optionsfelder „Anrede“ mit „Frau“ und „Herr“ auf S. 3 („Frau“ gewählt). Alle mit
    /// Erscheinungsbild, damit sie ohne Neuaufbau sichtbar sind.
    /// </summary>
    /// <param name="path">Zieldatei.</param>
    /// <param name="nameValue">Wert des Textfelds.</param>
    /// <returns>Der Pfad.</returns>
    public static string CreateWithForm(string path, string nameValue = "Max Muster")
    {
        var plain = Create(path + ".ohne.pdf");
        using (var document = PdfReader.Open(plain, PdfDocumentOpenMode.Modify))
        {
            var helv = Font(document, "/Helvetica");
            var name = Widget(document, document.Pages[0], 100, 700, 300, 720);
            name.Elements.SetName("/FT", "/Tx");
            name.Elements["/T"] = new PdfString("Name");
            name.Elements["/V"] = new PdfString(nameValue);
            name.Elements.SetString("/DA", "/Helv 12 Tf 0 g");
            SetAppearance(document, name, "/N", 200, 20, $"BT /Helv 12 Tf 2 5 Td ({nameValue}) Tj ET", helv);

            var newsletter = Field(document, "/Btn", "Newsletter");
            newsletter.Elements.SetName("/V", "/Yes");
            var box = Widget(document, document.Pages[1], 100, 700, 115, 715);
            Kid(newsletter, box);
            box.Elements.SetName("/AS", "/Yes");
            SetStates(document, box, 15, 15, "/Yes", "0 0 0 RG 1 w 2 2 m 7 12 l 13 3 l S");

            var salutation = Field(document, "/Btn", "Anrede");
            salutation.Elements.SetInteger("/Ff", 49152);
            salutation.Elements.SetName("/V", "/Frau");
            foreach (var (state, x) in new[] { ("/Frau", 100), ("/Herr", 200) })
            {
                var radio = Widget(document, document.Pages[2], x, 700, x + 15, 715);
                Kid(salutation, radio);
                radio.Elements.SetName("/AS", state == "/Frau" ? "/Frau" : "/Off");
                SetStates(document, radio, 15, 15, state, "0 g 4 4 7 7 re f");
            }

            var acroForm = new PdfDictionary(document);
            acroForm.Elements["/Fields"] = new PdfArray(document, name.Reference!, newsletter.Reference!, salutation.Reference!);
            acroForm.Elements.SetString("/DA", "/Helv 0 Tf 0 g");
            document.Internals.Catalog.Elements["/AcroForm"] = acroForm;
            document.Save(path);
        }

        File.Delete(plain);
        return path;
    }

    /// <summary>Widget-Annotation auf einer Seite.</summary>
    private static PdfDictionary Widget(PdfDocument document, PdfPage page, double left, double bottom, double right, double top)
    {
        var widget = new PdfDictionary(document);
        widget.Elements.SetName("/Type", "/Annot");
        widget.Elements.SetName("/Subtype", "/Widget");
        widget.Elements["/Rect"] = new PdfArray(document, new PdfReal(left), new PdfReal(bottom), new PdfReal(right), new PdfReal(top));
        widget.Elements.SetInteger("/F", 4);
        document.Internals.AddObject(widget);
        widget.Elements["/P"] = page.Reference;
        if (page.Elements.GetArray("/Annots") is not { } annots)
        {
            annots = new PdfArray(document);
            page.Elements["/Annots"] = annots;
        }

        annots.Elements.Add(widget.Reference!);
        return widget;
    }

    /// <summary>Feld ohne eigenes Widget (Elternteil seiner Widgets).</summary>
    private static PdfDictionary Field(PdfDocument document, string type, string name)
    {
        var field = new PdfDictionary(document);
        field.Elements.SetName("/FT", type);
        field.Elements["/T"] = new PdfString(name);
        field.Elements["/Kids"] = new PdfArray(document);
        document.Internals.AddObject(field);
        return field;
    }

    /// <summary>Hängt ein Widget an ein Feld.</summary>
    private static void Kid(PdfDictionary field, PdfDictionary widget)
    {
        field.Elements.GetArray("/Kids")!.Elements.Add(widget.Reference!);
        widget.Elements["/Parent"] = field.Reference;
    }

    /// <summary>Erscheinungsbild mit einem Inhaltsstrom.</summary>
    private static void SetAppearance(PdfDocument document, PdfDictionary widget, string key, double width, double height, string content, PdfDictionary? font)
    {
        var appearance = new PdfDictionary(document);
        appearance.Elements[key] = Stream(document, width, height, content, font).Reference;
        widget.Elements["/AP"] = appearance;
    }

    /// <summary>Erscheinungsbilder „an“ und „aus“ eines Kästchens oder Optionsfelds.</summary>
    private static void SetStates(PdfDocument document, PdfDictionary widget, double width, double height, string onState, string onContent)
    {
        var states = new PdfDictionary(document);
        states.Elements[onState] = Stream(document, width, height, "0 0 0 RG 0.5 w 0.5 0.5 14 14 re S " + onContent, null).Reference;
        states.Elements["/Off"] = Stream(document, width, height, "0 0 0 RG 0.5 w 0.5 0.5 14 14 re S", null).Reference;
        var appearance = new PdfDictionary(document);
        appearance.Elements["/N"] = states;
        widget.Elements["/AP"] = appearance;
    }

    /// <summary>Form-XObject.</summary>
    private static PdfDictionary Stream(PdfDocument document, double width, double height, string content, PdfDictionary? font)
    {
        var form = new PdfDictionary(document);
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements["/BBox"] = new PdfArray(document, new PdfReal(0), new PdfReal(0), new PdfReal(width), new PdfReal(height));
        if (font is not null)
        {
            var fonts = new PdfDictionary(document);
            fonts.Elements["/Helv"] = font.Reference;
            var resources = new PdfDictionary(document);
            resources.Elements["/Font"] = fonts;
            form.Elements["/Resources"] = resources;
        }

        form.CreateStream(System.Text.Encoding.ASCII.GetBytes(content));
        document.Internals.AddObject(form);
        return form;
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
