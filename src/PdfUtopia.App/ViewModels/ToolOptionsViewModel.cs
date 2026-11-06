using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Images;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Gewähltes Werkzeug und seine Einstellungen (Farbe, Strichstärke, Glättung, Druckempfindlichkeit,
/// Radiergröße). Jede Änderung wird gemeldet, damit sie gespeichert wird.
/// </summary>
public sealed partial class ToolOptionsViewModel : ObservableObject
{
    /// <summary>
    /// Übernimmt die gespeicherten Werte.
    /// </summary>
    /// <param name="settings">Die Einstellungen.</param>
    public ToolOptionsViewModel(AppSettings settings)
    {
        FreehandColor = settings.FreehandColor;
        RectangleColor = settings.RectangleColor;
        CommentColor = settings.CommentColor;
        TextColor = settings.TextColor;
        Shape = settings.Shape;
        ArrowEnds = settings.ArrowEnds;
        SelectedSignatureIndex = settings.SelectedSignature;
        TextSize = settings.TextSize;
        MarkerColor = settings.MarkerColor;
        FreehandWidth = settings.FreehandWidth;
        RectangleWidth = settings.RectangleWidth;
        MarkerHeight = settings.MarkerHeight;
        Smoothing = settings.Smoothing;
        PressureSensitive = settings.PressureSensitive;
        EraserRadius = settings.EraserRadius;
        StampColor = settings.StampColor;
        StampDate = settings.StampDate;
        StampName = settings.StampName;
        UserName = settings.UserName;
        SelectedStampIndex = settings.SelectedStamp;
        SetCustomStamps(settings.CustomStamps);
    }

    /// <summary>Wird bei jeder dauerhaft zu speichernden Änderung ausgelöst.</summary>
    public event EventHandler? Changed;

    /// <summary>Feste Textmarker-Farben.</summary>
    public IReadOnlyList<string> MarkerColors => DrawingColors.MarkerColors;

    /// <summary>Gewähltes Werkzeug.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Hint), nameof(ShowColorPicker), nameof(ShowMarkerColors), nameof(ShowShapeKinds), nameof(ShowArrowEnds), nameof(ShowSignatureOptions), nameof(ShowStampOptions), nameof(ShowWidth),
        nameof(ShowFreehandOptions), nameof(ShowEraserOptions), nameof(CurrentColor), nameof(CurrentWidth), nameof(WidthMinimum),
        nameof(WidthMaximum), nameof(WidthLabel), nameof(WidthText), nameof(ShowPressureHint), nameof(ColorTarget))]
    public partial Tool Tool { get; set; } = Tool.Select;

    /// <summary>Farbe für Freihand.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentColor))]
    public partial string FreehandColor { get; set; }

    /// <summary>Farbe für Rechtecke.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentColor))]
    public partial string RectangleColor { get; set; }

    /// <summary>Farbe für Kommentare.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentColor))]
    public partial string CommentColor { get; set; }

    /// <summary>Farbe des Markers.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentColor))]
    public partial string MarkerColor { get; set; }

    /// <summary>Gewählte Form des Werkzeugs „Formen“ (A44).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowArrowEnds))]
    public partial ShapeKind Shape { get; set; }

    /// <summary>Gespeicherte Unterschriften zur Auswahl in der Optionsleiste (A49).</summary>
    public System.Collections.ObjectModel.ObservableCollection<SignatureChoice> Signatures { get; } = [];

    /// <summary>Position der gewählten Unterschrift.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSignature))]
    public partial int SelectedSignatureIndex { get; set; }

    /// <summary>Die gewählte Unterschrift oder <c>null</c>, wenn keine gespeichert ist.</summary>
    public Core.Signatures.Signature? SelectedSignature =>
        Signatures.Count == 0 ? null : Signatures[Math.Clamp(SelectedSignatureIndex, 0, Signatures.Count - 1)].Signature;

    /// <summary>Ob die Optionsleiste die Unterschriften zeigt.</summary>
    public bool ShowSignatureOptions => Tool == Tool.Signature;

    /// <summary>Ob noch Platz für eine weitere Unterschrift ist.</summary>
    public bool CanAddSignature => Signatures.Count < Core.Signatures.Signature.MaxCount;

    /// <summary>Ob noch keine Unterschrift gespeichert ist.</summary>
    public bool HasNoSignature => Signatures.Count == 0;

    /// <summary>Wird ausgelöst, wenn das Werkzeug eine Unterschrift braucht, aber keine gespeichert ist.</summary>
    public event EventHandler? SignatureDialogRequested;

    /// <summary>
    /// Übernimmt die gespeicherten Unterschriften; die Auswahl bleibt, soweit möglich.
    /// </summary>
    /// <param name="signatures">Die Unterschriften.</param>
    public void SetSignatures(IReadOnlyList<Core.Signatures.Signature> signatures)
    {
        Signatures.Clear();
        for (var i = 0; i < signatures.Count; i++)
        {
            Signatures.Add(new SignatureChoice(i, signatures[i]));
        }

        SelectSignature(Math.Clamp(SelectedSignatureIndex, 0, Math.Max(0, signatures.Count - 1)));
        OnPropertyChanged(nameof(CanAddSignature));
        OnPropertyChanged(nameof(HasNoSignature));
        OnPropertyChanged(nameof(SelectedSignature));
    }

    /// <summary>
    /// Wählt eine Unterschrift.
    /// </summary>
    /// <param name="index">Ihre Position.</param>
    public void SelectSignature(int index)
    {
        SelectedSignatureIndex = index;
        foreach (var choice in Signatures)
        {
            choice.IsSelected = choice.Index == index;
        }
    }

    /// <summary>Bittet um den Unterschriften-Dialog (z. B. Klick auf die Seite ohne gespeicherte Unterschrift).</summary>
    public void RequestSignatureDialog() => SignatureDialogRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Stempel zur Auswahl: die Vorlagen, dahinter die eigenen (A51).</summary>
    public System.Collections.ObjectModel.ObservableCollection<StampChoice> Stamps { get; } = [];

    /// <summary>Eigene Stempel.</summary>
    public IReadOnlyList<Core.Settings.CustomStamp> CustomStamps { get; private set; } = [];

    /// <summary>Vorlagen in der Auswahl.</summary>
    public IReadOnlyList<StampChoice> PresetChoices => Stamps.Where(choice => !choice.IsCustom).ToList();

    /// <summary>Eigene Stempel in der Auswahl.</summary>
    public IReadOnlyList<StampChoice> CustomChoices => Stamps.Where(choice => choice.IsCustom).ToList();

    /// <summary>Ob es eigene Stempel gibt.</summary>
    public bool HasCustomStamps => CustomStamps.Count > 0;

    /// <summary>Stempelfarben zur Auswahl; die gewählte ist markiert.</summary>
    public IReadOnlyList<StampColorChoice> StampColorChoices { get; } = StampAnnotation.Colors.Select(hex => new StampColorChoice(hex)).ToList();

    /// <summary>Position des gewählten Stempels.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StampPreview))]
    public partial int SelectedStampIndex { get; set; }

    /// <summary>Farbe des nächsten Stempels.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StampPreview))]
    public partial string StampColor { get; set; }

    /// <summary>Ob Stempel das Datum tragen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StampPreview))]
    public partial bool StampDate { get; set; }

    /// <summary>Ob Stempel den Namen tragen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StampPreview), nameof(ShowNoNameHint))]
    public partial bool StampName { get; set; }

    /// <summary>Name für Stempel (aus den Einstellungen).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StampPreview), nameof(ShowNoNameHint))]
    public partial string UserName { get; set; }

    /// <summary>Gewähltes Bild, das mit dem nächsten Klick gesetzt wird, oder <c>null</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingImage), nameof(PendingImageText))]
    public partial PendingImage? PendingImage { get; set; }

    /// <summary>Wählbare Stempelfarben.</summary>
    public IReadOnlyList<string> StampColors => StampAnnotation.Colors;

    /// <summary>Ob die Optionsleiste die Stempel zeigt.</summary>
    public bool ShowStampOptions => Tool == Tool.Stamp;

    /// <summary>Ob ein Bild auf das Setzen wartet.</summary>
    public bool HasPendingImage => PendingImage is not null;

    /// <summary>Hinweis zum wartenden Bild.</summary>
    public string PendingImageText => PendingImage is { } image ? Loc.F("Stamp.ImagePending", image.Name) : string.Empty;

    /// <summary>Hinweis, dass „Name“ angehakt, aber kein Name eingetragen ist.</summary>
    public bool ShowNoNameHint => StampName && string.IsNullOrWhiteSpace(UserName);

    /// <summary>Der gewählte Stempel.</summary>
    public StampChoice? SelectedStamp => Stamps.Count == 0 ? null : Stamps[Math.Clamp(SelectedStampIndex, 0, Stamps.Count - 1)];

    /// <summary>Vorschau des nächsten Stempels (mit Datum und Name, in der gewählten Farbe).</summary>
    public StampAnnotation StampPreview => CreateStamp(new PdfPoint(0, 0), StampAnnotation.DefaultWidth, 0);

    /// <summary>
    /// Übernimmt die eigenen Stempel und baut die Auswahl neu auf (auch nach einem Sprachwechsel).
    /// </summary>
    /// <param name="customStamps">Die eigenen Stempel.</param>
    public void SetCustomStamps(IReadOnlyList<Core.Settings.CustomStamp> customStamps)
    {
        CustomStamps = customStamps;
        Stamps.Clear();
        foreach (var (key, color) in StampAnnotation.Presets)
        {
            Stamps.Add(new StampChoice(Stamps.Count, Loc.T($"Stamp.{key}"), color, isCustom: false));
        }

        foreach (var stamp in customStamps)
        {
            Stamps.Add(new StampChoice(Stamps.Count, stamp.Text, stamp.ColorHex, isCustom: true));
        }

        var index = Math.Clamp(SelectedStampIndex, 0, Stamps.Count - 1);
        SelectedStampIndex = index;
        foreach (var choice in Stamps)
        {
            choice.IsSelected = choice.Index == index;
        }

        OnPropertyChanged(nameof(SelectedStamp));
        OnPropertyChanged(nameof(StampPreview));
        OnPropertyChanged(nameof(PresetChoices));
        OnPropertyChanged(nameof(CustomChoices));
        OnPropertyChanged(nameof(HasCustomStamps));
    }

    /// <summary>
    /// Markiert die gewählte Stempelfarbe.
    /// </summary>
    /// <param name="value">Die neue Farbe.</param>
    partial void OnStampColorChanged(string value)
    {
        foreach (var choice in StampColorChoices)
        {
            choice.IsSelected = string.Equals(choice.ColorHex, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Wählt einen Stempel; die Farbe wird seine Standardfarbe.
    /// </summary>
    /// <param name="index">Seine Position.</param>
    public void SelectStamp(int index)
    {
        SelectedStampIndex = Math.Clamp(index, 0, Stamps.Count - 1);
        foreach (var choice in Stamps)
        {
            choice.IsSelected = choice.Index == SelectedStampIndex;
        }

        StampColor = Stamps[SelectedStampIndex].ColorHex;
        OnPropertyChanged(nameof(SelectedStamp));
    }

    /// <summary>
    /// Zweite Stempelzeile aus Datum und Name, je nach Häkchen.
    /// </summary>
    /// <param name="today">Heutiges Datum.</param>
    /// <returns>Die Zeile oder leer.</returns>
    public string StampDetail(DateTime today) => StampAnnotation.DetailLine(
        StampDate ? today.ToString(Loc.Language == Core.Settings.UiLanguage.German ? "dd.MM.yyyy" : "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : null,
        StampName ? UserName : null);

    /// <summary>
    /// Erzeugt den gewählten Stempel.
    /// </summary>
    /// <param name="center">Mitte.</param>
    /// <param name="width">Breite.</param>
    /// <param name="angle">Drehung in Grad.</param>
    /// <returns>Der Stempel mit neuer Kennung.</returns>
    public StampAnnotation CreateStamp(PdfPoint center, double width, double angle) =>
        StampAnnotation.Create(SelectedStamp?.Text ?? string.Empty, StampDetail(DateTime.Today), StampColor, center, width, angle);

    /// <summary>Wo Pfeile ihre Spitzen tragen.</summary>
    [ObservableProperty]
    public partial ArrowEnds ArrowEnds { get; set; }

    /// <summary>Farbe des Text-Werkzeugs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentColor))]
    public partial string TextColor { get; set; }

    /// <summary>Schriftgröße des Text-Werkzeugs (8–36 pt).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentWidth), nameof(WidthText))]
    public partial double TextSize { get; set; }

    /// <summary>Strichstärke Freihand.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentWidth), nameof(WidthText))]
    public partial double FreehandWidth { get; set; }

    /// <summary>Strichstärke Rechteck.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentWidth), nameof(WidthText))]
    public partial double RectangleWidth { get; set; }

    /// <summary>Höhe des Markers.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentWidth), nameof(WidthText))]
    public partial double MarkerHeight { get; set; }

    /// <summary>Glättung in Prozent.</summary>
    [ObservableProperty]
    public partial int Smoothing { get; set; }

    /// <summary>Druckempfindlichkeit für Freihand.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPressureHint))]
    public partial bool PressureSensitive { get; set; }

    /// <summary>Radius des Radierers.</summary>
    [ObservableProperty]
    public partial double EraserRadius { get; set; }

    /// <summary>Ob zuletzt ein Stift ohne Andruck gezeichnet hat.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPressureHint))]
    public partial bool PenReportedNoPressure { get; set; }

    /// <summary>Titel in der Optionsleiste.</summary>
    public string Title => Loc.T($"Tool.{Tool}");

    /// <summary>Hinweis rechts in der Optionsleiste.</summary>
    public string Hint => Loc.T($"Hint.{Tool}");

    /// <summary>Ob die gemeinsame Farbwahl angeboten wird (Freihand, Rechteck, Kommentar).</summary>
    public bool ShowColorPicker => Tool is Tool.Freehand or Tool.Shape or Tool.Comment or Tool.Text;

    /// <summary>Ob die festen Markerfarben angeboten werden.</summary>
    public bool ShowMarkerColors => Tool == Tool.Marker;

    /// <summary>Ob die Formauswahl gezeigt wird.</summary>
    public bool ShowShapeKinds => Tool == Tool.Shape;

    /// <summary>Ob die Lage der Pfeilspitzen gewählt werden kann (nur bei Pfeilen).</summary>
    public bool ShowArrowEnds => Tool == Tool.Shape && Shape is ShapeKind.Arrow or ShapeKind.ClosedArrow;

    /// <summary>Ob eine Strichstärke einstellbar ist.</summary>
    public bool ShowWidth => Tool is Tool.Freehand or Tool.Shape or Tool.Marker or Tool.Text;

    /// <summary>Beschriftung des Schiebereglers: Strichstärke bzw. beim Text die Schriftgröße.</summary>
    public string WidthLabel => Loc.T(Tool == Tool.Text ? "Options.FontSize" : "Options.Width");

    /// <summary>Wert des Schiebereglers mit Einheit.</summary>
    public string WidthText => $"{CurrentWidth:0} {(Tool == Tool.Text ? "pt" : "px")}";

    /// <summary>Ob Glättung und Druckempfindlichkeit angeboten werden.</summary>
    public bool ShowFreehandOptions => Tool == Tool.Freehand;

    /// <summary>Ob die Radiergröße angeboten wird.</summary>
    public bool ShowEraserOptions => Tool == Tool.Eraser;

    /// <summary>Ob der Hinweis „kein Andruck“ angezeigt wird.</summary>
    public bool ShowPressureHint => Tool == Tool.Freehand && PressureSensitive && PenReportedNoPressure;

    /// <summary>Ziel der Farbwahl für das aktuelle Werkzeug.</summary>
    public ColorTarget ColorTarget => Tool switch
    {
        Tool.Shape => ColorTarget.Shape,
        Tool.Comment => ColorTarget.Comment,
        Tool.Text => ColorTarget.Text,
        _ => ColorTarget.Freehand,
    };

    /// <summary>Farbe des aktuellen Werkzeugs.</summary>
    public string CurrentColor => Tool switch
    {
        Tool.Shape => RectangleColor,
        Tool.Comment => CommentColor,
        Tool.Marker => MarkerColor,
        Tool.Text => TextColor,
        _ => FreehandColor,
    };

    /// <summary>Strichstärke des aktuellen Werkzeugs.</summary>
    public double CurrentWidth
    {
        get => Tool switch
        {
            Tool.Shape => RectangleWidth,
            Tool.Marker => MarkerHeight,
            Tool.Text => TextSize,
            _ => FreehandWidth,
        };
        set
        {
            switch (Tool)
            {
                case Tool.Shape: RectangleWidth = value; break;
                case Tool.Marker: MarkerHeight = value; break;
                case Tool.Text: TextSize = value; break;
                default: FreehandWidth = value; break;
            }
        }
    }

    /// <summary>Kleinste Strichstärke des aktuellen Werkzeugs.</summary>
    public double WidthMinimum => Tool switch
    {
        Tool.Marker => 8,
        Tool.Text => TextAnnotation.MinFontSize,
        _ => 1,
    };

    /// <summary>Größte Strichstärke des aktuellen Werkzeugs.</summary>
    public double WidthMaximum => Tool switch
    {
        Tool.Marker => 30,
        Tool.Text => TextAnnotation.MaxFontSize,
        _ => 12,
    };

    /// <summary>
    /// Farbe eines Farbziels.
    /// </summary>
    /// <param name="target">Das Ziel (nicht <see cref="ColorTarget.Accent"/>).</param>
    /// <returns>Die Farbe.</returns>
    public string ColorOf(ColorTarget target) => target switch
    {
        ColorTarget.Shape => RectangleColor,
        ColorTarget.Comment => CommentColor,
        ColorTarget.Text => TextColor,
        _ => FreehandColor,
    };

    /// <summary>
    /// Setzt die Farbe eines Farbziels.
    /// </summary>
    /// <param name="target">Das Ziel.</param>
    /// <param name="hex">Die Farbe.</param>
    public void SetColor(ColorTarget target, string hex)
    {
        switch (target)
        {
            case ColorTarget.Shape: RectangleColor = hex; break;
            case ColorTarget.Comment: CommentColor = hex; break;
            case ColorTarget.Text: TextColor = hex; break;
            case ColorTarget.Freehand: FreehandColor = hex; break;
        }
    }

    /// <summary>
    /// Standardfarbe eines Farbziels.
    /// </summary>
    /// <param name="target">Das Ziel.</param>
    /// <returns>Die Farbe.</returns>
    public static string DefaultColorOf(ColorTarget target) => target switch
    {
        ColorTarget.Shape => DrawingColors.RectangleDefault,
        ColorTarget.Comment => DrawingColors.CommentDefault,
        ColorTarget.Freehand => DrawingColors.FreehandDefault,
        ColorTarget.Text => DrawingColors.TextDefault,
        _ => AccentPalette.DefaultAccent,
    };

    /// <summary>
    /// Schreibt die Werte in die Einstellungen.
    /// </summary>
    /// <param name="settings">Bisherige Einstellungen.</param>
    /// <returns>Aktualisierte Einstellungen.</returns>
    public AppSettings ApplyTo(AppSettings settings) => settings with
    {
        FreehandColor = FreehandColor,
        RectangleColor = RectangleColor,
        CommentColor = CommentColor,
        TextColor = TextColor,
        Shape = Shape,
        SelectedSignature = SelectedSignatureIndex,
        SelectedStamp = SelectedStampIndex,
        StampColor = StampColor,
        StampDate = StampDate,
        StampName = StampName,
        ArrowEnds = ArrowEnds,
        TextSize = TextAnnotation.ClampFontSize(TextSize),
        MarkerColor = MarkerColor,
        FreehandWidth = FreehandWidth,
        RectangleWidth = RectangleWidth,
        MarkerHeight = MarkerHeight,
        Smoothing = Smoothing,
        PressureSensitive = PressureSensitive,
        EraserRadius = EraserHitTest.ClampRadius(EraserRadius),
    };

    /// <summary>
    /// Meldet Änderungen dauerhafter Werte.
    /// </summary>
    /// <param name="e">Die Änderung.</param>
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(FreehandColor) or nameof(RectangleColor) or nameof(CommentColor) or nameof(MarkerColor) or nameof(TextColor) or nameof(TextSize) or nameof(Shape) or nameof(ArrowEnds) or nameof(SelectedSignatureIndex)
            or nameof(SelectedStampIndex) or nameof(StampColor) or nameof(StampDate) or nameof(StampName)
            or nameof(FreehandWidth) or nameof(RectangleWidth) or nameof(MarkerHeight) or nameof(Smoothing)
            or nameof(PressureSensitive) or nameof(EraserRadius))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}

/// <summary>
/// Eine gespeicherte Unterschrift zur Auswahl in der Optionsleiste.
/// </summary>
/// <param name="index">Position in der Liste.</param>
/// <param name="signature">Die Unterschrift.</param>
public sealed partial class SignatureChoice(int index, Core.Signatures.Signature signature) : ObservableObject
{
    /// <summary>Position in der Liste.</summary>
    public int Index { get; } = index;

    /// <summary>Die Unterschrift.</summary>
    public Core.Signatures.Signature Signature { get; } = signature;

    /// <summary>Ob sie gewählt ist.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Ein Stempel zur Auswahl: Vorlage oder eigener Stempel.
/// </summary>
/// <param name="index">Position in der Liste.</param>
/// <param name="text">Stempeltext.</param>
/// <param name="colorHex">Standardfarbe.</param>
/// <param name="isCustom">Ob es ein eigener Stempel ist (löschbar).</param>
public sealed partial class StampChoice(int index, string text, string colorHex, bool isCustom) : ObservableObject
{
    /// <summary>Position in der Liste.</summary>
    public int Index { get; } = index;

    /// <summary>Stempeltext.</summary>
    public string Text { get; } = text;

    /// <summary>Standardfarbe.</summary>
    public string ColorHex { get; } = colorHex;

    /// <summary>Ob es ein eigener Stempel ist.</summary>
    public bool IsCustom { get; } = isCustom;

    /// <summary>Vorschau ohne Datum und Name.</summary>
    public StampAnnotation Preview { get; } = StampAnnotation.Create(text, string.Empty, colorHex, new PdfPoint(0, 0), StampAnnotation.DefaultWidth);

    /// <summary>Ob er gewählt ist.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Gewähltes Bild, das noch auf die Seite gesetzt wird.
/// </summary>
/// <param name="Image">Die Bilddaten.</param>
/// <param name="Name">Dateiname.</param>
public sealed record PendingImage(ImageData Image, string Name);

/// <summary>
/// Eine Stempelfarbe zur Auswahl.
/// </summary>
/// <param name="colorHex">Die Farbe.</param>
public sealed partial class StampColorChoice(string colorHex) : ObservableObject
{
    /// <summary>Die Farbe.</summary>
    public string ColorHex { get; } = colorHex;

    /// <summary>Name der Farbe als Tooltip.</summary>
    public string Name => Loc.T($"Stamp.Color.{ColorHex.TrimStart('#')}");

    /// <summary>Ob sie gewählt ist.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
