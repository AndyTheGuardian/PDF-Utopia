using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Annotations;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Drawing;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Schreibt die Annotationen einer Seite als echte PDF-Annotationen (P5): Freihand als <c>/Ink</c>,
/// Marker als <c>/Highlight</c>, Rechteck und Kommentar als <c>/Square</c>, Fußnoten als <c>/FreeText</c>
/// auf der verlängerten Seite. Jede Annotation erhält ein eigenes Erscheinungsbild, damit sie in allen
/// PDF-Betrachtern genauso aussieht wie in PDF Utopia.
/// </summary>
/// <param name="document">Das Zieldokument.</param>
/// <param name="options">Exporteinstellungen.</param>
internal sealed class AnnotationWriter(PdfDocument document, ExportOptions options)
{
    /// <summary>Kennzeichnung eigener Annotationen (für späteres Wiedereinlesen).</summary>
    public const string OwnerKey = "/PdfUtopia";

    /// <summary>Daten einer eigenen Annotation als JSON (für späteres Bearbeiten, F23).</summary>
    public const string DataKey = "/PdfUtopiaData";

    /// <summary>Seiteneintrag: Verlängerung für Fußnoten und die Dateidrehung, auf die sie sich bezieht.</summary>
    public const string ExtensionKey = "/PdfUtopiaExtension";

    private const double CommentFillOpacity = 0.14;
    private const double MarkerOpacity = 0.45;
    private const double CommentBorderWidth = 1.5;
    private const double BadgeRadius = 7;
    private const double BadgeFontSize = 7.5;
    private const double FootnoteBadgeRadius = 5.5;
    private const string FootnoteTextColor = "#333333";
    private const string SeparatorColor = "#C9CDD8";
    private const string HeadingColor = "#9AA0AB";
    private const int PrintFlag = 4;

    private readonly PdfReference _regularFont = CreateFont(document, "/Helvetica");
    private readonly PdfReference _boldFont = CreateFont(document, "/Helvetica-Bold");

    /// <summary>
    /// Schreibt alle Annotationen einer Seite.
    /// </summary>
    /// <param name="page">Die Seite im Zieldokument.</param>
    /// <param name="geometry">Geometrie der Seite.</param>
    /// <param name="annotations">Annotationen in Zeichenreihenfolge.</param>
    /// <param name="rotation">Drehung, mit der die Seite gespeichert wird; die mitgeschriebenen Daten werden
    /// entsprechend gedreht, damit sie nach dem Wiederöffnen zur angezeigten Seite passen.</param>
    /// <returns>Benötigte Verlängerung der Seite für Fußnoten (0 ohne Kommentare).</returns>
    public double Write(PdfPage page, PageGeometry geometry, IReadOnlyList<Annotation> annotations, PageRotation rotation)
    {
        var comments = annotations.OfType<CommentAnnotation>().ToList();
        foreach (var annotation in annotations)
        {
            var dictionary = annotation switch
            {
                InkAnnotation ink => Ink(geometry, ink),
                HighlightAnnotation highlight => Highlight(geometry, highlight),
                RectangleAnnotation rectangle => Rectangle(geometry, rectangle),
                CommentAnnotation comment => Comment(geometry, comment, comments.IndexOf(comment) + 1),
                TextAnnotation text => Text(geometry, text),
                LineAnnotation line => Line(geometry, line),
                EllipseAnnotation ellipse => Ellipse(geometry, ellipse),
                _ => throw new NotSupportedException($"Unbekannte Annotation {annotation.GetType().Name}."),
            };
            dictionary.Elements.SetString("/NM", annotation.Id.ToString());
            var saved = PageTransform.Rotate(annotation, rotation, geometry.DisplayWidth, geometry.DisplayHeight);
            dictionary.Elements[DataKey] = new PdfString(AnnotationSerializer.Serialize(saved), PdfStringEncoding.Unicode);
            page.Annotations.Add(dictionary);
        }

        var block = FootnoteLayout.Layout(comments, geometry.DisplayWidth, geometry.DisplayHeight);
        if (block.Notes.Count > 0)
        {
            page.Annotations.Add(Heading(geometry, block.Heading));
            foreach (var note in block.Notes)
            {
                page.Annotations.Add(Footnote(geometry, note));
            }
        }

        return block.Extension;
    }

    /// <summary>
    /// Freihandstrich mit Glättung und ggf. druckabhängiger Stärke.
    /// </summary>
    private RawAnnotation Ink(PageGeometry geometry, InkAnnotation ink)
    {
        var segments = StrokeGeometry.ToSegments(ink);
        var maxWidth = segments.Count == 0 ? ink.Width : segments.Max(segment => segment.Width);
        var bounds = Inflate(BoundsOf(segments), maxWidth / 2 + 1);
        var content = new ContentBuilder().RoundCaps().StrokeColor(ink.ColorHex);
        if (ink.PressureSensitive)
        {
            foreach (var segment in segments)
            {
                content.LineWidth(segment.Width);
                DrawSegment(content, bounds, segment, moveFirst: true).Stroke();
            }
        }
        else
        {
            content.LineWidth(ink.Width);
            for (var i = 0; i < segments.Count; i++)
            {
                DrawSegment(content, bounds, segments[i], moveFirst: i == 0);
            }

            content.Stroke();
        }

        var annotation = Create("/Ink", geometry, bounds, ink.ColorHex, Appearance(geometry, bounds, content));
        var inkList = new PdfArray(document);
        inkList.Elements.Add(Numbers(ink.Points.SelectMany(point => Coordinates(geometry.ToUser(point.Position)))));
        annotation.Elements["/InkList"] = inkList;
        annotation.Elements["/BS"] = BorderStyle(ink.Width);
        return annotation;
    }

    /// <summary>
    /// Textmarker als halbtransparente Fläche.
    /// </summary>
    private RawAnnotation Highlight(PageGeometry geometry, HighlightAnnotation highlight)
    {
        var content = new ContentBuilder().Save().GraphicsState("/GSm").FillColor(highlight.ColorHex)
            .Rectangle(0, 0, highlight.Bounds.Width, highlight.Bounds.Height).Fill().Restore();
        var states = new Dictionary<string, PdfDictionary> { ["/GSm"] = Opacity(MarkerOpacity, multiply: true) };
        var annotation = Create("/Highlight", geometry, highlight.Bounds, highlight.ColorHex, Appearance(geometry, highlight.Bounds, content, states));
        annotation.Elements["/QuadPoints"] = Numbers(geometry.QuadPoints(highlight.Bounds));
        annotation.Elements.SetReal("/CA", MarkerOpacity);
        return annotation;
    }

    /// <summary>
    /// Rechteck um ein Objekt.
    /// </summary>
    private RawAnnotation Rectangle(PageGeometry geometry, RectangleAnnotation rectangle)
    {
        var bounds = Inflate(rectangle.Bounds, rectangle.Width / 2);
        var content = new ContentBuilder().StrokeColor(rectangle.ColorHex).LineWidth(rectangle.Width)
            .RoundedRectangle(rectangle.Width / 2, rectangle.Width / 2, rectangle.Bounds.Width, rectangle.Bounds.Height,
                AnnotationShape.CornerRadiusFor(rectangle.Bounds.Width, rectangle.Bounds.Height)).Stroke();
        var annotation = Create("/Square", geometry, bounds, rectangle.ColorHex, Appearance(geometry, bounds, content));
        annotation.Elements["/BS"] = BorderStyle(rectangle.Width);
        annotation.Elements["/RD"] = Numbers(Enumerable.Repeat(rectangle.Width / 2, 4));
        return annotation;
    }

    /// <summary>
    /// Linie, optional mit offenen oder geschlossenen Pfeilspitzen an einem oder beiden Enden (A44), als Standard-Annotation „Line“.
    /// </summary>
    private RawAnnotation Line(PageGeometry geometry, LineAnnotation line)
    {
        var bounds = Inflate(line.Bounds, line.Width / 2 + 1);
        var content = new ContentBuilder().RoundCaps().StrokeColor(line.ColorHex).LineWidth(line.Width);
        StrokeLine(content, bounds, line.StrokeStart, line.StrokeEnd);
        foreach (var head in line.Heads)
        {
            var tip = Local(bounds, head.Tip);
            var (l, r) = (Local(bounds, head.Left), Local(bounds, head.Right));
            content.MoveTo(l.X, l.Y).LineTo(tip.X, tip.Y).LineTo(r.X, r.Y);
            if (line.Arrow == ArrowStyle.Closed)
            {
                content.FillColor(line.ColorHex).ClosePath().FillAndStroke();
            }
            else
            {
                content.Stroke();
            }
        }

        var annotation = Create("/Line", geometry, bounds, line.ColorHex, Appearance(geometry, bounds, content));
        annotation.Elements["/L"] = Numbers([.. Coordinates(geometry.ToUser(line.Start)), .. Coordinates(geometry.ToUser(line.End))]);
        var ending = line.Arrow == ArrowStyle.Closed ? "/ClosedArrow" : "/OpenArrow";
        var endings = new PdfArray(document);
        endings.Elements.Add(new PdfName(line.HasStartArrow ? ending : "/None"));
        endings.Elements.Add(new PdfName(line.HasEndArrow ? ending : "/None"));
        annotation.Elements["/LE"] = endings;
        if (line.Arrow == ArrowStyle.Closed)
        {
            annotation.Elements["/IC"] = Numbers(Color(line.ColorHex));
        }
        annotation.Elements["/BS"] = BorderStyle(line.Width);
        return annotation;
    }

    /// <summary>
    /// Ellipse (A44) als Standard-Annotation „Circle“.
    /// </summary>
    private RawAnnotation Ellipse(PageGeometry geometry, EllipseAnnotation ellipse)
    {
        var bounds = Inflate(ellipse.Bounds, ellipse.Width / 2);
        var content = new ContentBuilder().StrokeColor(ellipse.ColorHex).LineWidth(ellipse.Width)
            .Ellipse(bounds.Width / 2, bounds.Height / 2, ellipse.Bounds.Width / 2, ellipse.Bounds.Height / 2).Stroke();
        var annotation = Create("/Circle", geometry, bounds, ellipse.ColorHex, Appearance(geometry, bounds, content));
        annotation.Elements["/BS"] = BorderStyle(ellipse.Width);
        annotation.Elements["/RD"] = Numbers(Enumerable.Repeat(ellipse.Width / 2, 4));
        return annotation;
    }

    /// <summary>
    /// Gerade Strecke in lokalen Koordinaten des Erscheinungsbildes.
    /// </summary>
    private static void StrokeLine(ContentBuilder content, PdfRect bounds, PdfPoint start, PdfPoint end)
    {
        var (from, to) = (Local(bounds, start), Local(bounds, end));
        content.MoveTo(from.X, from.Y).LineTo(to.X, to.Y).Stroke();
    }

    /// <summary>
    /// Kommentierter Abschnitt: farbig umrandet und hinterlegt, Fußnotenzahl oben rechts.
    /// </summary>
    private RawAnnotation Comment(PageGeometry geometry, CommentAnnotation comment, int number)
    {
        var padding = BadgeRadius + 1;
        var bounds = Inflate(comment.Bounds, padding);
        var border = DrawingColors.CommentBorder(comment.ColorHex);
        var (width, height) = (comment.Bounds.Width, comment.Bounds.Height);
        var radius = AnnotationShape.CornerRadiusFor(width, height);
        var content = new ContentBuilder()
            .Save().GraphicsState("/GSf").FillColor(comment.ColorHex).RoundedRectangle(padding, padding, width, height, radius).Fill().Restore()
            .StrokeColor(border).LineWidth(CommentBorderWidth).RoundedRectangle(padding, padding, width, height, radius).Stroke();
        DrawNumberBadge(content, padding + width, padding + height, BadgeRadius, BadgeFontSize, number, comment.ColorHex, border);

        var states = new Dictionary<string, PdfDictionary> { ["/GSf"] = Opacity(CommentFillOpacity) };
        var annotation = Create("/Square", geometry, bounds, border, Appearance(geometry, bounds, content, states, withFonts: true));
        annotation.Elements["/IC"] = Numbers(Color(comment.ColorHex));
        annotation.Elements["/BS"] = BorderStyle(CommentBorderWidth);
        annotation.Elements["/RD"] = Numbers(Enumerable.Repeat(padding, 4));
        annotation.Elements["/Contents"] = new PdfString(comment.Text, PdfStringEncoding.Unicode);
        return annotation;
    }

    /// <summary>
    /// Freier Text (A43) als Standard-Annotation „FreeText“ mit Erscheinungsbild in Helvetica. Steht der Text nicht
    /// aufrecht zur Seite, dreht eine Transformation die Zeilen in die Leserichtung.
    /// </summary>
    private RawAnnotation Text(PageGeometry geometry, TextAnnotation text)
    {
        var bounds = text.Bounds;
        var (across, along) = TextAnnotation.Axes(text.Orientation);
        var origin = Local(bounds, text.Anchor);
        // Textraum: x in Leserichtung, y nach oben (gegen die Zeilenfolge); lokal ist y nach oben gerichtet
        var content = new ContentBuilder().Save()
            .Transform(across.X, -across.Y, -along.X, along.Y, origin.X, origin.Y)
            .FillColor(text.ColorHex);
        var lines = text.Lines;
        for (var i = 0; i < lines.Count; i++)
        {
            content.Text(0, -text.Baseline(i), lines[i], text.FontSize);
        }

        content.Restore();
        var annotation = Create("/FreeText", geometry, bounds, text.ColorHex, Appearance(geometry, bounds, content, withFonts: true));
        var (r, g, b) = ContentBuilder.Components(text.ColorHex);
        annotation.Elements.SetString("/DA", $"/Helv {ContentBuilder.N(text.FontSize)} Tf {ContentBuilder.N(r)} {ContentBuilder.N(g)} {ContentBuilder.N(b)} rg");
        annotation.Elements["/Contents"] = new PdfString(text.Text, PdfStringEncoding.Unicode);
        annotation.Elements["/BS"] = BorderStyle(0);
        return annotation;
    }

    /// <summary>
    /// Überschrift des Fußnotenbereichs mit gestrichelter Trennlinie.
    /// </summary>
    private RawAnnotation Heading(PageGeometry geometry, PdfRect bounds)
    {
        var content = new ContentBuilder()
            .StrokeColor(SeparatorColor).LineWidth(1).Dash(3, 2).MoveTo(0, bounds.Height - 1).LineTo(bounds.Width, bounds.Height - 1).Stroke()
            .FillColor(HeadingColor).Text(0, bounds.Height - 15, options.FootnoteHeading.ToUpperInvariant(), 6.5, bold: true);
        var annotation = Create("/FreeText", geometry, bounds, HeadingColor, Appearance(geometry, bounds, content, withFonts: true));
        annotation.Elements.SetString("/DA", "/Helv 6.5 Tf 0.6 g");
        annotation.Elements["/Contents"] = new PdfString(options.FootnoteHeading, PdfStringEncoding.Unicode);
        return annotation;
    }

    /// <summary>
    /// Fußnote mit Nummer und umbrochenem Text.
    /// </summary>
    private RawAnnotation Footnote(PageGeometry geometry, PlacedFootnote note)
    {
        var bounds = note.Bounds;
        var border = DrawingColors.CommentBorder(note.Comment.ColorHex);
        var content = new ContentBuilder();
        var firstLineCenter = bounds.Height - FootnoteLayout.LineHeight / 2;
        DrawNumberBadge(content, FootnoteBadgeRadius + 0.5, firstLineCenter, FootnoteBadgeRadius, 6.5, note.Number, note.Comment.ColorHex, border);
        content.FillColor(FootnoteTextColor);
        for (var i = 0; i < note.Lines.Count; i++)
        {
            var baseline = bounds.Height - (i + 1) * FootnoteLayout.LineHeight + 3;
            content.Text(FootnoteLayout.NumberColumn, baseline, note.Lines[i], FootnoteLayout.FontSize);
        }

        var annotation = Create("/FreeText", geometry, bounds, note.Comment.ColorHex, Appearance(geometry, bounds, content, withFonts: true));
        annotation.Elements.SetString("/DA", $"/Helv {ContentBuilder.N(FootnoteLayout.FontSize)} Tf 0.2 g");
        annotation.Elements["/Contents"] = new PdfString($"{note.Number} {note.Comment.Text}", PdfStringEncoding.Unicode);
        return annotation;
    }

    /// <summary>
    /// Kreis in Kommentarfarbe mit lesbarer Zahl; bei zu hellen Farben grau umrandet.
    /// </summary>
    private static void DrawNumberBadge(ContentBuilder content, double centerX, double centerY, double radius, double fontSize, int number, string colorHex, string borderHex)
    {
        content.FillColor(colorHex).StrokeColor(borderHex).LineWidth(borderHex == colorHex ? 0 : 1).Circle(centerX, centerY, radius);
        if (borderHex == colorHex)
        {
            content.Fill();
        }
        else
        {
            content.FillAndStroke();
        }

        var label = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var textWidth = StandardFont.Width(label, fontSize, bold: true);
        content.FillColor(DrawingColors.FootnoteNumberForeground(colorHex))
            .Text(centerX - textWidth / 2, centerY - fontSize * 0.35, label, fontSize, bold: true);
    }

    /// <summary>
    /// Zeichnet ein Bézier-Stück in lokalen Koordinaten des Erscheinungsbildes.
    /// </summary>
    private static ContentBuilder DrawSegment(ContentBuilder content, PdfRect bounds, BezierSegment segment, bool moveFirst)
    {
        if (moveFirst)
        {
            var start = Local(bounds, segment.Start);
            content.MoveTo(start.X, start.Y);
        }

        return content.CurveTo(Local(bounds, segment.Control1), Local(bounds, segment.Control2), Local(bounds, segment.End));
    }

    /// <summary>
    /// Legt die Annotation mit gemeinsamen Einträgen an.
    /// </summary>
    private RawAnnotation Create(string subtype, PageGeometry geometry, PdfRect displayBounds, string colorHex, PdfReference appearance)
    {
        var annotation = new RawAnnotation(document);
        annotation.Elements.SetName("/Subtype", subtype);
        var (left, bottom, right, top) = geometry.ToUser(displayBounds);
        annotation.Elements["/Rect"] = Numbers([left, bottom, right, top]);
        annotation.Elements["/C"] = Numbers(Color(colorHex));
        annotation.Elements.SetInteger("/F", PrintFlag);
        annotation.Elements.SetBoolean(OwnerKey, true);
        var appearanceDictionary = new PdfDictionary(document);
        appearanceDictionary.Elements["/N"] = appearance;
        annotation.Elements["/AP"] = appearanceDictionary;
        return annotation;
    }

    /// <summary>
    /// Erzeugt das Erscheinungsbild als Form-XObject in Anzeige-Ausrichtung.
    /// </summary>
    private PdfReference Appearance(
        PageGeometry geometry, PdfRect displayBounds, ContentBuilder content,
        IReadOnlyDictionary<string, PdfDictionary>? graphicsStates = null, bool withFonts = false)
    {
        var form = new PdfDictionary(document);
        form.Elements.SetName("/Type", "/XObject");
        form.Elements.SetName("/Subtype", "/Form");
        form.Elements["/BBox"] = Numbers([0, 0, displayBounds.Width, displayBounds.Height]);
        form.Elements["/Matrix"] = Numbers(geometry.AppearanceMatrix());
        form.Elements["/Resources"] = Resources(graphicsStates, withFonts);
        form.CreateStream(content.ToBytes());
        document.Internals.AddObject(form);
        return form.Reference!;
    }

    /// <summary>
    /// Ressourcen eines Erscheinungsbildes: Grafikzustände und Schriften.
    /// </summary>
    private PdfDictionary Resources(IReadOnlyDictionary<string, PdfDictionary>? graphicsStates, bool withFonts)
    {
        var resources = new PdfDictionary(document);
        if (graphicsStates is { Count: > 0 })
        {
            var states = new PdfDictionary(document);
            foreach (var (name, state) in graphicsStates)
            {
                states.Elements[name] = state;
            }

            resources.Elements["/ExtGState"] = states;
        }

        if (withFonts)
        {
            var fonts = new PdfDictionary(document);
            fonts.Elements[StandardFont.Regular] = _regularFont;
            fonts.Elements[StandardFont.Bold] = _boldFont;
            resources.Elements["/Font"] = fonts;
        }

        return resources;
    }

    /// <summary>
    /// Grafikzustand mit Deckkraft für Flächen, optional Mischmodus „Multiplizieren“ wie ein Textmarker.
    /// </summary>
    private PdfDictionary Opacity(double fillOpacity, bool multiply = false)
    {
        var state = new PdfDictionary(document);
        state.Elements.SetName("/Type", "/ExtGState");
        state.Elements.SetReal("/ca", fillOpacity);
        if (multiply)
        {
            state.Elements.SetName("/BM", "/Multiply");
        }

        return state;
    }

    /// <summary>
    /// Randstil mit Strichstärke.
    /// </summary>
    private PdfDictionary BorderStyle(double width)
    {
        var style = new PdfDictionary(document);
        style.Elements.SetReal("/W", width);
        style.Elements.SetName("/S", "/S");
        return style;
    }

    /// <summary>
    /// Zahlenfeld.
    /// </summary>
    private PdfArray Numbers(IEnumerable<double> values)
    {
        var array = new PdfArray(document);
        foreach (var value in values)
        {
            array.Elements.Add(new PdfReal(Math.Round(value, 3)));
        }

        return array;
    }

    /// <summary>
    /// Standardschrift als Schriftobjekt (nicht eingebettet, WinAnsi).
    /// </summary>
    private static PdfReference CreateFont(PdfDocument document, string baseFont)
    {
        var font = new PdfDictionary(document);
        font.Elements.SetName("/Type", "/Font");
        font.Elements.SetName("/Subtype", "/Type1");
        font.Elements.SetName("/BaseFont", baseFont);
        font.Elements.SetName("/Encoding", "/WinAnsiEncoding");
        document.Internals.AddObject(font);
        return font.Reference!;
    }

    /// <summary>
    /// Farbe als Zahlen 0–1.
    /// </summary>
    private static double[] Color(string hex)
    {
        var (r, g, b) = ContentBuilder.Components(hex);
        return [r, g, b];
    }

    /// <summary>
    /// Punkt als zwei Zahlen.
    /// </summary>
    private static double[] Coordinates(PdfPoint point) => [point.X, point.Y];

    /// <summary>
    /// Punkt in lokalen Koordinaten des Erscheinungsbildes (Ursprung links unten des Bereichs, Y nach oben).
    /// </summary>
    private static PdfPoint Local(PdfRect bounds, PdfPoint display) => new(display.X - bounds.X, bounds.Bottom - display.Y);

    /// <summary>
    /// Vergrößert ein Rechteck auf allen Seiten.
    /// </summary>
    private static PdfRect Inflate(PdfRect rect, double amount) =>
        new(rect.X - amount, rect.Y - amount, rect.Width + 2 * amount, rect.Height + 2 * amount);

    /// <summary>
    /// Umschließendes Rechteck aller Punkte und Kontrollpunkte.
    /// </summary>
    private static PdfRect BoundsOf(IReadOnlyList<BezierSegment> segments)
    {
        var points = segments.SelectMany(segment => new[] { segment.Start, segment.Control1, segment.Control2, segment.End }).ToList();
        var first = points.Count > 0 ? points[0] : new PdfPoint(0, 0);
        var (minX, minY, maxX, maxY) = points.Aggregate((first.X, first.Y, first.X, first.Y),
            (box, point) => (Math.Min(box.Item1, point.X), Math.Min(box.Item2, point.Y), Math.Max(box.Item3, point.X), Math.Max(box.Item4, point.Y)));
        return new PdfRect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Annotation mit frei belegbaren Einträgen.
    /// </summary>
    private sealed class RawAnnotation(PdfDocument document) : PdfAnnotation(document);
}
