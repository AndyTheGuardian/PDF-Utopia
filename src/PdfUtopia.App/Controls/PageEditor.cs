using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PdfUtopia.App.Localization;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Theming;
using BezierSegment = PdfUtopia.Core.Drawing.BezierSegment;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Zeigt die aktuelle Seite mit ihren Annotationen und nimmt Eingaben für Marker, Freihand, Rechteck,
/// Kommentar und Radierer entgegen. Die Größe entspricht der angezeigten Seite in Punkten; der Zoom wird von
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

    private readonly StrokeRecorder _recorder = new();
    private readonly List<PdfPoint> _eraserPath = [];
    private readonly HashSet<Guid> _eraserHits = [];
    private Tool? _gesture;
    private PdfPoint _start;
    private PdfPoint _current;
    private PdfPoint? _pointer;
    private Guid? _hoveredBadge;
    private PdfRect? _suggestedParagraph;
    private DocumentViewModel? _subscribed;

    static PageEditor()
    {
        AffectsMeasure<PageEditor>(DocumentProperty);
        AffectsRender<PageEditor>(DocumentProperty, OptionsProperty);
        FocusableProperty.OverrideDefaultValue<PageEditor>(true);
    }

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

        var (width, height) = Document.CurrentDisplaySize;
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty)
        {
            Subscribe(Document);
            RequestRender();
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

        foreach (var annotation in document.CurrentAnnotations)
        {
            var display = ToDisplay(annotation);
            using (context.PushOpacity(_eraserHits.Contains(annotation.Id) ? 0.2 : 1))
            {
                DrawAnnotation(context, display);
            }
        }

        DrawSuggestion(context);
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
            case Tool.Rectangle when IsDrag():
                document.AddAnnotation(new RectangleAnnotation(Guid.NewGuid(), options.RectangleColor, options.RectangleWidth, ToPage(DragRect())));
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
        }

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

        var erasing = eraserEnd || Options?.Tool == Tool.Eraser;
        Cursor = _hoveredBadge is not null ? new Cursor(StandardCursorType.Hand)
            : erasing ? new Cursor(StandardCursorType.None)
            : Options?.Tool is Tool.Select or null ? Cursor.Default
            : new Cursor(StandardCursorType.Cross);
        if (previous != _hoveredBadge || erasing)
        {
            InvalidateVisual();
        }
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
                context.DrawRectangle(null, new Pen(Brush(rectangle.ColorHex), rectangle.Width), ToRect(rectangle.Bounds));
                break;
            case CommentAnnotation comment:
                DrawComment(context, comment);
                break;
        }
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
        context.DrawRectangle(new SolidColorBrush(WithAlpha(comment.ColorHex, CommentFillAlpha)), new Pen(Brush(border), CommentBorderWidth), rect, 4, 4);

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
    /// Gestrichelter Rahmen um den Absatz, den ein Klick kommentieren würde.
    /// </summary>
    private void DrawSuggestion(DrawingContext context)
    {
        if (_gesture is not null || _suggestedParagraph is not { } paragraph)
        {
            return;
        }

        var (width, height) = Document!.CurrentPageSize;
        var display = PageTransform.Rotate(paragraph, Document.CurrentPage.Rotation, width, height);
        context.DrawRectangle(null, new Pen(new SolidColorBrush(Color.Parse("#A5B4FC")), 2, new DashStyle([3, 2], 0)), ToRect(display).Inflate(2), 4, 4);
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
            case Tool.Rectangle:
                context.DrawRectangle(null, new Pen(Brush(options.RectangleColor), options.RectangleWidth), ToRect(DragRect()));
                break;
            case Tool.Comment:
                context.DrawRectangle(new SolidColorBrush(WithAlpha(options.CommentColor, CommentFillAlpha)),
                    new Pen(Brush(DrawingColors.CommentBorder(options.CommentColor)), CommentBorderWidth, new DashStyle([4, 2], 0)), ToRect(DragRect()), 4, 4);
                break;
        }
    }

    /// <summary>
    /// Gestrichelter Kreis in Radiergröße am Mauszeiger.
    /// </summary>
    private void DrawEraserCursor(DrawingContext context)
    {
        var erasing = _gesture == Tool.Eraser || (_gesture is null && Options?.Tool == Tool.Eraser);
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
