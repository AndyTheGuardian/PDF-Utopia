using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Signatures;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Unterschriftenfeld (A49, Mockup v5): mit Stift oder Maus schreiben, mit Andruck. Weiß wie Papier, mit Grundlinie
/// und Kreuz wie auf einem Formular. Die Fläche entspricht <see cref="Signature.PadWidth"/> × <see cref="Signature.PadHeight"/>.
/// </summary>
public sealed class SignaturePad : Control
{
    private const double MinPointDistance = 1.5;
    private const double BaselineFromBottom = 46;
    private const double GuideInset = 24;

    private readonly List<List<StrokePoint>> _strokes = [];
    private List<StrokePoint>? _current;
    private string _colorHex = Signature.Colors[0];
    private bool _pressureSensitive = true;

    /// <summary>
    /// Erzeugt das Feld in fester Größe.
    /// </summary>
    public SignaturePad()
    {
        Width = Signature.PadWidth;
        Height = Signature.PadHeight;
        Cursor = new Cursor(StandardCursorType.Cross);
        ClipToBounds = true;
    }

    /// <summary>Wird nach jeder Änderung der Striche ausgelöst.</summary>
    public event EventHandler? Changed;

    /// <summary>Die geschriebenen Striche.</summary>
    public IReadOnlyList<IReadOnlyList<StrokePoint>> Strokes => _strokes.Where(stroke => stroke.Count > 1).Select(stroke => (IReadOnlyList<StrokePoint>)stroke.ToList()).ToList();

    /// <summary>Ob etwas geschrieben wurde.</summary>
    public bool HasStrokes => _strokes.Any(stroke => stroke.Count > 1);

    /// <summary>Strichfarbe.</summary>
    public string ColorHex
    {
        get => _colorHex;
        set
        {
            _colorHex = value;
            InvalidateVisual();
        }
    }

    /// <summary>Ob die Strichstärke dem Andruck folgt.</summary>
    public bool PressureSensitive
    {
        get => _pressureSensitive;
        set
        {
            _pressureSensitive = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Übernimmt Striche einer gespeicherten Unterschrift (oder leert das Feld).
    /// </summary>
    /// <param name="strokes">Die Striche.</param>
    public void Load(IEnumerable<IReadOnlyList<StrokePoint>> strokes)
    {
        _strokes.Clear();
        _strokes.AddRange(strokes.Select(stroke => stroke.ToList()));
        _current = null;
        OnChanged();
    }

    /// <summary>Leert das Feld.</summary>
    public void Clear() => Load([]);

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size), 8);
        var guide = new Pen(new SolidColorBrush(Color.Parse("#C9CCD6")), 1);
        var baseline = Signature.PadHeight - BaselineFromBottom;
        context.DrawLine(guide, new Point(GuideInset, baseline), new Point(Signature.PadWidth - GuideInset, baseline));
        var hintBrush = new SolidColorBrush(Color.Parse("#9AA0B0"));
        context.DrawText(Text("×", 18, hintBrush), new Point(GuideInset + 2, baseline - 26));
        if (!HasStrokes && _current is null)
        {
            var placeholder = Text(Loc.T("Signature.SignHere"), 13, hintBrush);
            context.DrawText(placeholder, new Point((Signature.PadWidth - placeholder.Width) / 2, (Signature.PadHeight - placeholder.Height) / 2 - 10));
        }

        StrokeRenderer.Draw(context, _strokes, _colorHex, Signature.PadStrokeWidth, _pressureSensitive, Signature.Smoothing);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsEraser || (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed))
        {
            return;
        }

        e.Pointer.Capture(this);
        e.PreventGestureRecognition();
        _current = [Sample(point)];
        _strokes.Add(_current);
        e.Handled = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_current is null)
        {
            return;
        }

        foreach (var point in e.GetIntermediatePoints(this))
        {
            // Stift abgehoben, ohne dass das Loslassen gemeldet wurde (A12)
            if (!point.Properties.IsLeftButtonPressed && point.Properties.Pressure <= 0)
            {
                EndStroke();
                return;
            }

            var sample = Sample(point);
            if (sample.Position.DistanceTo(_current[^1].Position) >= MinPointDistance)
            {
                _current.Add(sample);
            }
        }

        e.Handled = true;
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndStroke();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndStroke();
    }

    /// <summary>
    /// Schließt den laufenden Strich ab; ein einzelner Punkt wird verworfen.
    /// </summary>
    private void EndStroke()
    {
        if (_current is null)
        {
            return;
        }

        if (_current.Count < 2)
        {
            _strokes.Remove(_current);
        }

        _current = null;
        OnChanged();
    }

    /// <summary>Messpunkt mit Andruck (Maus: mittlerer Andruck).</summary>
    private static StrokePoint Sample(PointerPoint point) => new(
        new PdfPoint(point.Position.X, point.Position.Y),
        point.Properties.Pressure > 0 ? point.Properties.Pressure : 0.5f);

    /// <summary>Text für Hinweise im Feld.</summary>
    private static FormattedText Text(string text, double size, IBrush brush) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, size, brush);

    /// <summary>Meldet eine Änderung und zeichnet neu.</summary>
    private void OnChanged()
    {
        InvalidateVisual();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
