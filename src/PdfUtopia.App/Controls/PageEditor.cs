using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using PdfUtopia.App.Localization;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Theming;
using BezierSegment = PdfUtopia.Core.Drawing.BezierSegment;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Zeigt die aktuelle Seite mit ihren Annotationen und nimmt Eingaben für Marker, Freihand, Formen,
/// Kommentar, Text und Radierer entgegen. Text wird in einem eingeblendeten Textfeld geschrieben (A43). Die Größe entspricht der angezeigten Seite in Punkten; der Zoom wird von
/// außen über eine Layout-Transformation angewandt, die Seite aber passend zum Zoom scharf gerendert.
/// Eingaberegeln aus dem Mockup-Test (A12): nicht an Zeiger-ID oder Gerätetyp gebunden, nur bei Kontakt.
/// </summary>
public sealed class PageEditor : Control
{
    /// <summary>Angezeigtes Dokument.</summary>
    public static readonly StyledProperty<DocumentViewModel?> DocumentProperty =
        AvaloniaProperty.Register<PageEditor, DocumentViewModel?>(nameof(Document));

    /// <summary>Werkzeug und Werkzeugeinstellungen.</summary>
    public static readonly StyledProperty<ToolOptionsViewModel?> OptionsProperty =
        AvaloniaProperty.Register<PageEditor, ToolOptionsViewModel?>(nameof(Options));

    /// <summary>Zoom (für scharfes Rendern).</summary>
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<PageEditor, double>(nameof(Zoom), 1);

    private const double MinDragDistance = 4;
    private const double BadgeRadius = 11;
    private const double CommentBorderWidth = 1.5;
    private const byte CommentFillAlpha = 36;
    private const byte MarkerAlpha = 115;
    private const double TextHitMargin = 3;

    /// <summary>Schrift mit denselben Zeichenbreiten wie Helvetica, damit Anzeige und PDF gleich umbrechen.</summary>
    private static readonly FontFamily TextFont = new("Liberation Sans, Arial, Helvetica, sans-serif");

    private readonly StrokeRecorder _recorder = new();
    private readonly List<PdfPoint> _eraserPath = [];
    private readonly HashSet<Guid> _eraserHits = [];
    private Tool? _gesture;
    private PdfPoint _start;
    private PdfPoint _current;
    private PdfPoint? _pointer;
    private bool _eraserEndHovering;
    private bool _eraserCursorShown;
    private Guid? _hoveredBadge;
    private PdfRect? _suggestedParagraph;
    private DocumentViewModel? _subscribed;
    private ToolOptionsViewModel? _subscribedOptions;
    private TextAnnotation? _movingText;
    private bool _constrain;
    private Guid? _hoveredText;
    private TextBox? _textEditor;
    private TextAnnotation? _editing;
    private int _editingPageId;
    private bool _editingIsNew;

    static PageEditor()
    {
        AffectsMeasure<PageEditor>(DocumentProperty);
        AffectsRender<PageEditor>(DocumentProperty, OptionsProperty);
        FocusableProperty.OverrideDefaultValue<PageEditor>(true);
    }

    /// <summary>
    /// Initialisiert den Editor; Text auf der Seite wird in Graustufen geglättet wie die gerenderte Seite.
    /// </summary>
    public PageEditor() => TextOptions.SetTextRenderingMode(this, TextRenderingMode.Antialias);

    /// <summary>Angezeigtes Dokument.</summary>
    public DocumentViewModel? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>Werkzeug und Werkzeugeinstellungen.</summary>
    public ToolOptionsViewModel? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>Zoom.</summary>
    public double Zoom
    {
        get => GetValue(ZoomProperty);
        set => SetValue(ZoomProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Document is null)
        {
            return default;
        }

        _textEditor?.Measure(Size.Infinity);
        var (width, height) = Document.CurrentDisplaySize;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Eingabefeld an den Anker des bearbeiteten Texts; die Drehung übernimmt seine Render-Transformation
        if (_textEditor is { } editor && _editing is { } text)
        {
            var anchor = ((TextAnnotation)ToDisplay(text)).Anchor;
            editor.Arrange(new Rect(new Point(anchor.X, anchor.Y), new Size(text.Width, editor.DesiredSize.Height)));
        }

        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
        {
            CommitText();
            Subscribe(Document);
            RequestRender();
        }
        else if (change.Property == OptionsProperty)
        {
            SubscribeOptions(Options);
        }
        else if (change.Property == ZoomProperty)
        {
            RequestRender();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Application.Current is { } application)
        {
            // Neue Akzentfarbe oder neues Theme: Suchtreffer neu einfärben
            application.ResourcesChanged += OnApplicationResourcesChanged;
        }

        RequestRender();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (Application.Current is { } application)
        {
            application.ResourcesChanged -= OnApplicationResourcesChanged;
        }
    }

    /// <summary>
    /// Zeichnet nach geänderten Anwendungsressourcen neu.
    /// </summary>
    private void OnApplicationResourcesChanged(object? sender, ResourcesChangedEventArgs e) => InvalidateVisual();

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        context.FillRectangle(Brushes.White, bounds);
        if (Document is not { } document)
        {
            return;
        }

        if (document.CurrentBitmap is { } bitmap)
        {
            context.DrawImage(bitmap, bounds);
        }

        DrawSearchHits(context, document);

        foreach (var annotation in document.CurrentAnnotations.Where(annotation => annotation.Id != _editing?.Id && annotation.Id != _movingText?.Id))
        {
            var display = ToDisplay(annotation);
            using (context.PushOpacity(_eraserHits.Contains(annotation.Id) ? 0.2 : 1))
            {
                DrawAnnotation(context, display);
            }
        }

        DrawSuggestion(context);
        DrawTextFrames(context);
        DrawGesture(context);
        DrawEraserCursor(context);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Document is null || Options is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(this);
        var display = ToPoint(point.Position);
        _constrain = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_hoveredBadge is { } badge && point.Properties.IsLeftButtonPressed)
        {
            Document.Document.RemoveAnnotation(Document.CurrentPage.Id, badge);
            _hoveredBadge = null;
            e.Handled = true;
            return;
        }

        var sample = Sample(point, e.Pointer.Type, display);
        if (!sample.IsInContact || sample.IsExtraTouch || (e.Pointer.Type == PointerType.Mouse && !point.Properties.IsLeftButtonPressed))
        {
            return;
        }

        var tool = sample.IsEraser ? Tool.Eraser : Options.Tool;
        if (tool == Tool.Select)
        {
            return;
        }

        // Ein Klick neben das Textfeld schließt die Bearbeitung ab
        CommitText();

        // Sonst wertet die umgebende Bildlaufleiste Stiftbewegungen als Wischgeste und verschiebt die Seite
        e.PreventGestureRecognition();
        Begin(tool, display, sample);
        TryCapture(e.Pointer);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetCurrentPoint(this);
        _pointer = ToPoint(point.Position);
        _eraserEndHovering = point.Properties.IsEraser;
        _constrain = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (_gesture is null)
        {
            UpdateHover(_pointer.Value, point.Properties.IsEraser);
            return;
        }

        e.PreventGestureRecognition();
        e.Handled = true;
        var samples = e.GetIntermediatePoints(this).Select(p => Sample(p, e.Pointer.Type, ToPoint(p.Position))).ToList();
        if (samples.Count == 0 || !samples[^1].IsInContact)
        {
            // Stift abgehoben, ohne dass das Loslassen gemeldet wurde (A12)
            Finish();
            return;
        }

        Continue(samples);
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _constrain = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        Finish();
    }

    /// <inheritdoc />
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        Finish();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointer = null;
        _eraserEndHovering = false;
        _hoveredBadge = null;
        _suggestedParagraph = null;
        Cursor = Cursor.Default;
        InvalidateVisual();
    }

    /// <summary>
    /// Beginnt eine Eingabe mit einem Werkzeug.
    /// </summary>
    private void Begin(Tool tool, PdfPoint display, InputSample sample)
    {
        _gesture = tool;
        _start = _current = display;
        _eraserPath.Clear();
        _eraserHits.Clear();
        _movingText = tool == Tool.Text ? TextAt(display) : null;
        if (tool == Tool.Freehand)
        {
            _recorder.Begin(sample with { Position = ToPage(display), IsEraser = false });
        }
        else if (tool == Tool.Eraser)
        {
            Erase(display);
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Setzt eine laufende Eingabe fort.
    /// </summary>
    private void Continue(IReadOnlyList<InputSample> samples)
    {
        _current = samples[^1].Position;
        switch (_gesture)
        {
            case Tool.Freehand:
                _recorder.Move(samples.Select(sample => sample with { Position = ToPage(sample.Position), IsEraser = false }));
                break;
            case Tool.Eraser:
                foreach (var sample in samples)
                {
                    Erase(sample.Position);
                }

                break;
        }

        InvalidateVisual();
    }

    /// <summary>
    /// Schließt eine Eingabe ab und legt die Annotation an bzw. entfernt die radierten.
    /// </summary>
    private void Finish()
    {
        if (_gesture is not { } tool || Document is not { } document || Options is not { } options)
        {
            return;
        }

        _gesture = null;
        switch (tool)
        {
            case Tool.Freehand when _recorder.End() is { } points:
                document.AddAnnotation(new InkAnnotation(Guid.NewGuid(), options.FreehandColor, options.FreehandWidth, points,
                    options.Smoothing / 100.0, options.PressureSensitive));
                options.PenReportedNoPressure = _recorder.PenWithoutPressure;
                break;
            case Tool.Marker when Math.Abs(_current.X - _start.X) >= MinDragDistance:
                document.AddAnnotation(new HighlightAnnotation(Guid.NewGuid(), options.MarkerColor, ToPage(MarkerBand())));
                break;
            case Tool.Shape when DraggedShape(options, Guid.NewGuid(), toPage: true) is { } shape:
                document.AddAnnotation(shape);
                break;
            case Tool.Comment when IsDrag():
                document.AddAnnotation(new CommentAnnotation(Guid.NewGuid(), options.CommentColor, ToPage(DragRect()), Loc.T("Comment.New")));
                break;
            case Tool.Comment when ParagraphAt(_start) is { } paragraph && !IsCommented(paragraph):
                // Klick ohne Ziehen: erkannten Absatz kommentieren (F21)
                document.AddAnnotation(new CommentAnnotation(Guid.NewGuid(), options.CommentColor, paragraph, Loc.T("Comment.New")));
                break;
            case Tool.Eraser when _eraserHits.Count > 0:
                document.RemoveAnnotations(_eraserHits.ToList());
                break;
            case Tool.Text:
                FinishText(document, options);
                break;
        }

        _movingText = null;

        _eraserHits.Clear();
        InvalidateVisual();
    }

    /// <summary>
    /// Merkt sich, was der Radierer an einem Punkt berührt.
    /// </summary>
    private void Erase(PdfPoint display)
    {
        _eraserPath.Add(display);
        var page = ToPage(display);
        var radius = Options?.EraserRadius ?? EraserHitTest.DefaultRadius;
        foreach (var hit in Document!.CurrentAnnotations.Where(annotation => EraserHitTest.IsErasable(annotation) && EraserHitTest.Hits(annotation, page, radius)))
        {
            _eraserHits.Add(hit.Id);
        }
    }

    /// <summary>
    /// Ermittelt, ob die Maus über einer Fußnotenzahl steht (Kommentar entfernen, A26), und setzt den Mauszeiger.
    /// </summary>
    private void UpdateHover(PdfPoint display, bool eraserEnd)
    {
        var previous = _hoveredBadge;
        _hoveredBadge = Document?.CurrentAnnotations.OfType<CommentAnnotation>()
            .Select(comment => (Id: comment.Id, Bounds: ((CommentAnnotation)ToDisplay(comment)).Bounds))
            .Where(item => new PdfPoint(item.Bounds.Right, item.Bounds.Y).DistanceTo(display) <= BadgeRadius)
            .Select(item => (Guid?)item.Id)
            .LastOrDefault();
        var previousSuggestion = _suggestedParagraph;
        _suggestedParagraph = Options?.Tool == Tool.Comment && _hoveredBadge is null ? ParagraphAt(display) : null;
        if (previousSuggestion != _suggestedParagraph)
        {
            InvalidateVisual();
        }

        var previousText = _hoveredText;
        _hoveredText = Options?.Tool == Tool.Text && !eraserEnd ? TextAt(display)?.Id : null;
        if (previousText != _hoveredText)
        {
            InvalidateVisual();
        }

        var erasing = eraserEnd || Options?.Tool == Tool.Eraser;
        Cursor = _hoveredBadge is not null ? new Cursor(StandardCursorType.Hand)
            : erasing ? new Cursor(StandardCursorType.None)
            : _hoveredText is not null ? new Cursor(StandardCursorType.SizeAll)
            : Options?.Tool == Tool.Text ? new Cursor(StandardCursorType.Ibeam)
            : Options?.Tool is Tool.Select or null ? Cursor.Default
            : new Cursor(StandardCursorType.Cross);
        // Radierkreis folgt dem Zeiger; verschwindet er (Werkzeug oder Stiftende gewechselt), einmal neu zeichnen
        if (previous != _hoveredBadge || erasing || _eraserCursorShown)
        {
            InvalidateVisual();
        }

        _eraserCursorShown = erasing;
    }

    /// <summary>
    /// Zeichnet eine Annotation (in Anzeige-Koordinaten).
    /// </summary>
    private void DrawAnnotation(DrawingContext context, Annotation annotation)
    {
        switch (annotation)
        {
            case InkAnnotation ink:
                DrawInk(context, ink);
                break;
            case HighlightAnnotation highlight:
                context.FillRectangle(new SolidColorBrush(WithAlpha(highlight.ColorHex, MarkerAlpha)), ToRect(highlight.Bounds));
                break;
            case RectangleAnnotation rectangle:
                DrawRounded(context, null, new Pen(Brush(rectangle.ColorHex), rectangle.Width), ToRect(rectangle.Bounds));
                break;
            case CommentAnnotation comment:
                DrawComment(context, comment);
                break;
            case TextAnnotation text:
                DrawText(context, text);
                break;
            case LineAnnotation line:
                DrawLine(context, line);
                break;
            case EllipseAnnotation ellipse:
                var bounds = ToRect(ellipse.Bounds);
                context.DrawEllipse(null, new Pen(Brush(ellipse.ColorHex), ellipse.Width), bounds.Center, bounds.Width / 2, bounds.Height / 2);
                break;
        }
    }

    /// <summary>
    /// Linie mit runden Enden, beim Pfeil mit offenen oder gefüllten Spitzen an einem oder beiden Enden.
    /// </summary>
    private static void DrawLine(DrawingContext context, LineAnnotation line)
    {
        var pen = RoundPen(Brush(line.ColorHex), line.Width);
        context.DrawLine(pen, new Point(line.StrokeStart.X, line.StrokeStart.Y), new Point(line.StrokeEnd.X, line.StrokeEnd.Y));
        foreach (var head in line.Heads)
        {
            var (tip, left, right) = (new Point(head.Tip.X, head.Tip.Y), new Point(head.Left.X, head.Left.Y), new Point(head.Right.X, head.Right.Y));
            if (line.Arrow != ArrowStyle.Closed)
            {
                context.DrawLine(pen, left, tip);
                context.DrawLine(pen, right, tip);
                continue;
            }

            var triangle = new StreamGeometry();
            using (var figure = triangle.Open())
            {
                figure.BeginFigure(left, true);
                figure.LineTo(tip);
                figure.LineTo(right);
                figure.EndFigure(true);
            }

            context.DrawGeometry(pen.Brush, pen, triangle);
        }
    }

    /// <summary>
    /// Aufgezogene Form in Anzeige- oder Seitenkoordinaten; <c>null</c>, wenn kaum gezogen wurde. Mit Umschalt rasten
    /// Linien in 15°-Schritten ein, Rechteck und Ellipse werden quadratisch.
    /// </summary>
    private Annotation? DraggedShape(ToolOptionsViewModel options, Guid id, bool toPage)
    {
        var isLine = options.Shape is ShapeKind.Line or ShapeKind.Arrow or ShapeKind.ClosedArrow;
        var end = !_constrain ? _current : isLine ? ShapeConstraint.SnapAngle(_start, _current) : ShapeConstraint.Square(_start, _current);
        var rect = PdfRect.FromCorners(_start, end);
        var pageRect = toPage ? ToPage(rect) : rect;
        var (color, width) = (options.RectangleColor, options.RectangleWidth);
        if (isLine)
        {
            return _start.DistanceTo(end) < MinDragDistance ? null
                : new LineAnnotation(id, color, width, toPage ? ToPage(_start) : _start, toPage ? ToPage(end) : end, options.Shape switch
                {
                    ShapeKind.Arrow => ArrowStyle.Open,
                    ShapeKind.ClosedArrow => ArrowStyle.Closed,
                    _ => ArrowStyle.None,
                }, options.ArrowEnds);
        }

        if (rect.Width < MinDragDistance || rect.Height < MinDragDistance)
        {
            return null;
        }

        return options.Shape == ShapeKind.Ellipse
            ? new EllipseAnnotation(id, color, width, pageRect)
            : new RectangleAnnotation(id, color, width, pageRect);
    }

    /// <summary>
    /// Freier Text: Zeilen wie beim Speichern umbrochen, in Leserichtung gedreht (Anzeige-Koordinaten).
    /// </summary>
    private static void DrawText(DrawingContext context, TextAnnotation text)
    {
        var brush = Brush(text.ColorHex);
        var typeface = new Typeface(TextFont);
        var transform = Matrix.CreateRotation(-(int)text.Orientation * Math.PI / 180) * Matrix.CreateTranslation(text.Anchor.X, text.Anchor.Y);
        using (context.PushTransform(transform))
        {
            var lines = text.Lines;
            for (var i = 0; i < lines.Count; i++)
            {
                var line = new FormattedText(lines[i], CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, text.FontSize, brush);
                context.DrawText(line, new Point(0, text.Baseline(i) - line.Baseline));
            }
        }
    }

    /// <summary>
    /// Gestrichelter Rahmen um den Text unter dem Mauszeiger und um den gerade bearbeiteten Text.
    /// </summary>
    private void DrawTextFrames(DrawingContext context)
    {
        if (Document is null || _gesture is not null)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(Color.Parse("#6B7280")), 1, new DashStyle([3, 2], 0));
        var framed = Document.CurrentAnnotations.OfType<TextAnnotation>().Where(text => text.Id == _hoveredText && text.Id != _editing?.Id);
        foreach (var text in _editing is { } editing ? framed.Append(editing) : framed)
        {
            context.DrawRectangle(null, pen, ToRect(Inflate(((TextAnnotation)ToDisplay(text)).Bounds, TextHitMargin)), 2, 2);
        }
    }

    /// <summary>
    /// Text an einer Anzeigeposition (der oberste) oder <c>null</c>.
    /// </summary>
    private TextAnnotation? TextAt(PdfPoint display) =>
        Document?.CurrentAnnotations.OfType<TextAnnotation>()
            .LastOrDefault(text => Contains(Inflate(((TextAnnotation)ToDisplay(text)).Bounds, TextHitMargin), display));

    /// <summary>
    /// Text-Werkzeug losgelassen: Text verschieben, zum Bearbeiten öffnen oder neues Textfeld anlegen.
    /// </summary>
    private void FinishText(DocumentViewModel document, ToolOptionsViewModel options)
    {
        var moved = _current.DistanceTo(_start) >= MinDragDistance;
        if (_movingText is { } existing)
        {
            if (!moved)
            {
                BeginTextEdit(existing, isNew: false);
                return;
            }

            var anchor = ((TextAnnotation)ToDisplay(existing)).Anchor;
            var target = new PdfPoint(anchor.X + _current.X - _start.X, anchor.Y + _current.Y - _start.Y);
            document.Document.UpdateAnnotation(document.CurrentPage.Id, existing with { Anchor = ToPage(target) });
            return;
        }

        var fontSize = TextAnnotation.ClampFontSize(options.TextSize);
        var (displayWidth, _) = document.CurrentDisplaySize;
        var drag = DragRect();
        var (left, top, width) = Math.Abs(_current.X - _start.X) >= MinDragDistance
            ? (drag.X, drag.Y, Math.Max(TextAnnotation.MinWidth, drag.Width))
            // Klick: Zeile mittig auf den Klickpunkt, Standardbreite bis höchstens zum rechten Rand
            : (_start.X, _start.Y - fontSize * TextAnnotation.LineSpacing / 2,
                Math.Clamp(displayWidth - _start.X - 8, TextAnnotation.MinWidth, TextAnnotation.DefaultWidth));
        var text = new TextAnnotation(Guid.NewGuid(), options.TextColor, ToPage(new PdfPoint(left, top)), width, fontSize, string.Empty,
            document.CurrentPage.Rotation);
        BeginTextEdit(text, isNew: true);
    }

    /// <summary>
    /// Blendet über dem Text ein Eingabefeld ein (gleiche Schrift, Größe, Breite und Drehung).
    /// </summary>
    private void BeginTextEdit(TextAnnotation text, bool isNew)
    {
        CommitText();
        _editing = text;
        _editingIsNew = isNew;
        _editingPageId = Document!.CurrentPage.Id;
        var editor = new TextBox
        {
            Text = text.Text,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Padding = default,
            BorderThickness = default,
            MinHeight = 0,
            MinWidth = 0,
            FontFamily = TextFont,
            RenderTransformOrigin = RelativePoint.TopLeft,
        };
        // Durchsichtig in jedem Zustand, damit das Feld wie der fertige Text auf der Seite aussieht
        foreach (var key in new[] { "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused",
                     "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused" })
        {
            editor.Resources[key] = Brushes.Transparent;
        }

        editor.TextChanged += (_, _) =>
        {
            if (_editing is not null && editor == _textEditor)
            {
                _editing = _editing with { Text = editor.Text ?? string.Empty };
                InvalidateVisual();
            }
        };
        editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CommitText();
            }
        };
        editor.LostFocus += (_, _) =>
        {
            if (editor == _textEditor)
            {
                CommitText();
            }
        };
        _textEditor = editor;
        ApplyTextStyle();
        LogicalChildren.Add(editor);
        VisualChildren.Add(editor);
        InvalidateMeasure();
        Dispatcher.UIThread.Post(() =>
        {
            editor.Focus();
            editor.CaretIndex = editor.Text?.Length ?? 0;
        }, DispatcherPriority.Loaded);
        InvalidateVisual();
    }

    /// <summary>
    /// Übernimmt Lage, Schrift und Farbe des bearbeiteten Texts ins Eingabefeld.
    /// </summary>
    private void ApplyTextStyle()
    {
        if (_textEditor is not { } editor || _editing is not { } text)
        {
            return;
        }

        var display = (TextAnnotation)ToDisplay(text);
        var brush = Brush(text.ColorHex);
        foreach (var key in new[] { "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused" })
        {
            editor.Resources[key] = brush;
        }

        editor.Foreground = brush;
        editor.CaretBrush = brush;
        editor.FontSize = text.FontSize;
        editor.LineHeight = text.LineHeight;
        editor.Width = text.Width;
        editor.RenderTransform = new RotateTransform(-(int)display.Orientation);
        InvalidateArrange();
    }

    /// <summary>
    /// Schließt die Textbearbeitung ab: neuer Text wird angelegt, geänderter übernommen, geleerter gelöscht –
    /// jeweils als ein Rückgängig-Schritt.
    /// </summary>
    private void CommitText()
    {
        if (_editing is not { } text || _textEditor is not { } editor || Document is not { } document)
        {
            return;
        }

        _editing = null;
        _textEditor = null;
        VisualChildren.Remove(editor);
        LogicalChildren.Remove(editor);
        var result = text with { Text = (editor.Text ?? string.Empty).TrimEnd() };
        var annotations = document.Document.Annotations.For(_editingPageId);
        var original = annotations.OfType<TextAnnotation>().FirstOrDefault(candidate => candidate.Id == text.Id);
        if (_editingIsNew || original is null)
        {
            if (result.Text.Length > 0)
            {
                document.Document.AddAnnotation(_editingPageId, result);
            }
        }
        else if (result.Text.Length == 0)
        {
            document.Document.RemoveAnnotation(_editingPageId, text.Id);
        }
        else if (result != original)
        {
            document.Document.UpdateAnnotation(_editingPageId, result);
        }

        Focus();
        InvalidateVisual();
    }

    /// <summary>
    /// Freihandstrich wie beim Speichern (gleiche Glättung und Strichstärke, A13).
    /// </summary>
    private static void DrawInk(DrawingContext context, InkAnnotation ink)
    {
        var segments = StrokeGeometry.ToSegments(ink);
        var brush = Brush(ink.ColorHex);
        if (ink.PressureSensitive)
        {
            foreach (var segment in segments)
            {
                context.DrawGeometry(null, RoundPen(brush, segment.Width), Curve([segment]));
            }

            return;
        }

        context.DrawGeometry(null, RoundPen(brush, ink.Width), Curve(segments));
    }

    /// <summary>
    /// Kommentar: hinterlegt, umrandet, Fußnotenzahl oben rechts (beim Darüberfahren ein ×).
    /// </summary>
    private void DrawComment(DrawingContext context, CommentAnnotation comment)
    {
        var rect = ToRect(comment.Bounds);
        var border = DrawingColors.CommentBorder(comment.ColorHex);
        DrawRounded(context, new SolidColorBrush(WithAlpha(comment.ColorHex, CommentFillAlpha)), new Pen(Brush(border), CommentBorderWidth), rect);

        var number = Document!.Document.Annotations.FootnoteNumber(Document.CurrentPage.Id, comment.Id) ?? 0;
        var hovered = _hoveredBadge == comment.Id;
        var center = new Point(rect.Right, rect.Top);
        var radius = hovered ? BadgeRadius * 1.12 : BadgeRadius;
        context.DrawEllipse(Brush(comment.ColorHex), border == AccentPalette.Normalize(comment.ColorHex) ? null : new Pen(Brush(border), 1), center, radius, radius);
        var label = hovered ? "×" : number.ToString(CultureInfo.CurrentCulture);
        var text = new FormattedText(label, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Inter, Segoe UI, sans-serif", FontStyle.Normal, FontWeight.SemiBold),
            hovered ? 15 : 11, Brush(DrawingColors.FootnoteNumberForeground(comment.ColorHex)));
        context.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    /// <summary>
    /// Hinterlegt die Fundstellen der Suche halbtransparent in der Akzentfarbe (E7).
    /// </summary>
    private void DrawSearchHits(DrawingContext context, DocumentViewModel document)
    {
        var hits = document.CurrentSearchHits;
        if (hits.Count == 0)
        {
            return;
        }

        var accent = this.TryFindResource("AccentBrush", ActualThemeVariant, out var resource) && resource is ISolidColorBrush brush
            ? $"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}"
            : AccentPalette.DefaultAccent;
        // Deckkraft je Farbe so, dass der Text lesbar bleibt; sehr helle Farben bekommen zusätzlich einen Rahmen
        var (opacity, outline) = DrawingColors.SearchHighlight(accent);
        var fill = new SolidColorBrush(Color.Parse(accent), opacity);
        var pen = outline is null ? null : new Pen(Brush(outline), 1);
        var (width, height) = document.CurrentPageSize;
        foreach (var hit in hits)
        {
            context.DrawRectangle(fill, pen, ToRect(PageTransform.Rotate(hit, document.CurrentPage.Rotation, width, height)), 2, 2);
        }
    }

    /// <summary>
    /// Gestrichelter Rahmen in der Kommentarfarbe um den Absatz, den ein Klick kommentieren würde.
    /// </summary>
    private void DrawSuggestion(DrawingContext context)
    {
        if (_gesture is not null || _suggestedParagraph is not { } paragraph || Options is not { } options)
        {
            return;
        }

        var (width, height) = Document!.CurrentPageSize;
        var display = PageTransform.Rotate(paragraph, Document.CurrentPage.Rotation, width, height);
        DrawRounded(context, null, new Pen(Brush(DrawingColors.CommentBorder(options.CommentColor)), 2, new DashStyle([3, 2], 0)), ToRect(display));
    }

    /// <summary>
    /// Rechteck mit der gemeinsamen Eckenrundung von Rechtecken und Kommentaren (A37).
    /// </summary>
    private static void DrawRounded(DrawingContext context, IBrush? fill, IPen pen, Rect rect)
    {
        var radius = AnnotationShape.CornerRadiusFor(rect.Width, rect.Height);
        context.DrawRectangle(fill, pen, rect, radius, radius);
    }

    /// <summary>
    /// Erkannter Absatz an einer Anzeigeposition (Koordinaten der ungedrehten Seite) oder <c>null</c>.
    /// </summary>
    private PdfRect? ParagraphAt(PdfPoint display)
    {
        if (Document is null)
        {
            return null;
        }

        var page = ToPage(display);
        return Document.CurrentTextBlocks.Where(block => page.X >= block.X && page.X <= block.Right && page.Y >= block.Y && page.Y <= block.Bottom)
            .Select(block => (PdfRect?)block)
            .FirstOrDefault();
    }

    /// <summary>
    /// Ob ein Absatz schon kommentiert ist.
    /// </summary>
    private bool IsCommented(PdfRect paragraph) =>
        Document!.CurrentAnnotations.OfType<CommentAnnotation>().Any(comment => comment.Bounds == paragraph);

    /// <summary>
    /// Vorschau der laufenden Eingabe.
    /// </summary>
    private void DrawGesture(DrawingContext context)
    {
        if (_gesture is not { } tool || Options is not { } options)
        {
            return;
        }

        switch (tool)
        {
            case Tool.Freehand when _recorder.Points.Count > 1:
                var preview = new InkAnnotation(Guid.Empty, options.FreehandColor, options.FreehandWidth, _recorder.Points.ToList(),
                    options.Smoothing / 100.0, options.PressureSensitive);
                DrawInk(context, (InkAnnotation)ToDisplay(preview));
                break;
            case Tool.Marker:
                context.FillRectangle(new SolidColorBrush(WithAlpha(options.MarkerColor, MarkerAlpha)), ToRect(MarkerBand()));
                break;
            case Tool.Shape when DraggedShape(options, Guid.Empty, toPage: false) is { } shape:
                DrawAnnotation(context, shape);
                break;
            case Tool.Comment:
                DrawRounded(context, new SolidColorBrush(WithAlpha(options.CommentColor, CommentFillAlpha)),
                    new Pen(Brush(DrawingColors.CommentBorder(options.CommentColor)), CommentBorderWidth, new DashStyle([4, 2], 0)), ToRect(DragRect()));
                break;
            case Tool.Text when _movingText is { } moving:
                var shown = (TextAnnotation)ToDisplay(moving);
                var moved = shown with { Anchor = new PdfPoint(shown.Anchor.X + _current.X - _start.X, shown.Anchor.Y + _current.Y - _start.Y) };
                DrawText(context, moved);
                context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#6B7280")), 1, new DashStyle([3, 2], 0)),
                    ToRect(Inflate(moved.Bounds, TextHitMargin)), 2, 2);
                break;
            case Tool.Text when Math.Abs(_current.X - _start.X) >= MinDragDistance:
                var lineHeight = TextAnnotation.ClampFontSize(options.TextSize) * TextAnnotation.LineSpacing;
                context.DrawRectangle(null, new Pen(Brush(options.TextColor), 1, new DashStyle([4, 2], 0)),
                    new Rect(Math.Min(_start.X, _current.X), _start.Y, Math.Abs(_current.X - _start.X), lineHeight));
                break;
        }
    }

    /// <summary>
    /// Gestrichelter Kreis in Radiergröße am Mauszeiger – auch schon, wenn der Stift über der Seite schwebt, und beim
    /// Radiererende des Stifts unabhängig vom gewählten Werkzeug.
    /// </summary>
    private void DrawEraserCursor(DrawingContext context)
    {
        var erasing = _gesture == Tool.Eraser || (_gesture is null && (Options?.Tool == Tool.Eraser || _eraserEndHovering));
        if (!erasing || _pointer is not { } pointer)
        {
            return;
        }

        var radius = Options?.EraserRadius ?? EraserHitTest.DefaultRadius;
        context.DrawEllipse(new SolidColorBrush(Color.FromArgb(30, 107, 114, 128)), new Pen(new SolidColorBrush(Color.Parse("#6B7280")), 1, new DashStyle([3, 2], 0)),
            new Point(pointer.X, pointer.Y), radius, radius);
    }

    /// <summary>
    /// Waagerechter Markerbalken zwischen Start und aktueller Position.
    /// </summary>
    private PdfRect MarkerBand()
    {
        var height = Options?.MarkerHeight ?? 18;
        return new PdfRect(Math.Min(_start.X, _current.X), _start.Y - height / 2, Math.Abs(_current.X - _start.X), height);
    }

    /// <summary>Aufgezogenes Rechteck.</summary>
    private PdfRect DragRect() => PdfRect.FromCorners(_start, _current);

    /// <summary>Ob genug gezogen wurde (sonst Klick).</summary>
    private bool IsDrag() => DragRect() is { Width: >= MinDragDistance, Height: >= MinDragDistance };

    /// <summary>
    /// Messpunkt aus einem Avalonia-Zeigerpunkt.
    /// </summary>
    private static InputSample Sample(PointerPoint point, PointerType type, PdfPoint display) => new(
        display, point.Properties.Pressure, point.Properties.IsLeftButtonPressed,
        type switch { PointerType.Pen => PointerKind.Pen, PointerType.Touch => PointerKind.Touch, _ => PointerKind.Mouse },
        IsPrimary: true, IsEraser: point.Properties.IsEraser);

    /// <summary>
    /// Zeigerbindung ist eine Komfortfunktion; bei gemischten Maus-/Stift-Meldungen kann sie fehlschlagen (A12).
    /// </summary>
    private void TryCapture(IPointer pointer)
    {
        try
        {
            pointer.Capture(this);
        }
        catch (InvalidOperationException)
        {
            // Zeichnen funktioniert auch ohne Zeigerbindung
        }
    }

    /// <summary>
    /// Fordert das Rendern der Seite passend zu Zoom und Bildschirmauflösung an.
    /// </summary>
    private void RequestRender()
    {
        if (Document is { } document)
        {
            var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            _ = document.RenderCurrentAsync(Math.Max(0.25, Zoom * scaling));
        }
    }

    /// <summary>
    /// Reagiert auf Seitenwechsel, neue Bitmap und geänderte Annotationen.
    /// </summary>
    private void Subscribe(DocumentViewModel? document)
    {
        if (_subscribed is not null)
        {
            _subscribed.PropertyChanged -= OnDocumentPropertyChanged;
            _subscribed.CurrentPageAnnotationsChanged -= OnAnnotationsChanged;
        }

        _subscribed = document;
        if (document is not null)
        {
            document.PropertyChanged += OnDocumentPropertyChanged;
            document.CurrentPageAnnotationsChanged += OnAnnotationsChanged;
        }
    }

    /// <summary>
    /// Beobachtet Werkzeugwechsel und Text-Einstellungen.
    /// </summary>
    private void SubscribeOptions(ToolOptionsViewModel? options)
    {
        if (_subscribedOptions is not null)
        {
            _subscribedOptions.PropertyChanged -= OnOptionsPropertyChanged;
        }

        _subscribedOptions = options;
        if (options is not null)
        {
            options.PropertyChanged += OnOptionsPropertyChanged;
        }
    }

    /// <summary>
    /// Werkzeugwechsel schließt die Textbearbeitung ab; Farbe und Größe gelten für den gerade bearbeiteten Text.
    /// </summary>
    private void OnOptionsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (Options is not { } options)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(ToolOptionsViewModel.Tool) when options.Tool != Tool.Text:
                CommitText();
                break;
            case nameof(ToolOptionsViewModel.TextColor) or nameof(ToolOptionsViewModel.TextSize) when _editing is { } text:
                _editing = text with { ColorHex = options.TextColor, FontSize = TextAnnotation.ClampFontSize(options.TextSize) };
                ApplyTextStyle();
                InvalidateVisual();
                break;
        }
    }

    /// <summary>
    /// Neue Bitmap zeichnen, bei Seitenwechsel neu messen und rendern.
    /// </summary>
    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.CurrentBitmap))
        {
            InvalidateVisual();
        }
        else if (e.PropertyName == nameof(DocumentViewModel.CurrentIndex))
        {
            CommitText();
            InvalidateMeasure();
            RequestRender();
        }
    }

    /// <summary>
    /// Geänderte Annotationen oder Seiten (z. B. Drehung) neu zeichnen.
    /// </summary>
    private void OnAnnotationsChanged(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        InvalidateVisual();
        RequestRender();
    }

    /// <summary>Anzeigepunkt zu einem Steuerelement-Punkt.</summary>
    private static PdfPoint ToPoint(Point point) => new(point.X, point.Y);

    /// <summary>Avalonia-Rechteck.</summary>
    private static Rect ToRect(PdfRect rect) => new(rect.X, rect.Y, rect.Width, rect.Height);

    /// <summary>Anzeigepunkt → Punkt auf der ungedrehten Seite.</summary>
    private PdfPoint ToPage(PdfPoint display)
    {
        var (width, height) = Document!.CurrentPageSize;
        return PageTransform.Unrotate(display, Document.CurrentPage.Rotation, width, height);
    }

    /// <summary>Anzeige-Rechteck → Rechteck auf der ungedrehten Seite.</summary>
    private PdfRect ToPage(PdfRect display) =>
        PdfRect.FromCorners(ToPage(new PdfPoint(display.X, display.Y)), ToPage(new PdfPoint(display.Right, display.Bottom)));

    /// <summary>Annotation der ungedrehten Seite → Anzeige.</summary>
    private Annotation ToDisplay(Annotation annotation)
    {
        var (width, height) = Document!.CurrentPageSize;
        return PageTransform.Rotate(annotation, Document.CurrentPage.Rotation, width, height);
    }

    /// <summary>Vergrößert ein Rechteck auf allen Seiten.</summary>
    private static PdfRect Inflate(PdfRect rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + 2 * amount, rect.Height + 2 * amount);

    /// <summary>Ob ein Punkt im Rechteck liegt.</summary>
    private static bool Contains(PdfRect rect, PdfPoint point) =>
        point.X >= rect.X && point.X <= rect.Right && point.Y >= rect.Y && point.Y <= rect.Bottom;

    /// <summary>Kurvenzug aus Bézier-Stücken.</summary>
    private static StreamGeometry Curve(IReadOnlyList<BezierSegment> segments)
    {
        var geometry = new StreamGeometry();
        if (segments.Count == 0)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(new Point(segments[0].Start.X, segments[0].Start.Y), false);
        foreach (var segment in segments)
        {
            context.CubicBezierTo(
                new Point(segment.Control1.X, segment.Control1.Y), new Point(segment.Control2.X, segment.Control2.Y), new Point(segment.End.X, segment.End.Y));
        }

        context.EndFigure(false);
        return geometry;
    }

    /// <summary>Stift mit runden Enden.</summary>
    private static Pen RoundPen(IBrush brush, double width) => new(brush, width, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);

    /// <summary>Pinsel aus Hex-Farbe.</summary>
    private static SolidColorBrush Brush(string hex) => new(Color.Parse(hex));

    /// <summary>Farbe mit Deckkraft.</summary>
    private static Color WithAlpha(string hex, byte alpha)
    {
        var color = Color.Parse(hex);
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
