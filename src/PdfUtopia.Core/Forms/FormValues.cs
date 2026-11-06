namespace PdfUtopia.Core.Forms;

/// <summary>
/// Felder der geöffneten Datei mit ihren aktuellen Werten (A50, 7b). Änderungen laufen über
/// <see cref="EditableDocument.SetFieldValue"/>, damit jede rückgängig gemacht werden kann.
/// </summary>
public sealed class FormValues
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FormField> _fields = new(StringComparer.Ordinal);

    /// <summary>Wird nach jeder Änderung eines Werts mit dem Feldnamen ausgelöst.</summary>
    public event EventHandler<string>? Changed;

    /// <summary>Alle Felder in der Reihenfolge der Datei.</summary>
    public IReadOnlyList<FormField> Fields { get; private set; } = [];

    /// <summary>Ob die Datei ausfüllbare Felder hat.</summary>
    public bool HasFields => Fields.Count > 0;

    /// <summary>
    /// Übernimmt die Felder beim Öffnen.
    /// </summary>
    /// <param name="fields">Die Felder.</param>
    public void Load(IReadOnlyList<FormField> fields)
    {
        Fields = fields;
        _fields.Clear();
        _values.Clear();
        foreach (var field in fields)
        {
            _fields.TryAdd(field.Name, field);
            _values.TryAdd(field.Name, field.InitialValue);
        }
    }

    /// <summary>
    /// Feld nach Namen.
    /// </summary>
    /// <param name="name">Der Name.</param>
    /// <returns>Das Feld oder <c>null</c>.</returns>
    public FormField? Field(string name) => _fields.GetValueOrDefault(name);

    /// <summary>
    /// Aktueller Wert eines Felds.
    /// </summary>
    /// <param name="name">Der Name.</param>
    /// <returns>Der Wert (leer bei unbekannten Feldern).</returns>
    public string Value(string name) => _values.GetValueOrDefault(name, string.Empty);

    /// <summary>
    /// Felder, deren Wert sich vom Wert in der Datei unterscheidet, mit ihrem neuen Wert.
    /// </summary>
    /// <returns>Name → Wert.</returns>
    public IReadOnlyDictionary<string, string> ChangedValues() =>
        _fields.Values.Where(field => _values[field.Name] != field.InitialValue).ToDictionary(field => field.Name, field => _values[field.Name]);

    /// <summary>
    /// Felder mit sichtbaren Teilen auf einer Seite der geöffneten Datei, in Lesereihenfolge (oben nach unten,
    /// links nach rechts) – auch für die Tab-Reihenfolge.
    /// </summary>
    /// <param name="sourcePageNumber">Seite der geöffneten Datei.</param>
    /// <returns>Feld und Widget je sichtbarem Teil.</returns>
    public IReadOnlyList<(FormField Field, FormWidget Widget)> OnPage(int sourcePageNumber)
    {
        var items = Fields.SelectMany(field => field.Widgets.Where(widget => widget.SourcePageNumber == sourcePageNumber).Select(widget => (field, widget)))
            .OrderBy(item => item.widget.Bounds.Y).ToList();
        // Zeilen bilden: Felder, deren Oberkante nah an der ersten der Zeile liegt, stehen nebeneinander
        var rows = new List<List<(FormField Field, FormWidget Widget)>>();
        foreach (var item in items)
        {
            if (rows.Count > 0 && item.widget.Bounds.Y - rows[^1][0].Widget.Bounds.Y < RowTolerance)
            {
                rows[^1].Add(item);
            }
            else
            {
                rows.Add([item]);
            }
        }

        return rows.SelectMany(row => row.OrderBy(item => item.Widget.Bounds.X)).ToList();
    }

    /// <summary>Höchster Abstand der Oberkanten, bei dem Felder als eine Zeile gelten.</summary>
    private const double RowTolerance = 8;

    /// <summary>
    /// Setzt einen Wert (nur über <see cref="EditableDocument"/>).
    /// </summary>
    internal void Set(string name, string value)
    {
        _values[name] = value;
        Changed?.Invoke(this, name);
    }
}
