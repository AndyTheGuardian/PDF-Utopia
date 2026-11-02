using PdfUtopia.Core.Annotations;

namespace PdfUtopia.Core.Drawing;

/// <summary>
/// Zeichnet einen Freihandstrich aus Eingabepunkten auf. Regeln aus dem Mockup-Test (A12, A13):
/// <list type="bullet">
/// <item>Ein Strich ist nicht an Zeiger-ID oder Gerätetyp gebunden – manche Tablets melden das Drücken als
/// Maus und die Bewegungen als Stift.</item>
/// <item>Gezeichnet wird nur bei Kontakt; schwebt der Stift (kein Kontakt), endet der Strich.</item>
/// <item>Punkte, die näher als <see cref="MinPointDistance"/> am vorigen liegen, werden verworfen (Zittern).</item>
/// <item>Fehlt der Andruck (0), gilt der Mittelwert 0,5; das wird über <see cref="PenWithoutPressure"/> gemeldet.</item>
/// </list>
/// </summary>
public sealed class StrokeRecorder
{
    /// <summary>Mindestabstand zweier aufgenommener Punkte in Punkten.</summary>
    public const double MinPointDistance = 1.5;

    /// <summary>Mindestausdehnung eines Strichs; kleinere gelten als versehentlicher Klick.</summary>
    public const double MinExtent = 4;

    /// <summary>Andruck, der bei fehlender Druckmeldung angenommen wird.</summary>
    public const float FallbackPressure = 0.5f;

    private readonly List<StrokePoint> _points = [];

    /// <summary>Ob gerade ein Strich aufgezeichnet wird.</summary>
    public bool IsRecording { get; private set; }

    /// <summary>Bisher aufgenommene Punkte des laufenden Strichs.</summary>
    public IReadOnlyList<StrokePoint> Points => _points;

    /// <summary>Ob ein Stift bei Kontakt keinen Andruck gemeldet hat (Hinweis an den Anwender).</summary>
    public bool PenWithoutPressure { get; private set; }

    /// <summary>
    /// Beginnt einen Strich beim Drücken.
    /// </summary>
    /// <param name="sample">Der Punkt beim Drücken.</param>
    /// <returns><c>true</c>, wenn ein Strich begonnen wurde.</returns>
    public bool Begin(InputSample sample)
    {
        // Das Radiererende zeichnet nie, es wird vom Radierer verarbeitet
        if (sample.IsExtraTouch || sample.IsEraser || !sample.IsInContact)
        {
            return false;
        }

        _points.Clear();
        PenWithoutPressure = false;
        IsRecording = true;
        Append(sample, force: true);
        return true;
    }

    /// <summary>
    /// Verarbeitet die Punkte einer Bewegung, inklusive der Zwischenpunkte des Geräts.
    /// </summary>
    /// <param name="samples">Die Punkte in zeitlicher Reihenfolge.</param>
    /// <returns><c>true</c>, solange der Strich läuft; <c>false</c>, wenn der Kontakt endete.</returns>
    public bool Move(IEnumerable<InputSample> samples)
    {
        if (!IsRecording)
        {
            return false;
        }

        foreach (var sample in samples.Where(sample => !sample.IsExtraTouch))
        {
            if (!sample.IsInContact)
            {
                IsRecording = false;
                return false;
            }

            Append(sample, force: false);
        }

        return true;
    }

    /// <summary>
    /// Beendet den Strich (Loslassen, Abbruch oder Verlust der Zeigerbindung).
    /// </summary>
    /// <returns>Die Punkte, oder <c>null</c>, wenn der Strich zu klein ist.</returns>
    public IReadOnlyList<StrokePoint>? End()
    {
        IsRecording = false;
        return IsLargeEnough(_points) ? _points.ToList() : null;
    }

    /// <summary>
    /// Prüft die Ausdehnung statt des Abstands von Start und Ende – sonst würden geschlossene Formen
    /// wie Kreise verworfen.
    /// </summary>
    /// <param name="points">Die Punkte.</param>
    /// <returns><c>true</c>, wenn sich mindestens ein Punkt um <see cref="MinExtent"/> vom Start entfernt.</returns>
    public static bool IsLargeEnough(IReadOnlyList<StrokePoint> points) =>
        points.Count >= 2 && points.Any(point => point.Position.DistanceTo(points[0].Position) >= MinExtent);

    /// <summary>
    /// Nimmt einen Punkt auf, sofern er weit genug vom vorigen entfernt ist.
    /// </summary>
    /// <param name="sample">Der Punkt.</param>
    /// <param name="force">Ohne Abstandsprüfung aufnehmen (Startpunkt).</param>
    private void Append(InputSample sample, bool force)
    {
        if (sample.Kind == PointerKind.Pen && sample.Pressure <= 0)
        {
            PenWithoutPressure = true;
        }

        if (!force && _points.Count > 0 && sample.Position.DistanceTo(_points[^1].Position) < MinPointDistance)
        {
            return;
        }

        _points.Add(new StrokePoint(sample.Position, sample.Pressure > 0 ? sample.Pressure : FallbackPressure));
    }
}
