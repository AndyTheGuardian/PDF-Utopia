using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Images;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Text;

namespace PdfUtopia.Core.Tests.Annotations;

/// <summary>
/// Tests für Stempel und Bilder (A51): gedrehtes Rechteck, Größe ändern, Drehen mit Einrasten (F37), Stempelaufbau,
/// Bilddaten und Einstellungen.
/// </summary>
public sealed class BoxAnnotationTests
{
    private static StampAnnotation Stamp(double angle = 0) =>
        new(Guid.NewGuid(), "#15803D", new PdfRect(100, 200, 160, 40), angle, "Genehmigt", "06.11.2026");

    [Fact]
    public void Corners_Unrotated_AreTheRectangle()
    {
        var stamp = Stamp();

        Assert.Equal([new PdfPoint(100, 200), new PdfPoint(260, 200), new PdfPoint(260, 240), new PdfPoint(100, 240)], stamp.Corners);
        Assert.Equal(stamp.Bounds, stamp.Envelope);
    }

    [Fact]
    public void Envelope_At90Degrees_SwapsWidthAndHeight()
    {
        var envelope = Stamp(90).Envelope;

        Assert.Equal(40, envelope.Width, 6);
        Assert.Equal(160, envelope.Height, 6);
        Assert.Equal(180, envelope.X + envelope.Width / 2, 6);
        Assert.Equal(220, envelope.Y + envelope.Height / 2, 6);
    }

    [Fact]
    public void Corners_Rotate_Clockwise()
    {
        // Oben links liegt nach 90° im Uhrzeigersinn oben rechts der Hülle
        var topLeft = Stamp(90).Corners[0];

        Assert.Equal(200, topLeft.X, 6);
        Assert.Equal(140, topLeft.Y, 6);
    }

    [Fact]
    public void Contains_FollowsRotation()
    {
        var stamp = Stamp(90);

        Assert.True(stamp.Contains(new PdfPoint(180, 150)));
        Assert.False(stamp.Contains(new PdfPoint(110, 220)));
        Assert.True(stamp.Contains(new PdfPoint(160, 150), margin: 1));
    }

    [Fact]
    public void ResizeFrom_KeepsOppositeCornerAndAspectRatio()
    {
        var stamp = Stamp(30);
        var anchor = stamp.Corners[0];
        var pointer = stamp.FromLocal(160, 60);

        var resized = stamp.ResizeFrom(2, pointer);

        Assert.Equal(anchor.X, resized.Corners[0].X, 6);
        Assert.Equal(anchor.Y, resized.Corners[0].Y, 6);
        Assert.Equal(stamp.Bounds.Height / stamp.Bounds.Width, resized.Bounds.Height / resized.Bounds.Width, 6);
        Assert.True(resized.Bounds.Width > stamp.Bounds.Width);
        Assert.Equal(30, resized.Angle);
    }

    [Fact]
    public void ResizeFrom_NeverBelowMinimumWidth()
    {
        var stamp = Stamp();

        var resized = stamp.ResizeFrom(2, stamp.Corners[0]);

        Assert.Equal(BoxAnnotation.MinWidth, resized.Bounds.Width, 6);
    }

    [Theory]
    [InlineData(180, 100, false, 0)]
    [InlineData(300, 220, false, 90)]
    [InlineData(180, 400, false, 180)]
    [InlineData(60, 220, false, 270)]
    [InlineData(250, 100, false, 30)]
    [InlineData(250, 100, true, 30.256)]
    public void AngleTowards_SnapsTo15Degrees_UnlessFree(double x, double y, bool free, double expected)
    {
        Assert.Equal(expected, Stamp().AngleTowards(new PdfPoint(x, y), free), 3);
    }

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(360, 0)]
    [InlineData(725, 5)]
    public void NormalizeAngle_WrapsIntoRange(double angle, double expected)
    {
        Assert.Equal(expected, BoxAnnotation.NormalizeAngle(angle), 6);
    }

    [Fact]
    public void PageRotation_MovesCenterAndAddsAngle()
    {
        var stamp = Stamp(15);

        var rotated = (StampAnnotation)PageTransform.Rotate(stamp, PageRotation.Clockwise90, 600, 800);

        Assert.Equal(PageTransform.Rotate(stamp.Center, PageRotation.Clockwise90, 600, 800), rotated.Center);
        Assert.Equal(105, rotated.Angle, 6);
        Assert.Equal(stamp.Bounds.Width, rotated.Bounds.Width);
    }

    [Fact]
    public void EraserHitsRotatedArea_AndBoundsAreTheEnvelope()
    {
        var stamp = Stamp(90);

        Assert.True(EraserHitTest.Hits(stamp, new PdfPoint(180, 138.5), 2));
        Assert.False(EraserHitTest.Hits(stamp, new PdfPoint(110, 220), 2));
        Assert.Equal(stamp.Envelope, AnnotationBounds.Of(stamp));
    }

    [Fact]
    public void Layout_FitsTextAndGrowsWithDetail()
    {
        var plain = StampLayout.For("GENEHMIGT", string.Empty);
        var withDetail = StampLayout.For("GENEHMIGT", "06.11.2026 · A. Muster");

        Assert.True(plain.Width >= Helvetica.Width("GENEHMIGT", StampLayout.LabelSize, bold: true));
        Assert.True(withDetail.Height > plain.Height);
        Assert.True(plain.LabelLeft > 0);
        Assert.Equal(plain.Width - 2 * plain.LabelLeft, Helvetica.Width("GENEHMIGT", StampLayout.LabelSize, bold: true), 6);
    }

    [Fact]
    public void Create_UsesLayoutAspectRatio_AndUppercaseLabel()
    {
        var stamp = StampAnnotation.Create("Geprüft", "06.11.2026", "#15803D", new PdfPoint(300, 400), 150, -30);

        Assert.Equal("GEPRÜFT", stamp.Label);
        Assert.Equal(stamp.Layout.Height / stamp.Layout.Width, stamp.Bounds.Height / stamp.Bounds.Width, 6);
        Assert.Equal(new PdfPoint(300, 400), stamp.Center);
        Assert.Equal(330, stamp.Angle);
    }

    [Theory]
    [InlineData("06.11.2026", "A. Muster", "06.11.2026 · A. Muster")]
    [InlineData("06.11.2026", null, "06.11.2026")]
    [InlineData(null, " A. Muster ", "A. Muster")]
    [InlineData(null, "", "")]
    public void DetailLine_SkipsMissingParts(string? date, string? name, string expected)
    {
        Assert.Equal(expected, StampAnnotation.DetailLine(date, name));
    }

    [Fact]
    public void Overview_ShowsStampTextAndImageKind()
    {
        var document = new EditableDocument(1);
        var image = ImageAnnotation.Create(new ImageData(2, 1, ImageEncoding.Rgb, new byte[6]), new PdfPoint(100, 100), 200);
        document.AddAnnotation(document.Pages[0].Id, Stamp());
        document.AddAnnotation(document.Pages[0].Id, image);

        var entries = AnnotationOverview.Build(document, (_, _) => string.Empty);

        Assert.Equal((AnnotationCategory.Shape, AnnotationLabel.Stamp, "GENEHMIGT"), (entries[0].Category, entries[0].Label, entries[0].Text));
        Assert.Equal((AnnotationCategory.Shape, AnnotationLabel.Image), (entries[1].Category, entries[1].Label));
        Assert.Equal(100, image.Bounds.Height, 6);
    }

    [Theory]
    [InlineData(1500, 800, 1500, 800)]
    [InlineData(4000, 3000, 2000, 1500)]
    [InlineData(1000, 6000, 333, 2000)]
    public void FitWithinLimit_ShrinksLongestEdgeTo2000(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.Equal((expectedWidth, expectedHeight), ImageData.FitWithinLimit(width, height));
    }

    [Fact]
    public void FromRgba_SplitsAlpha_AndDropsItWhenOpaque()
    {
        var opaque = ImageData.FromRgba(1, 2, [10, 20, 30, 255, 40, 50, 60, 255]);
        var transparent = ImageData.FromRgba(1, 2, [10, 20, 30, 255, 40, 50, 60, 0]);

        Assert.Equal([10, 20, 30, 40, 50, 60], opaque.Data);
        Assert.Null(opaque.Alpha);
        Assert.Equal([255, 0], transparent.Alpha!);
    }

    [Fact]
    public void JpegInfo_ReadsSizeAndComponents()
    {
        byte[] header =
        [
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,
            0xFF, 0xC0, 0x00, 0x11, 0x08, 0x00, 0x08, 0x00, 0x10, 0x03,
        ];

        Assert.Equal((16, 8, 3), JpegInfo.Read(header));
        Assert.Null(JpegInfo.Read([0x89, 0x50, 0x4E, 0x47]));
    }

    [Fact]
    public void Settings_NormalizeCustomStamps()
    {
        var settings = new AppSettings
        {
            CustomStamps = [new CustomStamp("  Bezahlt  ", "#15803D"), new CustomStamp(" ", "#15803D"), new CustomStamp(new string('x', 40), "kaputt")],
            StampColor = "falsch",
            UserName = "  A. Muster ",
        }.Normalized();

        Assert.Equal([new CustomStamp("Bezahlt", "#15803D"), new CustomStamp(new string('x', 30), StampAnnotation.Colors[2])], settings.CustomStamps);
        Assert.Equal(StampAnnotation.Presets[0].ColorHex, settings.StampColor);
        Assert.Equal("A. Muster", settings.UserName);
    }
}
