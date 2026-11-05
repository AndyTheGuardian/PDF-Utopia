using PdfUtopia.Core.Forms;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Tests.Forms;

/// <summary>
/// Tests des Ausfüllens (A50, 7b): Werte ändern, Rückgängig, Schreibschutz, Höchstlänge, Reihenfolge.
/// </summary>
public sealed class FormValuesTests
{
    private static FormField Text(string name, double x, double y, bool readOnly = false, int maxLength = 0) =>
        new(name, FormFieldKind.Text, "alt", readOnly, false, false, maxLength, 0, 0, [], [new FormWidget(1, new PdfRect(x, y, 100, 20), null)]);

    private static EditableDocument Document(params FormField[] fields)
    {
        var document = new EditableDocument(2);
        document.Form.Load(fields);
        return document;
    }

    [Fact]
    public void SetFieldValue_IsOneUndoStep_AndListedAsChanged()
    {
        var document = Document(Text("Name", 10, 10));

        Assert.True(document.SetFieldValue("Name", "neu"));
        Assert.Equal("neu", document.Form.Value("Name"));
        Assert.Equal(new Dictionary<string, string> { ["Name"] = "neu" }, document.Form.ChangedValues());
        Assert.True(document.IsModified);

        document.History.Undo();
        Assert.Equal("alt", document.Form.Value("Name"));
        Assert.Empty(document.Form.ChangedValues());
        Assert.False(document.IsModified);
    }

    [Fact]
    public void SetFieldValue_SameValue_ReadOnly_Unknown_NoStep()
    {
        var document = Document(Text("Name", 10, 10), Text("Nummer", 10, 40, readOnly: true));

        Assert.False(document.SetFieldValue("Name", "alt"));
        Assert.False(document.SetFieldValue("Nummer", "x"));
        Assert.False(document.SetFieldValue("Gibt es nicht", "x"));
        Assert.False(document.History.CanUndo);
    }

    [Fact]
    public void SetFieldValue_CutsToMaxLength()
    {
        var document = Document(Text("Datum", 10, 10, maxLength: 10));

        document.SetFieldValue("Datum", "05.11.2026 und mehr");

        Assert.Equal("05.11.2026", document.Form.Value("Datum"));
    }

    [Fact]
    public void Changed_ReportsFieldName()
    {
        var document = Document(Text("Name", 10, 10));
        var names = new List<string>();
        document.Form.Changed += (_, name) => names.Add(name);

        document.SetFieldValue("Name", "a");
        document.History.Undo();

        Assert.Equal(["Name", "Name"], names);
    }

    [Fact]
    public void OnPage_ReadingOrder()
    {
        var document = Document(Text("unten", 10, 200), Text("rechts", 300, 50), Text("links", 10, 51));

        Assert.Equal(["links", "rechts", "unten"], document.Form.OnPage(1).Select(item => item.Field.Name));
        Assert.Empty(document.Form.OnPage(2));
    }

    [Theory]
    [InlineData(0, false, 20, 11.2)]
    [InlineData(0, false, 40, 12)]
    [InlineData(0, true, 60, 10)]
    [InlineData(14, false, 20, 14)]
    public void FontSizeFor(double fontSize, bool multiline, double height, double expected)
    {
        var field = Text("x", 0, 0) with { FontSize = fontSize, Multiline = multiline };
        Assert.Equal(expected, field.FontSizeFor(height), 6);
    }
}
