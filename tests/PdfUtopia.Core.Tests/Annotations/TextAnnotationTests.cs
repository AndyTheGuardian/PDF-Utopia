using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Text;

namespace PdfUtopia.Core.Tests.Annotations;

/// <summary>
/// Tests des Text-Werkzeugs (A43): Umbruch, Lage auf der Seite, Drehung, Radierer.
/// </summary>
public sealed class TextAnnotationTests
{
    private const double PageWidth = 600;
    private const double PageHeight = 800;

    private static TextAnnotation Text(string text = "Hallo Welt", PageRotation orientation = PageRotation.None, double width = 200) =>
        new(Guid.NewGuid(), "#1F2328", new PdfPoint(100, 120), width, 12, text, orientation);

    [Fact]
    public void Lines_WrapAtWidth_AndHeightGrows()
    {
        var single = Text("kurz");
        var wrapped = Text(string.Join(' ', Enumerable.Repeat("Wort", 30)));

        Assert.Single(single.Lines);
        Assert.Equal(single.LineHeight, single.Height);
        Assert.True(wrapped.Lines.Count > 1);
        Assert.All(wrapped.Lines, line => Assert.True(Helvetica.Width(line, 12) <= wrapped.Width));
        Assert.Equal(wrapped.Lines.Count * wrapped.LineHeight, wrapped.Height);
    }

    [Fact]
    public void Lines_KeepLineBreaks_AndEmptyTextHasOneLine()
    {
        Assert.Equal(["eins", "", "drei"], Text("eins\n\ndrei").Lines);
        Assert.Single(Text(string.Empty).Lines);
    }

    [Fact]
    public void Baseline_LiesWithinLine()
    {
        var text = Text("a\nb");
        Assert.InRange(text.Baseline(0), text.FontSize * 0.7, text.LineHeight);
        Assert.Equal(text.LineHeight, text.Baseline(1) - text.Baseline(0), 6);
    }

    [Fact]
    public void Bounds_Upright()
    {
        var text = Text();
        AssertRect(new PdfRect(100, 120, 200, text.Height), text.Bounds);
    }

    [Fact]
    public void Bounds_TurnedText_RunsUpwardsFromAnchor()
    {
        // Bei 90° steht der Text aufrecht; auf der ungedrehten Seite läuft er von unten nach oben
        var text = Text(orientation: PageRotation.Clockwise90);
        AssertRect(new PdfRect(100, 120 - 200, text.Height, 200), text.Bounds);
    }

    [Theory]
    [InlineData(PageRotation.None)]
    [InlineData(PageRotation.Clockwise90)]
    [InlineData(PageRotation.Rotate180)]
    [InlineData(PageRotation.Clockwise270)]
    public void CreatedOnRotatedPage_IsUprightInDisplay(PageRotation rotation)
    {
        // So legt die Oberfläche Text an: Anker von der Anzeige auf die ungedrehte Seite, Ausrichtung = Drehung
        var displayAnchor = new PdfPoint(50, 60);
        var page = PageTransform.Unrotate(displayAnchor, rotation, PageWidth, PageHeight);
        var text = new TextAnnotation(Guid.NewGuid(), "#000000", page, 150, 12, "Text", rotation);

        var display = Assert.IsType<TextAnnotation>(PageTransform.Rotate(text, rotation, PageWidth, PageHeight));

        Assert.Equal(PageRotation.None, display.Orientation);
        Assert.Equal(displayAnchor.X, display.Anchor.X, 6);
        Assert.Equal(displayAnchor.Y, display.Anchor.Y, 6);
    }

    [Theory]
    [InlineData(PageRotation.None, PageRotation.Clockwise90)]
    [InlineData(PageRotation.Clockwise90, PageRotation.Clockwise90)]
    [InlineData(PageRotation.Clockwise270, PageRotation.Rotate180)]
    public void Rotate_MovesBoundsLikeOtherAnnotations(PageRotation orientation, PageRotation rotation)
    {
        var text = Text(orientation: orientation) with { Anchor = new PdfPoint(300, 400) };

        var rotated = (TextAnnotation)PageTransform.Rotate(text, rotation, PageWidth, PageHeight);

        AssertRect(PageTransform.Rotate(text.Bounds, rotation, PageWidth, PageHeight), rotated.Bounds);
    }

    [Fact]
    public void Eraser_HitsTextArea()
    {
        var text = Text();
        Assert.True(EraserHitTest.IsErasable(text));
        Assert.True(EraserHitTest.Hits(text, new PdfPoint(150, 125), 2));
        Assert.False(EraserHitTest.Hits(text, new PdfPoint(150, 300), 2));
    }

    [Theory]
    [InlineData(2, 8)]
    [InlineData(12, 12)]
    [InlineData(99, 36)]
    public void ClampFontSize(double size, double expected) => Assert.Equal(expected, TextAnnotation.ClampFontSize(size));

    /// <summary>Vergleicht Rechtecke mit Rundungstoleranz.</summary>
    private static void AssertRect(PdfRect expected, PdfRect actual)
    {
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }
}
