using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Tests.Drawing;

/// <summary>
/// Tests der Strichaufzeichnung mit den Eingaberegeln aus dem Mockup-Test (A12): gemischte Maus-/Stift-Meldungen,
/// Zeichnen nur bei Kontakt, fehlender Andruck.
/// </summary>
public sealed class StrokeRecorderTests
{
    private static InputSample Mouse(double x, double y, bool pressed = true) =>
        new(new PdfPoint(x, y), pressed ? 0.5f : 0f, pressed, PointerKind.Mouse);

    private static InputSample Pen(double x, double y, float pressure, bool pressed = true) =>
        new(new PdfPoint(x, y), pressure, pressed, PointerKind.Pen);

    [Fact]
    public void MouseDown_PenMoves_AreRecordedAsOneStroke()
    {
        var recorder = new StrokeRecorder();

        recorder.Begin(Mouse(0, 0));
        recorder.Move([Pen(10, 0, 0.6f), Pen(20, 5, 0.7f)]);
        var points = recorder.End();

        Assert.NotNull(points);
        Assert.Equal(3, points.Count);
    }

    [Fact]
    public void PenContactWithoutButton_CountsAsContact()
    {
        var recorder = new StrokeRecorder();
        recorder.Begin(Pen(0, 0, 0.4f, pressed: false));

        var running = recorder.Move([Pen(10, 0, 0.4f, pressed: false)]);

        Assert.True(running);
        Assert.Equal(2, recorder.Points.Count);
    }

    [Fact]
    public void HoverWithoutContact_EndsStroke_AndIgnoresFurtherMoves()
    {
        var recorder = new StrokeRecorder();
        recorder.Begin(Pen(0, 0, 0.5f));
        recorder.Move([Pen(10, 0, 0.5f)]);

        var running = recorder.Move([Pen(20, 0, 0f, pressed: false), Pen(30, 0, 0.5f)]);

        Assert.False(running);
        Assert.False(recorder.IsRecording);
        Assert.False(recorder.Move([Pen(40, 0, 0.5f)]));
        Assert.Equal(2, recorder.Points.Count);
    }

    [Fact]
    public void Begin_WithoutContact_DoesNotStart()
    {
        var recorder = new StrokeRecorder();

        Assert.False(recorder.Begin(Mouse(0, 0, pressed: false)));
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void EraserEnd_NeverStartsStroke()
    {
        var recorder = new StrokeRecorder();
        var eraser = new InputSample(new PdfPoint(0, 0), 0.6f, true, PointerKind.Pen, IsEraser: true);

        Assert.False(recorder.Begin(eraser));
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void ExtraTouchFinger_IsIgnored()
    {
        var recorder = new StrokeRecorder();
        var secondFinger = new InputSample(new PdfPoint(0, 0), 0.5f, true, PointerKind.Touch, IsPrimary: false);

        Assert.False(recorder.Begin(secondFinger));
    }

    [Fact]
    public void PointsCloserThanMinimumDistance_AreDropped()
    {
        var recorder = new StrokeRecorder();
        recorder.Begin(Mouse(0, 0));

        recorder.Move([Mouse(0.5, 0), Mouse(1, 0), Mouse(2, 0)]);

        Assert.Equal([0d, 2d], recorder.Points.Select(point => point.Position.X));
    }

    [Fact]
    public void PenWithoutPressure_UsesFallbackAndIsReported()
    {
        var recorder = new StrokeRecorder();
        recorder.Begin(Mouse(0, 0));

        recorder.Move([Pen(10, 0, 0f)]);

        Assert.True(recorder.PenWithoutPressure);
        Assert.All(recorder.Points, point => Assert.Equal(StrokeRecorder.FallbackPressure, point.Pressure));
    }

    [Fact]
    public void PenWithPressure_KeepsPressureAndIsNotReported()
    {
        var recorder = new StrokeRecorder();
        recorder.Begin(Pen(0, 0, 0.2f));

        recorder.Move([Pen(10, 0, 0.9f)]);

        Assert.False(recorder.PenWithoutPressure);
        Assert.Equal([0.2f, 0.9f], recorder.Points.Select(point => point.Pressure));
    }

    [Fact]
    public void End_TinyStroke_ReturnsNull()
    {
        var recorder = new StrokeRecorder();
        recorder.Begin(Mouse(0, 0));
        recorder.Move([Mouse(2, 0), Mouse(3.5, 0)]);

        Assert.Null(recorder.End());
    }

    [Fact]
    public void End_ClosedCircle_IsKept()
    {
        var recorder = new StrokeRecorder();
        recorder.Begin(Mouse(50, 0));
        var circle = Enumerable.Range(1, 12)
            .Select(i => Mouse(50 * Math.Cos(i / 12.0 * 2 * Math.PI), 50 * Math.Sin(i / 12.0 * 2 * Math.PI)));
        recorder.Move(circle);

        Assert.NotNull(recorder.End());
    }
}
