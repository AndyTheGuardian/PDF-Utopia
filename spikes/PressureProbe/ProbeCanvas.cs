using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace PressureProbe;

/// <summary>
/// Zeichenfläche, die Freihandstriche mit druckabhängiger Breite zeichnet und die vom Gerät gemeldeten
/// Eingabewerte (Gerätetyp, Andruck, Kontakt, Zwischenpunkte) anzeigt.
/// Wie im Mockup ist ein Strich nicht an Zeiger-ID oder Gerätetyp gebunden (A12).
/// </summary>
internal sealed class ProbeCanvas : Control
{
    private const double BaseWidth = 4;

    private readonly List<List<(Point Position, float Pressure)>> _strokes = [];
    private List<(Point Position, float Pressure)>? _current;
    private readonly HashSet<float> _distinctPressures = [];
    private int _events;
    private int _intermediatePoints;
    private float _maxPressure;

    /// <summary>Meldet die Werte des letzten Ereignisses als Text.</summary>
    public event EventHandler<string>? LiveInfoChanged;

    /// <summary>Meldet die Zusammenfassung seit dem letzten Leeren.</summary>
    public event EventHandler<string>? SummaryChanged;

    /// <summary>
    /// Löscht alle Striche und Messwerte.
    /// </summary>
    public void Clear()
    {
        _strokes.Clear();
        _current = null;
        _distinctPressures.Clear();
        _events = _intermediatePoints = 0;
        _maxPressure = 0;
        PublishSummary();
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        Report("Pressed", e, point, 1);
        _current = [(point.Position, point.Properties.Pressure)];
        _strokes.Add(_current);
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var points = e.GetIntermediatePoints(this);
        var last = points.Count > 0 ? points[^1] : e.GetCurrentPoint(this);
        Report("Moved", e, last, points.Count);
        if (_current is null)
        {
            return;
        }

        if (!IsInContact(last))
        {
            _current = null;
            return;
        }

        _intermediatePoints += points.Count;
        foreach (var p in points)
        {
            _current.Add((p.Position, p.Properties.Pressure));
        }

        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Report("Released", e, e.GetCurrentPoint(this), 1);
        _current = null;
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _current = null;
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        // Transparente Fläche macht das ganze Control für Zeigerereignisse treffbar
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        foreach (var stroke in _strokes)
        {
            for (var i = 1; i < stroke.Count; i++)
            {
                var width = BaseWidth * (0.3 + 1.4 * Effective(stroke[i].Pressure));
                var pen = new Pen(new SolidColorBrush(Color.Parse("#C93434")), width, lineCap: PenLineCap.Round);
                context.DrawLine(pen, stroke[i - 1].Position, stroke[i].Position);
            }
        }
    }

    /// <summary>
    /// Kontakt: Taste gedrückt oder Druck größer 0 (A12).
    /// </summary>
    /// <param name="point">Der Zeigerpunkt.</param>
    /// <returns><c>true</c> bei Kontakt.</returns>
    private static bool IsInContact(PointerPoint point) =>
        point.Properties.IsLeftButtonPressed || point.Properties.Pressure > 0;

    /// <summary>
    /// Ersetzt fehlenden Andruck (0) durch den Mittelwert, damit die Linie sichtbar bleibt.
    /// </summary>
    /// <param name="pressure">Gemeldeter Andruck.</param>
    /// <returns>Wirksamer Andruck.</returns>
    private static float Effective(float pressure) => pressure > 0 ? pressure : 0.5f;

    /// <summary>
    /// Zeigt die Werte eines Ereignisses an und sammelt Statistik.
    /// </summary>
    private void Report(string kind, PointerEventArgs e, PointerPoint point, int intermediateCount)
    {
        _events++;
        var pressure = point.Properties.Pressure;
        if (IsInContact(point))
        {
            _distinctPressures.Add(MathF.Round(pressure, 2));
            _maxPressure = MathF.Max(_maxPressure, pressure);
        }

        LiveInfoChanged?.Invoke(this,
            $"{kind,-8} Gerät={e.Pointer.Type,-5} ID={e.Pointer.Id,-3} Druck={pressure:0.000} Links={point.Properties.IsLeftButtonPressed,-5} " +
            $"Radierer={point.Properties.IsEraser,-5} Neigung X/Y={point.Properties.XTilt:0}/{point.Properties.YTilt:0} Zwischenpunkte={intermediateCount}");
        PublishSummary();
    }

    /// <summary>
    /// Meldet die Zusammenfassung: Höchstdruck und Anzahl unterschiedlicher Druckwerte bei Kontakt.
    /// </summary>
    private void PublishSummary()
    {
        var verdict = _distinctPressures.Count switch
        {
            0 => "noch keine Kontaktdaten",
            _ when _distinctPressures.All(p => p is 0f or 0.5f) => "KEIN echter Andruck (nur 0 bzw. 0,5)",
            _ => "Andruck wird gemeldet",
        };
        SummaryChanged?.Invoke(this,
            $"Ergebnis: {verdict} · Höchstdruck {_maxPressure:0.000} · {_distinctPressures.Count} verschiedene Druckwerte · " +
            $"{_events} Ereignisse · {_intermediatePoints} Zwischenpunkte");
    }
}
