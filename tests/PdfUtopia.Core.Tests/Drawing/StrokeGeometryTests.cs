using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Tests.Drawing;

/// <summary>
/// Tests der Glättung (Catmull-Rom) und der druckabhängigen Strichstärke (A10, A13).
/// </summary>
public sealed class StrokeGeometryTests
{
    private static StrokePoint P(double x, double y, float pressure = 0.5f) => new(new PdfPoint(x, y), pressure);

    private static readonly StrokePoint[] Zigzag = [P(0, 0), P(10, 10), P(20, 0), P(30, 10)];

    [Theory]
    [InlineData(0f, 3)]
    [InlineData(0.5f, 10)]
    [InlineData(1f, 17)]
    [InlineData(2f, 17)]
    [InlineData(-1f, 3)]
    public void PressureWidth_ScalesBaseWidth(float pressure, double expected) =>
        Assert.Equal(expected, StrokeGeometry.PressureWidth(10, pressure), precision: 6);

    [Fact]
    public void NoSmoothing_GivesStraightSegments()
    {
        var segments = StrokeGeometry.ToSegments(Zigzag, smoothing: 0, baseWidth: 2, pressureSensitive: false);

        Assert.Equal(3, segments.Count);
        Assert.All(segments, segment =>
        {
            Assert.Equal(segment.Start, segment.Control1);
            Assert.Equal(segment.End, segment.Control2);
        });
    }

    [Fact]
    public void Segments_AreConnected_AndPassThroughAllPoints()
    {
        var segments = StrokeGeometry.ToSegments(Zigzag, smoothing: 1, baseWidth: 2, pressureSensitive: false);

        Assert.Equal(Zigzag.Select(point => point.Position).SkipLast(1), segments.Select(segment => segment.Start));
        Assert.Equal(Zigzag.Select(point => point.Position).Skip(1), segments.Select(segment => segment.End));
    }

    [Fact]
    public void FullSmoothing_UsesCatmullRomControlPoints()
    {
        var segment = StrokeGeometry.ToSegments(Zigzag, smoothing: 1, baseWidth: 2, pressureSensitive: false)[1];

        // Stück von (10,10) nach (20,0): c1 = p1 + (p2 − p0)/6, c2 = p2 − (p3 − p1)/6
        AssertNear(new PdfPoint(10 + 20 / 6.0, 10), segment.Control1);
        AssertNear(new PdfPoint(20 - 20 / 6.0, 0), segment.Control2);
    }

    private static void AssertNear(PdfPoint expected, PdfPoint actual)
    {
        Assert.Equal(expected.X, actual.X, precision: 9);
        Assert.Equal(expected.Y, actual.Y, precision: 9);
    }

    [Fact]
    public void Smoothing_IsClampedToOne()
    {
        var clamped = StrokeGeometry.ToSegments(Zigzag, smoothing: 5, baseWidth: 2, pressureSensitive: false);
        var full = StrokeGeometry.ToSegments(Zigzag, smoothing: 1, baseWidth: 2, pressureSensitive: false);

        Assert.Equal(full, clamped);
    }

    [Fact]
    public void PressureSensitive_SegmentWidthFollowsEndPointPressure()
    {
        StrokePoint[] points = [P(0, 0, 0.1f), P(10, 0, 0.5f), P(20, 0, 1f)];

        var widths = StrokeGeometry.ToSegments(points, 0.5, baseWidth: 10, pressureSensitive: true).Select(segment => segment.Width);

        Assert.Equal([10, 17], widths.Select(width => Math.Round(width, 6)));
    }

    [Fact]
    public void NotPressureSensitive_AllSegmentsUseBaseWidth()
    {
        StrokePoint[] points = [P(0, 0, 0.1f), P(10, 0, 0.9f), P(20, 0, 0.3f)];

        Assert.All(StrokeGeometry.ToSegments(points, 0.5, 4, pressureSensitive: false), segment => Assert.Equal(4, segment.Width));
    }

    [Fact]
    public void SinglePoint_GivesNoSegments() =>
        Assert.Empty(StrokeGeometry.ToSegments([P(0, 0)], 0.5, 2, false));
}
