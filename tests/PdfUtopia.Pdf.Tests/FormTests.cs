using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Formulare bleiben beim Speichern und Extrahieren erhalten (A50, P7).
/// </summary>
public sealed class FormTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly string _file;

    public FormTests() => _file = TestPdfFactory.CreateWithForm(_temp.File("formular.pdf"));

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void FormFieldCount_CountsFieldsNotWidgets()
    {
        using var source = PdfSource.Open(_file);
        Assert.Equal(3, source.FormFieldCount);

        using var plain = PdfSource.Open(TestPdfFactory.Create(_temp.File("ohne.pdf")));
        Assert.Equal(0, plain.FormFieldCount);
    }

    [Fact]
    public void Save_KeepsFormWithValues()
    {
        var saved = Save(_ => { });

        Assert.Equal(["Name", "Newsletter", "Anrede"], FieldNames(saved));
        using var pdf = PdfReader.Open(saved, PdfDocumentOpenMode.Import);
        var fields = Fields(pdf);
        Assert.Equal("Max Muster", fields[0].Elements.GetString("/V"));
        Assert.Equal("/Yes", fields[1].Elements.GetName("/V"));
        Assert.Equal(2, fields[2].Elements.GetArray("/Kids")!.Elements.Count);
        using var reopened = PdfSource.Open(saved);
        Assert.Equal(3, reopened.FormFieldCount);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void FormFields_AreDrawn(int pageNumber, bool hasField) =>
        Assert.Equal(hasField, DifferingPixels(_file, pageNumber) > 50);

    [Fact]
    public void Save_FieldsStayVisible() =>
        Assert.True(DifferingPixels(Save(_ => { }), 1) > 50, "Feldwert „Max Muster“ sollte nach dem Speichern sichtbar sein");

    [Fact]
    public void Save_DeletedPage_DropsItsFields()
    {
        var saved = Save(document => document.DeletePage(1));

        Assert.Equal(["Name", "Anrede"], FieldNames(saved));
    }

    [Fact]
    public void Extract_KeepsOnlyFieldsOfExtractedPages()
    {
        using var source = PdfSource.Open(_file);
        var target = _temp.File("auszug.pdf");

        PdfExporter.Extract(source, source.CreateDocument(), [3], target);

        Assert.Equal(["Anrede"], FieldNames(target));
    }

    [Fact]
    public void Save_FileWithoutForm_HasNoForm()
    {
        using var source = PdfSource.Open(TestPdfFactory.Create(_temp.File("ohne.pdf")));
        var target = _temp.File("ohne-gespeichert.pdf");
        PdfExporter.Save(source, source.CreateDocument(), target);

        using var pdf = PdfReader.Open(target, PdfDocumentOpenMode.Import);
        Assert.False(pdf.Internals.Catalog.Elements.ContainsKey("/AcroForm"));
    }

    [Fact]
    public void Save_InsertedForm_RenamesDuplicateFields()
    {
        using var opened = PdfSource.Open(_file);
        using var other = PdfSource.Open(TestPdfFactory.CreateWithForm(_temp.File("zweites.pdf"), "Erika Muster"));
        var document = opened.CreateDocument();
        document.InsertPages(document.Pages.Count, 1, [1]);
        var target = _temp.File("zusammen.pdf");

        PdfExporter.Save([opened, other], document, target);

        Assert.Equal(["Name", "Newsletter", "Anrede", "Name_2"], FieldNames(target));
        using var pdf = PdfReader.Open(target, PdfDocumentOpenMode.Import);
        Assert.Equal("Erika Muster", Fields(pdf)[3].Elements.GetString("/V"));
    }

    [Fact]
    public void FormFields_AreReadWithKindValueAndPosition()
    {
        using var source = PdfSource.Open(_file);
        var fields = source.FormFields;

        Assert.Equal(["Name", "Newsletter", "Anrede"], fields.Select(field => field.Name));
        Assert.Equal([Core.Forms.FormFieldKind.Text, Core.Forms.FormFieldKind.CheckBox, Core.Forms.FormFieldKind.Radio], fields.Select(field => field.Kind));
        Assert.Equal(["Max Muster", "Yes", "Frau"], fields.Select(field => field.InitialValue));
        var name = Assert.Single(fields[0].Widgets);
        Assert.Equal(1, name.SourcePageNumber);
        Assert.Equal(100, name.Bounds.X, 1);
        Assert.Equal(TestPdfFactory.Height - 720, name.Bounds.Y, 1);
        Assert.Equal((200, 20), (Math.Round(name.Bounds.Width), Math.Round(name.Bounds.Height)));
        Assert.Equal(12, fields[0].FontSize);
        Assert.Equal("Yes", Assert.Single(fields[1].Widgets).OnState);
        Assert.Equal(["Frau", "Herr"], fields[2].Widgets.Select(widget => widget.OnState));
        Assert.Equal(3, Assert.Single(fields[2].Widgets.Select(widget => widget.SourcePageNumber).Distinct()));
    }

    [Fact]
    public void Save_WritesChangedValues_AndAppearance()
    {
        var saved = Save(document =>
        {
            document.SetFieldValue("Name", "Erika Beispiel");
            document.SetFieldValue("Newsletter", "Off");
            document.SetFieldValue("Anrede", "Herr");
        });

        using var reopened = PdfSource.Open(saved);
        Assert.Equal(["Erika Beispiel", "Off", "Herr"], reopened.FormFields.Select(field => field.InitialValue));
        using var pdf = PdfReader.Open(saved, PdfDocumentOpenMode.Import);
        var radios = Fields(pdf)[2].Elements.GetArray("/Kids")!.Elements
            .Select(item => ((PdfSharp.Pdf.Advanced.PdfReference)item).Value as PdfDictionary).Select(widget => widget!.Elements.GetName("/AS"));
        Assert.Equal(["/Off", "/Herr"], radios);
        // Neues Erscheinungsbild: Seite 1 sieht anders aus als vorher
        using var before = PdfSource.Open(_file);
        Assert.NotEqual(before.Render(1, 1).Pixels, reopened.Render(1, 1).Pixels);
    }

    [Fact]
    public void Save_UnchangedValues_KeepOriginalAppearance()
    {
        var saved = Save(_ => { });

        using var pdf = PdfReader.Open(saved, PdfDocumentOpenMode.Import);
        Assert.False(pdf.Internals.Catalog.Elements.GetDictionary("/AcroForm")!.Elements.GetBoolean("/NeedAppearances"));
        using var reopened = PdfSource.Open(saved);
        Assert.Equal("Max Muster", reopened.FormFields[0].InitialValue);
    }

    /// <summary>
    /// Pixel einer Seite, die sich von derselben Seite ohne Formular unterscheiden – also das, was die Felder zeichnen.
    /// </summary>
    private int DifferingPixels(string path, int pageNumber)
    {
        using var withForm = PdfSource.Open(path);
        using var plain = PdfSource.Open(TestPdfFactory.Create(_temp.File($"vergleich-{Guid.NewGuid():N}.pdf")));
        var (a, b) = (withForm.Render(pageNumber, 1), plain.Render(pageNumber, 1));
        var count = 0;
        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                count += a.PixelAt(x, y) != b.PixelAt(x, y) ? 1 : 0;
            }
        }

        return count;
    }

    /// <summary>
    /// Speichert die Formulardatei nach einer Änderung.
    /// </summary>
    private string Save(Action<EditableDocument> edit)
    {
        using var source = PdfSource.Open(_file);
        var document = source.CreateDocument();
        edit(document);
        var target = _temp.File($"{Guid.NewGuid():N}.pdf");
        PdfExporter.Save(source, document, target);
        return target;
    }

    /// <summary>Namen der obersten Felder im Formular.</summary>
    private static List<string> FieldNames(string path)
    {
        using var pdf = PdfReader.Open(path, PdfDocumentOpenMode.Import);
        return Fields(pdf).Select(field => field.Elements.GetString("/T")).ToList();
    }

    /// <summary>Oberste Felder des Formulars.</summary>
    private static List<PdfDictionary> Fields(PdfDocument pdf) =>
        pdf.Internals.Catalog.Elements.GetDictionary("/AcroForm")?.Elements.GetArray("/Fields")?.Elements
            .Select(item => item is PdfSharp.Pdf.Advanced.PdfReference reference ? reference.Value as PdfDictionary : item as PdfDictionary)
            .OfType<PdfDictionary>().ToList() ?? [];
}
