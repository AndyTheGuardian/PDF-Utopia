using System.Globalization;
using System.Text.RegularExpressions;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfUtopia.Core.Forms;

namespace PdfUtopia.Pdf.Import;

/// <summary>
/// Liest die ausfüllbaren Felder eines Formulars (A50, 7b): Art, Name, Wert, Schalter, Einträge und die Lage der
/// sichtbaren Teile (noch in PDF-Koordinaten; die Umrechnung übernimmt <see cref="Rendering.PdfSource"/>).
/// Schaltflächen ohne Wert und Signaturfelder werden übergangen.
/// </summary>
internal static partial class FormReader
{
    private const int ReadOnlyFlag = 1 << 0;
    private const int RequiredFlag = 1 << 1;
    private const int MultilineFlag = 1 << 12;
    private const int RadioFlag = 1 << 15;
    private const int PushButtonFlag = 1 << 16;
    private const int ComboFlag = 1 << 17;
    private const int MaxDepth = 32;

    /// <summary>
    /// Ein gelesenes Feld mit Widgets in PDF-Koordinaten.
    /// </summary>
    /// <param name="Field">Das Feld (Widgets noch ohne Lage).</param>
    /// <param name="Widgets">Seite (1-basiert), Rechteck links/unten/rechts/oben und „an“-Zustand je Widget.</param>
    public sealed record RawField(FormField Field, IReadOnlyList<(int Page, double Left, double Bottom, double Right, double Top, string? OnState)> Widgets);

    /// <summary>
    /// Liest die Felder einer geöffneten Datei.
    /// </summary>
    /// <param name="document">Die Datei (zum Lesen geöffnet).</param>
    /// <returns>Die Felder in der Reihenfolge des Formulars.</returns>
    public static IReadOnlyList<RawField> Read(PdfDocument document)
    {
        var fields = document.Internals.Catalog.Elements.GetDictionary("/AcroForm")?.Elements.GetArray("/Fields");
        if (fields is null)
        {
            return [];
        }

        var pages = new Dictionary<PdfDictionary, int>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < document.Pages.Count; i++)
        {
            foreach (var annotation in (document.Pages[i].Elements.GetArray("/Annots")?.Elements.ToList() ?? []).Select(Resolve).OfType<PdfDictionary>())
            {
                pages.TryAdd(annotation, i + 1);
            }
        }

        var result = new List<RawField>();
        var defaultAppearance = document.Internals.Catalog.Elements.GetDictionary("/AcroForm")?.Elements.GetString("/DA") ?? string.Empty;
        foreach (var item in fields.Elements)
        {
            if (Resolve(item) is PdfDictionary node)
            {
                Collect(node, new Inherited(string.Empty, null, 0, null, defaultAppearance, 0, null), pages, result, depth: 0);
            }
        }

        return result;
    }

    /// <summary>Vom Elternfeld geerbte Einträge.</summary>
    private sealed record Inherited(string Name, string? Type, int Flags, PdfItem? Value, string Appearance, int Alignment, PdfArray? Options);

    /// <summary>
    /// Geht ein Feld durch: Unterfelder (Kinder mit Namen) rekursiv, sonst ist es ein Endfeld mit seinen Widgets.
    /// </summary>
    private static void Collect(PdfDictionary node, Inherited parent, Dictionary<PdfDictionary, int> pages, List<RawField> result, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        var partial = node.Elements.GetString("/T");
        var inherited = new Inherited(
            partial.Length == 0 ? parent.Name : parent.Name.Length == 0 ? partial : $"{parent.Name}.{partial}",
            node.Elements.GetName("/FT") is { Length: > 0 } type ? type : parent.Type,
            node.Elements.ContainsKey("/Ff") ? node.Elements.GetInteger("/Ff") : parent.Flags,
            node.Elements["/V"] ?? parent.Value,
            node.Elements.GetString("/DA") is { Length: > 0 } da ? da : parent.Appearance,
            node.Elements.ContainsKey("/Q") ? node.Elements.GetInteger("/Q") : parent.Alignment,
            node.Elements.GetArray("/Opt") ?? parent.Options);

        var kids = node.Elements.GetArray("/Kids")?.Elements.Select(Resolve).OfType<PdfDictionary>().ToList() ?? [];
        var subfields = kids.Where(kid => kid.Elements.ContainsKey("/T")).ToList();
        if (subfields.Count > 0)
        {
            foreach (var child in subfields)
            {
                Collect(child, inherited, pages, result, depth + 1);
            }

            return;
        }

        var widgets = kids.Count > 0 ? kids : [node];
        if (ToField(inherited, widgets, pages) is { } field)
        {
            result.Add(field);
        }
    }

    /// <summary>
    /// Baut ein Endfeld; <c>null</c> für nicht unterstützte Arten (Schaltflächen, Signaturen) und Felder ohne Namen.
    /// </summary>
    private static RawField? ToField(Inherited data, List<PdfDictionary> widgets, Dictionary<PdfDictionary, int> pages)
    {
        FormFieldKind? kind = data.Type switch
        {
            "/Tx" => FormFieldKind.Text,
            "/Btn" when (data.Flags & PushButtonFlag) != 0 => null,
            "/Btn" when (data.Flags & RadioFlag) != 0 => FormFieldKind.Radio,
            "/Btn" => FormFieldKind.CheckBox,
            "/Ch" when (data.Flags & ComboFlag) != 0 => FormFieldKind.ComboBox,
            "/Ch" => FormFieldKind.ListBox,
            _ => null,
        };
        if (kind is not { } fieldKind || data.Name.Length == 0)
        {
            return null;
        }

        var placed = new List<(int, double, double, double, double, string?)>();
        foreach (var widget in widgets)
        {
            if (!pages.TryGetValue(widget, out var page) || widget.Elements.GetArray("/Rect") is not { Elements.Count: 4 } rect)
            {
                continue;
            }

            var values = rect.Elements.Select(Number).ToArray();
            placed.Add((page, Math.Min(values[0], values[2]), Math.Min(values[1], values[3]), Math.Max(values[0], values[2]), Math.Max(values[1], values[3]),
                fieldKind is FormFieldKind.CheckBox or FormFieldKind.Radio ? OnState(widget) : null));
        }

        var field = new FormField(
            data.Name, fieldKind, ValueOf(data.Value, fieldKind), (data.Flags & ReadOnlyFlag) != 0, (data.Flags & RequiredFlag) != 0,
            (data.Flags & MultilineFlag) != 0, MaxLength(widgets), FontSize(data.Appearance), Math.Clamp(data.Alignment, 0, 2),
            Options(data.Options), []);
        return new RawField(field, placed);
    }

    /// <summary>Wert als Text; bei Kästchen und Optionsfeldern der Zustandsname ohne Schrägstrich.</summary>
    private static string ValueOf(PdfItem? value, FormFieldKind kind) => Resolve(value) switch
    {
        PdfName name => name.Value.TrimStart('/'),
        PdfString text => text.Value,
        PdfArray array when array.Elements.Count > 0 => ValueOf(array.Elements[0], kind),
        _ => kind is FormFieldKind.CheckBox or FormFieldKind.Radio ? FormField.Off : string.Empty,
    };

    /// <summary>Name des „an“-Zustands eines Widgets (der Eintrag in /AP /N, der nicht „Off“ ist).</summary>
    private static string? OnState(PdfDictionary widget)
    {
        var normal = widget.Elements.GetDictionary("/AP")?.Elements.GetDictionary("/N");
        return normal?.Elements.Keys.Select(key => key.TrimStart('/')).FirstOrDefault(key => key != FormField.Off);
    }

    /// <summary>Höchstlänge (steht meist am Feld, manchmal am Widget).</summary>
    private static int MaxLength(List<PdfDictionary> widgets) =>
        widgets.Select(widget => widget.Elements.ContainsKey("/MaxLen") ? widget.Elements.GetInteger("/MaxLen") : 0).DefaultIfEmpty(0).Max();

    /// <summary>Einträge einer Liste: Text oder Paar aus Wert und Anzeigetext.</summary>
    private static IReadOnlyList<FormOption> Options(PdfArray? options) =>
        options?.Elements.Select(Resolve).Select(item => item switch
        {
            PdfString text => new FormOption(text.Value, text.Value),
            PdfArray { Elements.Count: >= 2 } pair when Resolve(pair.Elements[0]) is PdfString value && Resolve(pair.Elements[1]) is PdfString label
                => new FormOption(value.Value, label.Value),
            _ => null,
        }).OfType<FormOption>().ToList() ?? [];

    /// <summary>Schriftgröße aus der Standard-Darstellung, z. B. „/Helv 12 Tf 0 g“.</summary>
    private static double FontSize(string appearance) =>
        FontSizePattern().Match(appearance) is { Success: true } match && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size)
            ? size
            : 0;

    [GeneratedRegex(@"([\d.]+)\s+Tf")]
    private static partial Regex FontSizePattern();

    /// <summary>Zahl eines Eintrags.</summary>
    private static double Number(PdfItem item) => Resolve(item) switch
    {
        PdfReal real => real.Value,
        PdfInteger integer => integer.Value,
        _ => 0,
    };

    /// <summary>Eintrag hinter einem Verweis.</summary>
    private static PdfItem? Resolve(PdfItem? item) => item is PdfReference reference ? reference.Value : item;
}
