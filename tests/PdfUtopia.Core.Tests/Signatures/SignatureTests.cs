using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Signatures;

namespace PdfUtopia.Core.Tests.Signatures;

/// <summary>
/// Tests der Unterschrift (A49): Setzen, Verschieben, Drehen, Radierer, Speichern.
/// </summary>
public sealed class SignatureTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"pdfutopia-sig-{Guid.NewGuid():N}");

    // Striche von (100, 50) bis (300, 150): 200 × 100 im Unterschriftenfeld
    private static readonly Signature Sample = new("Unterschrift", "#1E3A8A", true,
    [
        [new(new PdfPoint(100, 50), 0.5f), new(new PdfPoint(200, 150), 0.7f)],
        [new(new PdfPoint(200, 100), 0.5f), new(new PdfPoint(300, 120), 0.6f)],
    ]);

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Bounds_AndHeightKeepAspect()
    {
        Assert.Equal(new PdfRect(100, 50, 200, 100), Sample.Bounds);
        Assert.Equal(75, Sample.HeightFor(150));
    }

    [Fact]
    public void Place_ScalesIntoRectangle_AndScalesStrokeWidth()
    {
        var placed = SignatureAnnotation.Place(Sample, 10, 20, 100, point => point);

        Assert.Equal(new PdfRect(10, 20, 100, 50), placed.Bounds);
        Assert.Equal(Signature.PadStrokeWidth * 0.5, placed.Width);
        Assert.Equal(2, placed.Strokes.Count);
        Assert.Equal(0.7f, placed.Strokes[0][1].Pressure);
        Assert.True(placed.PressureSensitive);
    }

    [Fact]
    public void Place_UsesPageMapping()
    {
        var placed = SignatureAnnotation.Place(Sample, 10, 20, 100, point => new PdfPoint(point.X + 1000, point.Y));
        Assert.Equal(1010, placed.Bounds.X);
    }

    [Fact]
    public void Translate_MovesAllStrokes_KeepsId()
    {
        var placed = SignatureAnnotation.Place(Sample, 10, 20, 100, point => point);
        var moved = placed.Translate(5, -10);

        Assert.Equal(placed.Id, moved.Id);
        Assert.Equal(new PdfRect(15, 10, 100, 50), moved.Bounds);
    }

    [Fact]
    public void Rotate_MovesPoints()
    {
        var placed = SignatureAnnotation.Place(Sample, 10, 20, 100, point => point);
        var rotated = (SignatureAnnotation)PageTransform.Rotate(placed, PageRotation.Clockwise90, 600, 800);
        Assert.Equal(PageTransform.Rotate(placed.Bounds, PageRotation.Clockwise90, 600, 800), rotated.Bounds);
    }

    [Fact]
    public void Eraser_HitsSignatureArea()
    {
        var placed = SignatureAnnotation.Place(Sample, 10, 20, 100, point => point);
        Assert.True(EraserHitTest.IsErasable(placed));
        Assert.True(EraserHitTest.Hits(placed, new PdfPoint(60, 45), 1));
        Assert.False(EraserHitTest.Hits(placed, new PdfPoint(60, 200), 1));
    }

    [Fact]
    public void Overview_ListsSignatureUnderFreehand()
    {
        var document = new EditableDocument(1);
        document.AddAnnotation(1, SignatureAnnotation.Place(Sample, 10, 20, 100, point => point));

        var entry = Assert.Single(AnnotationOverview.Build(document));
        Assert.Equal((AnnotationCategory.Freehand, AnnotationLabel.Signature), (entry.Category, entry.Label));
    }

    [Fact]
    public void Store_SavesAndLoads_AtMostFive()
    {
        var store = new SignatureStore(Path.Combine(_folder, "signatures.json"));
        store.Save(Enumerable.Range(1, 7).Select(i => Sample with { Name = $"U{i}" }).ToList());

        var loaded = store.Load();

        Assert.Equal(["U1", "U2", "U3", "U4", "U5"], loaded.Select(signature => signature.Name));
        Assert.Equal(Sample.Strokes[1][1], loaded[0].Strokes[1][1]);
    }

    [Fact]
    public void Store_MissingOrBrokenFile_IsEmpty_AndUnknownColorIsReplaced()
    {
        var path = Path.Combine(_folder, "signatures.json");
        var store = new SignatureStore(path);
        Assert.Empty(store.Load());

        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, "{ kaputt");
        Assert.Empty(store.Load());

        store.Save([Sample with { ColorHex = "#123456" }]);
        Assert.Equal(Signature.Colors[0], Assert.Single(store.Load()).ColorHex);
    }

    [Fact]
    public void Colors_IncludeDarkAndLightRed() => Assert.Equal(5, Signature.Colors.Count);
}
