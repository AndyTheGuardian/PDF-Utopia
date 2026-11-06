using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using PdfUtopia.App.Services;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Werkzeug „Stempel“ (A51, Mockup v7): Stempel und Bilder setzen, anklicken, verschieben, über die Eckgriffe in der
/// Größe ändern und über den Drehgriff drehen (F37). Entf löscht die Auswahl, Esc hebt sie auf. Bilddateien lassen
/// sich auf die Seite ziehen.
/// </summary>
public sealed partial class PageEditor
{
    /// <summary>Kantenlänge der Eckgriffe in Bildschirmpixeln.</summary>
    private const double HandleSize = 8;

    /// <summary>Abstand des Drehgriffs über der Oberkante in Bildschirmpixeln.</summary>
    private const double RotateHandleDistance = 24;

    /// <summary>Radius des Drehgriffs in Bildschirmpixeln.</summary>
    private const double RotateHandleRadius = 5;

    private BoxGesture _boxGesture;
    private int _boxCorner;
    private BoxAnnotation? _boxOriginal;
    private BoxAnnotation? _boxPreview;
    private BoxHit _boxHover;

    /// <summary>Was ein Zeiger beim Werkzeug „Stempel“ verändert.</summary>
    private enum BoxGesture
    {
        None,
        Place,
        Move,
        Resize,
        Rotate,
    }

    /// <summary>Was unter dem Zeiger liegt.</summary>
    private enum BoxHit
    {
        None,
        Box,
        Corner,
        RotateHandle,
    }

    /// <summary>Angeklickter Stempel bzw. angeklicktes Bild der Seite (Anzeige-Koordinaten) oder <c>null</c>.</summary>
    private BoxAnnotation? SelectedBoxDisplay =>
        Options?.Tool == Tool.Stamp && Document is { SelectedBox: { } id } document
            && Annotations.FirstOrDefault(annotation => annotation.Id == id) is BoxAnnotation box
            ? (BoxAnnotation)ToDisplay(box)
            : null;

    /// <summary>
    /// Richtet das Ablegen von Bilddateien auf der Seite ein.
    /// </summary>
    private void InitializeImageDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnImageDragOver);
        AddHandler(DragDrop.DropEvent, OnImageDrop);
    }

    /// <summary>
    /// Beginnt eine Eingabe mit dem Werkzeug „Stempel“: Griff, angeklickte Anmerkung oder neuer Stempel bzw. neues Bild.
    /// </summary>
    private void BeginBox(PdfPoint display)
    {
        Focus();
        _boxPreview = null;
        var (hit, corner) = HitBox(display);
        var document = Document!;
        if (hit is BoxHit.Corner or BoxHit.RotateHandle)
        {
            _boxOriginal = SelectedBoxDisplay;
            _boxCorner = corner;
            _boxGesture = hit == BoxHit.Corner ? BoxGesture.Resize : BoxGesture.Rotate;
            return;
        }

        if (Options!.PendingImage is null && BoxAt(display) is { } box)
        {
            document.SelectedBox = box.Id;
            _boxOriginal = (BoxAnnotation)ToDisplay(box);
            _boxGesture = BoxGesture.Move;
            return;
        }

        document.SelectedBox = null;
        _boxOriginal = null;
        _boxGesture = BoxGesture.Place;
    }

    /// <summary>
    /// Aktualisiert die Vorschau während des Ziehens.
    /// </summary>
    private void ContinueBox()
    {
        _boxPreview = _boxGesture switch
        {
            BoxGesture.Move => _boxOriginal!.Translate(_current.X - _start.X, _current.Y - _start.Y),
            BoxGesture.Resize => _boxOriginal!.ResizeFrom(_boxCorner, _current),
            BoxGesture.Rotate => _boxOriginal! with { Angle = _boxOriginal.AngleTowards(_current, free: _constrain) },
            BoxGesture.Place when Math.Abs(_current.X - _start.X) >= MinDragDistance => DraggedBox(),
            _ => null,
        };
    }

    /// <summary>
    /// Schließt eine Eingabe ab: Änderung übernehmen oder neuen Stempel bzw. neues Bild setzen und anwählen.
    /// </summary>
    private void FinishBox(DocumentViewModel document, ToolOptionsViewModel options)
    {
        ContinueBox();
        var (gesture, preview) = (_boxGesture, _boxPreview);
        _boxGesture = BoxGesture.None;
        _boxPreview = null;
        _boxOriginal = null;
        switch (gesture)
        {
            case BoxGesture.Move or BoxGesture.Resize or BoxGesture.Rotate when preview is not null && _current.DistanceTo(_start) >= 0.5:
                document.Document.UpdateAnnotation(PageId, Page!.ToPage(preview));
                break;
            case BoxGesture.Place:
                var placed = Page!.PlaceBox(preview ?? NewBox(_start, DefaultBoxWidth(options)));
                options.PendingImage = null;
                document.SelectedBox = placed.Id;
                break;
        }

        // Der Zeiger steht jetzt auf der Anmerkung: kein Abbild eines neuen Stempels darüber zeichnen
        _boxHover = HitBox(_current).Hit;
    }

    /// <summary>
    /// Neuer Stempel bzw. neues Bild mit der Mitte an einem Punkt der Anzeige.
    /// </summary>
    private BoxAnnotation NewBox(PdfPoint center, double width) =>
        Options!.PendingImage is { } pending
            ? ImageAnnotation.Create(pending.Image, center, width)
            : Options.CreateStamp(center, width, 0);

    /// <summary>Breite eines per Klick gesetzten Stempels bzw. Bildes.</summary>
    private static double DefaultBoxWidth(ToolOptionsViewModel options) =>
        options.PendingImage is null ? StampAnnotation.DefaultWidth : ImageAnnotation.DefaultWidth;

    /// <summary>
    /// Aufgezogener neuer Stempel bzw. neues Bild: die Breite folgt dem Zeiger, die linke obere Ecke ist der Startpunkt.
    /// </summary>
    private BoxAnnotation DraggedBox()
    {
        var width = Math.Max(BoxAnnotation.MinWidth, Math.Abs(_current.X - _start.X));
        var template = NewBox(default, width);
        var left = Math.Min(_start.X, _current.X);
        return template with { Bounds = new PdfRect(left, _start.Y, template.Bounds.Width, template.Bounds.Height) };
    }

    /// <summary>
    /// Oberster Stempel bzw. oberstes Bild an einer Anzeigeposition.
    /// </summary>
    private BoxAnnotation? BoxAt(PdfPoint display) =>
        Annotations.OfType<BoxAnnotation>().LastOrDefault(box => ((BoxAnnotation)ToDisplay(box)).Contains(display, TextHitMargin));

    /// <summary>
    /// Was unter dem Zeiger liegt: Griffe der Auswahl, eine Anmerkung oder nichts.
    /// </summary>
    /// <returns>Die Art und bei einer Ecke ihr Index.</returns>
    private (BoxHit Hit, int Corner) HitBox(PdfPoint display)
    {
        var scale = 1 / Math.Max(Zoom, 0.01);
        if (SelectedBoxDisplay is { } selected)
        {
            if (RotateHandle(selected, scale).DistanceTo(display) <= (RotateHandleRadius + 3) * scale)
            {
                return (BoxHit.RotateHandle, 0);
            }

            var corners = selected.Corners;
            for (var i = 0; i < corners.Count; i++)
            {
                if (corners[i].DistanceTo(display) <= HandleSize * scale)
                {
                    return (BoxHit.Corner, i);
                }
            }
        }

        return (Options?.PendingImage is null && BoxAt(display) is not null ? BoxHit.Box : BoxHit.None, 0);
    }

    /// <summary>Mitte des Drehgriffs über der Oberkante.</summary>
    private static PdfPoint RotateHandle(BoxAnnotation box, double scale) =>
        box.FromLocal(0, -box.Bounds.Height / 2 - RotateHandleDistance * scale);

    /// <summary>
    /// Zeiger und Vorschau beim Werkzeug „Stempel“ ohne gedrückte Taste.
    /// </summary>
    /// <returns>Der Mauszeiger.</returns>
    private Cursor UpdateBoxHover(PdfPoint display)
    {
        var (hit, corner) = HitBox(display);
        _boxHover = hit;
        InvalidateVisual();
        return hit switch
        {
            BoxHit.RotateHandle => new Cursor(StandardCursorType.Hand),
            BoxHit.Corner => new Cursor(CornerCursor(corner, SelectedBoxDisplay?.Angle ?? 0)),
            BoxHit.Box => new Cursor(StandardCursorType.SizeAll),
            _ => new Cursor(StandardCursorType.Cross),
        };
    }

    /// <summary>Größenänderungszeiger für eine Ecke, passend zur Drehung.</summary>
    private static StandardCursorType CornerCursor(int corner, double angle) =>
        ((corner + (int)Math.Round(angle / 90)) % 4) switch
        {
            0 => StandardCursorType.TopLeftCorner,
            1 => StandardCursorType.TopRightCorner,
            2 => StandardCursorType.BottomRightCorner,
            _ => StandardCursorType.BottomLeftCorner,
        };

    /// <summary>
    /// Zeichnet die Vorschau beim Ziehen bzw. das Abbild am Zeiger und die Griffe der Auswahl.
    /// </summary>
    private void DrawBoxOverlay(DrawingContext context)
    {
        if (Options is not { Tool: Tool.Stamp } options)
        {
            return;
        }

        if (_boxPreview is { } preview)
        {
            using (context.PushOpacity(_boxGesture == BoxGesture.Place ? 0.6 : 1))
            {
                BoxRenderer.Draw(context, preview);
            }
        }
        else if (_gesture is null && _pointer is { } pointer && _boxHover == BoxHit.None)
        {
            using (context.PushOpacity(0.45))
            {
                BoxRenderer.Draw(context, NewBox(pointer, DefaultBoxWidth(options)));
            }
        }

        var selected = _boxGesture is BoxGesture.Move or BoxGesture.Resize or BoxGesture.Rotate ? _boxPreview ?? _boxOriginal : SelectedBoxDisplay;
        if (selected is not null && _boxGesture != BoxGesture.Place)
        {
            DrawSelection(context, selected);
        }
    }

    /// <summary>
    /// Gestrichelter Rahmen in der Akzentfarbe, vier Eckgriffe und der Drehgriff über der Oberkante.
    /// </summary>
    private void DrawSelection(DrawingContext context, BoxAnnotation box)
    {
        var scale = 1 / Math.Max(Zoom, 0.01);
        var accent = this.TryFindResource("AccentBorderBrush", ActualThemeVariant, out var resource) && resource is ISolidColorBrush brush
            ? brush.Color
            : Color.Parse(AccentPalette.DefaultAccent);
        var accentBrush = new SolidColorBrush(accent);
        var dashed = new Pen(accentBrush, 1.2 * scale, new DashStyle([4, 3], 0));
        var solid = new Pen(accentBrush, 1.5 * scale);
        var corners = box.Corners;
        var outline = new PolylineGeometry(corners.Select(point => new Point(point.X, point.Y)), true);
        context.DrawGeometry(null, dashed, outline);

        var top = box.FromLocal(0, -box.Bounds.Height / 2);
        var handle = RotateHandle(box, scale);
        context.DrawLine(solid, new Point(top.X, top.Y), new Point(handle.X, handle.Y));
        context.DrawEllipse(Brushes.White, solid, new Point(handle.X, handle.Y), RotateHandleRadius * scale, RotateHandleRadius * scale);

        var half = HandleSize / 2 * scale;
        foreach (var corner in corners)
        {
            context.DrawRectangle(Brushes.White, solid, new Rect(corner.X - half, corner.Y - half, 2 * half, 2 * half), 1.5 * scale, 1.5 * scale);
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Document is not { } document || Options is not { Tool: Tool.Stamp or Tool.Redact } options)
        {
            return;
        }

        if (e.Key == Key.Delete && document.SelectedBox is { } id && Annotations.Any(annotation => annotation.Id == id))
        {
            document.Document.RemoveAnnotation(PageId, id);
            document.SelectedBox = null;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && (document.SelectedBox is not null || options.PendingImage is not null))
        {
            document.SelectedBox = null;
            options.PendingImage = null;
            e.Handled = true;
        }

        InvalidateVisual();
    }

    /// <summary>Lässt nur Bilddateien (PNG, JPEG) zum Ablegen zu.</summary>
    private void OnImageDragOver(object? sender, DragEventArgs e)
    {
        var hasImage = e.DataTransfer.TryGetFiles()?.Any(file => file.TryGetLocalPath() is { } path && ImageLoader.IsSupported(path)) == true;
        e.DragEffects = hasImage ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = hasImage;
    }

    /// <summary>Setzt eine abgelegte Bilddatei mit der Mitte an den Ablagepunkt.</summary>
    private async void OnImageDrop(object? sender, DragEventArgs e)
    {
        var path = e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).FirstOrDefault(path => path is not null && ImageLoader.IsSupported(path));
        if (path is null || Page is not { } page || TopLevel.GetTopLevel(this)?.DataContext is not MainWindowViewModel main)
        {
            return;
        }

        e.Handled = true;
        var position = e.GetPosition(this);
        if (await main.LoadImageAsync(path) is { } loaded)
        {
            main.PlaceImage(page, loaded, new PdfPoint(position.X, position.Y));
        }
    }
}
