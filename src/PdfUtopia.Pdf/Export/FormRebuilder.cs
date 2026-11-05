using PdfSharp.Pdf;
using PdfUtopia.Core.Forms;
using PdfUtopia.Core.Text;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Baut beim Speichern und Extrahieren die Formular-Registrierung (<c>/AcroForm</c>) neu auf (P7, A50). Die Felder
/// kommen mit ihren Seiten in die neue Datei, das Verzeichnis der Felder im Katalog aber nicht; ohne es gelten sie
/// in anderen Programmen nicht mehr als Formular.
/// </summary>
internal static class FormRebuilder
{
    /// <summary>Schlüssel der Feldliste im Formular.</summary>
    private const string FieldsKey = "/Fields";

    /// <summary>
    /// Sucht alle Felder auf den gespeicherten Seiten, entfernt Widgets nicht gespeicherter Seiten aus ihren Feldern,
    /// benennt gleichnamige Felder verschiedener Dateien um und trägt das Formular in den Katalog ein.
    /// </summary>
    /// <param name="output">Die neue Datei.</param>
    /// <param name="pages">Gespeicherte Seiten mit der Quelldatei, aus der sie stammen.</param>
    /// <param name="inputs">Geöffnete Quelldateien nach Quellnummer.</param>
    /// <param name="form">Felder der geöffneten Datei mit ihren aktuellen Werten (7b) oder <c>null</c>.</param>
    public static void Rebuild(
        PdfDocument output, IReadOnlyList<(PdfPage Page, int SourceIndex)> pages, IReadOnlyDictionary<int, PdfDocument> inputs, FormValues? form = null)
    {
        var widgets = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        var fields = new List<(PdfDictionary Field, int SourceIndex)>();
        var seen = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        foreach (var (page, sourceIndex) in pages)
        {
            foreach (var widget in Widgets(page))
            {
                widgets.Add(widget);
                if (TopField(widget) is { } field && seen.Add(field))
                {
                    fields.Add((field, sourceIndex));
                }
            }
        }

        fields = fields.Where(entry => Prune(entry.Field, widgets) && entry.Field.Reference is not null).ToList();
        if (fields.Count == 0)
        {
            return;
        }

        var resources = DefaultResources(output);
        var needAppearances = form is not null && WriteValues(fields.Where(entry => entry.SourceIndex == 0).Select(entry => entry.Field), form, resources);
        RenameDuplicates(fields);
        var acroForm = new PdfDictionary(output);
        var array = new PdfArray(output);
        foreach (var (field, _) in fields)
        {
            array.Elements.Add(field.Reference!);
        }

        acroForm.Elements[FieldsKey] = array;
        CopySettings(Template(inputs, fields[0].SourceIndex), acroForm);
        if (needAppearances)
        {
            acroForm.Elements.SetBoolean("/NeedAppearances", true);
        }

        acroForm.Elements["/DR"] = resources;
        output.Internals.Catalog.Elements["/AcroForm"] = acroForm;
    }

    /// <summary>
    /// Schreibt geänderte Werte der geöffneten Datei in ihre Felder (7b): Text und Listen in /V mit neuem
    /// Erscheinungsbild, Kästchen und Optionsfelder über /V und den Zustand /AS der Widgets.
    /// </summary>
    /// <returns><c>true</c>, wenn ein Erscheinungsbild fehlt und Betrachter es neu erzeugen sollen (Auswahllisten).</returns>
    private static bool WriteValues(IEnumerable<PdfDictionary> topFields, FormValues form, PdfDictionary resources)
    {
        var changed = form.ChangedValues();
        if (changed.Count == 0)
        {
            return false;
        }

        var needAppearances = false;
        foreach (var (node, name, widgets) in topFields.SelectMany(field => Terminals(field, string.Empty, depth: 0)))
        {
            if (!changed.TryGetValue(name, out var value) || form.Field(name) is not { } field)
            {
                continue;
            }

            switch (field.Kind)
            {
                case FormFieldKind.CheckBox or FormFieldKind.Radio:
                    node.Elements.SetName("/V", "/" + value);
                    foreach (var widget in widgets)
                    {
                        var states = widget.Elements.GetDictionary("/AP")?.Elements.GetDictionary("/N");
                        widget.Elements.SetName("/AS", states?.Elements.ContainsKey("/" + value) == true ? "/" + value : "/" + FormField.Off);
                    }

                    break;
                case FormFieldKind.ListBox:
                    node.Elements["/V"] = new PdfString(value, PdfStringEncoding.Unicode);
                    needAppearances = true;
                    break;
                default:
                    node.Elements["/V"] = new PdfString(value, PdfStringEncoding.Unicode);
                    var label = field.Kind == FormFieldKind.ComboBox ? field.Options.FirstOrDefault(option => option.Value == value)?.Label ?? value : value;
                    foreach (var widget in widgets)
                    {
                        SetTextAppearance(widget, field, label, resources);
                    }

                    break;
            }
        }

        return needAppearances;
    }

    /// <summary>
    /// Endfelder eines Felds mit vollständigem Namen und ihren Widgets (wie beim Lesen, <see cref="Import.FormReader"/>).
    /// </summary>
    private static IEnumerable<(PdfDictionary Node, string Name, List<PdfDictionary> Widgets)> Terminals(PdfDictionary node, string parentName, int depth)
    {
        if (depth > 32)
        {
            yield break;
        }

        var partial = node.Elements.GetString("/T");
        var name = partial.Length == 0 ? parentName : parentName.Length == 0 ? partial : $"{parentName}.{partial}";
        var kids = node.Elements.GetArray("/Kids")?.Elements.Select(Resolve).OfType<PdfDictionary>().ToList() ?? [];
        var subfields = kids.Where(kid => kid.Elements.ContainsKey("/T")).ToList();
        if (subfields.Count == 0)
        {
            yield return (node, name, kids.Count > 0 ? kids : [node]);
            yield break;
        }

        foreach (var terminal in subfields.SelectMany(child => Terminals(child, name, depth + 1)))
        {
            yield return terminal;
        }
    }

    /// <summary>
    /// Erscheinungsbild eines Text- oder Klapplistenfelds in Helvetica: einzeilig senkrecht mittig, mehrzeilig von oben
    /// mit Umbruch, Ausrichtung wie im Feld.
    /// </summary>
    private static void SetTextAppearance(PdfDictionary widget, FormField field, string text, PdfDictionary resources)
    {
        if (widget.Elements.GetArray("/Rect") is not { Elements.Count: 4 } rect)
        {
            return;
        }

        var values = rect.Elements.Select(Number).ToArray();
        var (width, height) = (Math.Abs(values[2] - values[0]), Math.Abs(values[3] - values[1]));
        var size = field.FontSizeFor(height);
        var lines = field.Multiline ? TextWrapper.Wrap(text, width - 4, size) : [text.ReplaceLineEndings(" ")];
        var content = new ContentBuilder().Op("/Tx BMC").Save().Rectangle(1, 1, width - 2, height - 2).Op("W n").FillColor("#000000");
        for (var i = 0; i < lines.Count; i++)
        {
            var lineWidth = Helvetica.Width(lines[i], size);
            var x = field.Alignment switch { 1 => (width - lineWidth) / 2, 2 => width - 2 - lineWidth, _ => 2 };
            var y = field.Multiline ? height - 2 - size * (i + 1) * 1.15 + size * 0.2 : (height - size * 0.72) / 2;
            content.Text(x, y, lines[i], size);
        }

        content.Restore().Op("EMC");
        var form = new PdfDictionary(widget.Owner);
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements["/BBox"] = new PdfArray(widget.Owner, new PdfReal(0), new PdfReal(0), new PdfReal(width), new PdfReal(height));
        form.Elements["/Resources"] = resources;
        form.CreateStream(content.ToBytes());
        widget.Owner.Internals.AddObject(form);
        var appearance = new PdfDictionary(widget.Owner);
        appearance.Elements["/N"] = form.Reference;
        widget.Elements["/AP"] = appearance;
    }

    /// <summary>Widget-Annotationen einer Seite.</summary>
    private static IEnumerable<PdfDictionary> Widgets(PdfPage page) =>
        (page.Elements.GetArray("/Annots")?.Elements.ToList() ?? []).Select(Resolve).OfType<PdfDictionary>()
            .Where(annotation => annotation.Elements.GetName("/Subtype") == "/Widget");

    /// <summary>
    /// Oberstes Feld eines Widgets (über /Parent); <c>null</c>, wenn das Widget zu keinem Feld gehört.
    /// </summary>
    private static PdfDictionary? TopField(PdfDictionary widget)
    {
        var isField = widget.Elements.ContainsKey("/T") || widget.Elements.ContainsKey("/FT");
        var node = isField ? widget : widget.Elements.GetDictionary("/Parent");
        var depth = 0;
        while (node?.Elements.GetDictionary("/Parent") is { } parent && depth++ < 32)
        {
            node = parent;
        }

        return node;
    }

    /// <summary>
    /// Entfernt Unterpunkte, die auf keiner gespeicherten Seite liegen.
    /// </summary>
    /// <returns><c>true</c>, wenn das Feld (oder Widget) erhalten bleibt.</returns>
    private static bool Prune(PdfDictionary node, HashSet<PdfDictionary> widgets)
    {
        if (node.Elements.GetArray("/Kids") is not { } kids)
        {
            return widgets.Contains(node);
        }

        var kept = new PdfArray(node.Owner);
        foreach (var item in kids.Elements)
        {
            if (Resolve(item) is { } kid && Prune(kid, widgets))
            {
                kept.Elements.Add(item);
            }
        }

        node.Elements["/Kids"] = kept;
        return kept.Elements.Count > 0 || widgets.Contains(node);
    }

    /// <summary>
    /// Gleichnamige Felder aus verschiedenen Dateien teilen sich sonst einen Wert; spätere bekommen „_2“, „_3“ … angehängt.
    /// </summary>
    private static void RenameDuplicates(List<(PdfDictionary Field, int SourceIndex)> fields)
    {
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (field, sourceIndex) in fields)
        {
            var name = field.Elements.GetString("/T");
            if (name.Length == 0)
            {
                continue;
            }

            if (owners.TryGetValue(name, out var owner) && owner != sourceIndex)
            {
                var number = 2;
                while (owners.ContainsKey($"{name}_{number}"))
                {
                    number++;
                }

                name = $"{name}_{number}";
                field.Elements["/T"] = new PdfString(name);
            }

            owners.TryAdd(name, sourceIndex);
        }
    }

    /// <summary>Formular der Quelldatei des ersten Felds (für Standard-Darstellung und Ausrichtung).</summary>
    private static PdfDictionary? Template(IReadOnlyDictionary<int, PdfDocument> inputs, int sourceIndex) =>
        inputs.TryGetValue(sourceIndex, out var input) ? input.Internals.Catalog.Elements.GetDictionary("/AcroForm") : null;

    /// <summary>
    /// Übernimmt Standard-Darstellung (/DA), Ausrichtung (/Q) und „Erscheinungsbilder neu erzeugen“ (/NeedAppearances).
    /// </summary>
    private static void CopySettings(PdfDictionary? template, PdfDictionary acroForm)
    {
        acroForm.Elements.SetString("/DA", template?.Elements.GetString("/DA") is { Length: > 0 } da ? da : "/Helv 0 Tf 0 g");
        if (template?.Elements.ContainsKey("/Q") == true)
        {
            acroForm.Elements.SetInteger("/Q", template.Elements.GetInteger("/Q"));
        }

        if (template?.Elements.GetBoolean("/NeedAppearances") == true)
        {
            acroForm.Elements.SetBoolean("/NeedAppearances", true);
        }
    }

    /// <summary>
    /// Standard-Schriften für Programme, die Feldinhalte neu zeichnen: Helvetica und ZapfDingbats (Häkchen).
    /// </summary>
    private static PdfDictionary DefaultResources(PdfDocument output)
    {
        var fonts = new PdfDictionary(output);
        fonts.Elements["/Helv"] = Font(output, "/Helvetica", "/WinAnsiEncoding");
        fonts.Elements["/ZaDb"] = Font(output, "/ZapfDingbats", null);
        var resources = new PdfDictionary(output);
        resources.Elements["/Font"] = fonts;
        return resources;
    }

    /// <summary>Standardschrift (nicht eingebettet).</summary>
    private static PdfItem Font(PdfDocument output, string baseFont, string? encoding)
    {
        var font = new PdfDictionary(output);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", baseFont);
        if (encoding is not null)
        {
            font.Elements.SetName("/Encoding", encoding);
        }

        output.Internals.AddObject(font);
        return font.Reference!;
    }

    /// <summary>Zahl eines Eintrags (auch über Verweise).</summary>
    private static double Number(PdfItem item) => (item is PdfSharp.Pdf.Advanced.PdfReference reference ? reference.Value : item) switch
    {
        PdfReal real => real.Value,
        PdfInteger integer => integer.Value,
        _ => 0,
    };

    /// <summary>Dictionary hinter einem Eintrag (auch über Verweise).</summary>
    private static PdfDictionary? Resolve(PdfItem item) => item switch
    {
        PdfSharp.Pdf.Advanced.PdfReference reference => reference.Value as PdfDictionary,
        PdfDictionary dictionary => dictionary,
        _ => null,
    };
}
