namespace PdfUtopia.Core.History;

/// <summary>
/// Eine rückgängig machbare Änderung.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>Führt die Änderung aus (auch beim Wiederholen).</summary>
    void Execute();

    /// <summary>Nimmt die Änderung zurück.</summary>
    void Undo();
}

/// <summary>
/// Verlauf für Rückgängig/Wiederholen mit einstellbarer Höchstzahl an Schritten (1–500, Standard 50).
/// Wird die Höchstzahl überschritten, fällt der älteste Schritt weg. Ein neuer Schritt verwirft alle
/// wiederholbaren Schritte. Jeder Stand des Verlaufs hat eine Kennung (<see cref="State"/>), damit sich erkennen
/// lässt, ob Rückgängig/Wiederholen zu einem gespeicherten Stand zurückgeführt hat.
/// </summary>
public sealed class UndoHistory
{
    /// <summary>Kleinste einstellbare Schrittzahl.</summary>
    public const int MinLimit = 1;

    /// <summary>Größte einstellbare Schrittzahl.</summary>
    public const int MaxLimit = 500;

    /// <summary>Standard-Schrittzahl.</summary>
    public const int DefaultLimit = 50;

    private readonly LinkedList<Step> _undo = new();
    private readonly Stack<Step> _redo = new();
    private int _limit;
    private long _nextState = 1;
    private long _baseState;

    /// <summary>
    /// Erzeugt einen leeren Verlauf.
    /// </summary>
    /// <param name="limit">Höchstzahl an Schritten; wird auf 1–500 begrenzt.</param>
    public UndoHistory(int limit = DefaultLimit) => _limit = ClampLimit(limit);

    /// <summary>Wird nach jeder Änderung des Verlaufs ausgelöst.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Höchstzahl an Schritten (1–500). Beim Verkleinern fallen die ältesten Schritte weg.
    /// </summary>
    public int Limit
    {
        get => _limit;
        set
        {
            _limit = ClampLimit(value);
            if (Trim())
            {
                OnChanged();
            }
        }
    }

    /// <summary>
    /// Kennung des aktuellen Stands. Sie ist nach Rückgängig und Wiederholen dieselbe wie vorher bei diesem Stand;
    /// ein neuer Schritt ergibt immer eine neue Kennung.
    /// </summary>
    public long State => _undo.Last?.Value.State ?? _baseState;

    /// <summary>Anzahl rückgängig machbarer Schritte.</summary>
    public int UndoCount => _undo.Count;

    /// <summary>Anzahl wiederholbarer Schritte.</summary>
    public int RedoCount => _redo.Count;

    /// <summary>Ob ein Schritt rückgängig gemacht werden kann.</summary>
    public bool CanUndo => _undo.Count > 0;

    /// <summary>Ob ein Schritt wiederholt werden kann.</summary>
    public bool CanRedo => _redo.Count > 0;

    /// <summary>
    /// Begrenzt eine Schrittzahl auf den erlaubten Bereich.
    /// </summary>
    /// <param name="limit">Gewünschte Schrittzahl.</param>
    /// <returns>Schrittzahl zwischen 1 und 500.</returns>
    public static int ClampLimit(int limit) => Math.Clamp(limit, MinLimit, MaxLimit);

    /// <summary>
    /// Führt eine Änderung aus und nimmt sie in den Verlauf auf.
    /// </summary>
    /// <param name="command">Die Änderung.</param>
    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undo.AddLast(new Step(command, _nextState++));
        _redo.Clear();
        Trim();
        OnChanged();
    }

    /// <summary>
    /// Macht den letzten Schritt rückgängig.
    /// </summary>
    /// <returns><c>true</c>, wenn ein Schritt rückgängig gemacht wurde.</returns>
    public bool Undo()
    {
        if (_undo.Last is not { } last)
        {
            return false;
        }

        _undo.RemoveLast();
        last.Value.Command.Undo();
        _redo.Push(last.Value);
        OnChanged();
        return true;
    }

    /// <summary>
    /// Wiederholt den zuletzt rückgängig gemachten Schritt.
    /// </summary>
    /// <returns><c>true</c>, wenn ein Schritt wiederholt wurde.</returns>
    public bool Redo()
    {
        if (!_redo.TryPop(out var step))
        {
            return false;
        }

        step.Command.Execute();
        _undo.AddLast(step);
        OnChanged();
        return true;
    }

    /// <summary>
    /// Leert den Verlauf, z. B. nach dem Öffnen eines Dokuments.
    /// </summary>
    public void Clear()
    {
        // Der aktuelle Stand bleibt derselbe, er ist nur nicht mehr verlassbar
        _baseState = State;
        _undo.Clear();
        _redo.Clear();
        OnChanged();
    }

    /// <summary>
    /// Entfernt die ältesten Schritte über der Höchstzahl.
    /// </summary>
    /// <returns><c>true</c>, wenn Schritte entfernt wurden.</returns>
    private bool Trim()
    {
        var trimmed = false;
        while (_undo.Count > _limit && _undo.First is { } oldest)
        {
            // Der Stand nach dem ältesten Schritt wird zum frühesten erreichbaren Stand
            _baseState = oldest.Value.State;
            _undo.RemoveFirst();
            trimmed = true;
        }

        return trimmed;
    }

    /// <summary>
    /// Meldet eine Änderung.
    /// </summary>
    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Ein Schritt im Verlauf mit der Kennung des Stands nach diesem Schritt.
    /// </summary>
    private readonly record struct Step(IUndoableCommand Command, long State);
}
