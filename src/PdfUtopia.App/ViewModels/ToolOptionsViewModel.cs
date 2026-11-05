using CommunityToolkit.Mvvm.ComponentModel;
using PdfUtopia.App.Localization;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
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
        TextSize = settings.TextSize;
        MarkerColor = settings.MarkerColor;
        FreehandWidth = settings.FreehandWidth;
        RectangleWidth = settings.RectangleWidth;
        MarkerHeight = settings.MarkerHeight;
        Smoothing = settings.Smoothing;
        PressureSensitive = settings.PressureSensitive;
        EraserRadius = settings.EraserRadius;
    }

    /// <summary>Wird bei jeder dauerhaft zu speichernden Änderung ausgelöst.</summary>
    public event EventHandler? Changed;

    /// <summary>Feste Textmarker-Farben.</summary>
    public IReadOnlyList<string> MarkerColors => DrawingColors.MarkerColors;

    /// <summary>Gewähltes Werkzeug.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Hint), nameof(ShowColorPicker), nameof(ShowMarkerColors), nameof(ShowShapeKinds), nameof(ShowArrowEnds), nameof(ShowWidth),
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
        if (e.PropertyName is nameof(FreehandColor) or nameof(RectangleColor) or nameof(CommentColor) or nameof(MarkerColor) or nameof(TextColor) or nameof(TextSize) or nameof(Shape) or nameof(ArrowEnds)
            or nameof(FreehandWidth) or nameof(RectangleWidth) or nameof(MarkerHeight) or nameof(Smoothing)
            or nameof(PressureSensitive) or nameof(EraserRadius))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
