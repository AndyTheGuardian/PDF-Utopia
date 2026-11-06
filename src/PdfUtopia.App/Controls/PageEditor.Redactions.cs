using Avalonia;
using Avalonia.Media;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Werkzeug „Schwärzen“ (A52, Mockup v8): Bereiche aufziehen; bis zum Speichern rot gestrichelt und schraffiert, damit
/// der Inhalt darunter prüfbar bleibt. Eine angeklickte Markierung wird mit Entf entfernt.
/// </summary>
public sealed partial class PageEditor
{
    private static readonly Color RedactRed = Color.Parse("#DC2626");

    /// <summary>
    /// Beginnt eine Eingabe mit dem Werkzeug „Schwärzen“: eine Markierung anklicken oder einen Bereich aufziehen.
    /// </summary>
    private void BeginRedaction(PdfPoint display)
    {
        Focus();
        Document!.SelectedBox = RedactionAt(display)?.Id;
    }

    /// <summary>
    /// Legt den aufgezogenen Bereich als Markierung an und wählt sie.
    /// </summary>
    private void FinishRedaction(DocumentViewModel document)
    {
        if (document.SelectedBox is not null && _current.DistanceTo(_start) < MinDragDistance)
        {
            return;
        }

        if (IsDrag())
        {
            var redaction = new RedactionAnnotation(Guid.NewGuid(), ToPage(DragRect()));
            Page!.AddAnnotation(redaction);
            document.SelectedBox = redaction.Id;
        }
    }

    /// <summary>Oberste Markierung an einer Anzeigeposition.</summary>
    private RedactionAnnotation? RedactionAt(PdfPoint display) =>
        Annotations.OfType<RedactionAnnotation>()
            .LastOrDefault(redaction => Contains(((RedactionAnnotation)ToDisplay(redaction)).Bounds, display));

    /// <summary>
    /// Zeichnet eine Markierung: rot schraffiert und gestrichelt, die angeklickte mit durchgezogenem Rahmen.
    /// </summary>
    private void DrawRedaction(DrawingContext context, RedactionAnnotation redaction)
    {
        var rect = ToRect(redaction.Bounds);
        var selected = Options?.Tool == Tool.Redact && Document?.SelectedBox == redaction.Id;
        context.FillRectangle(new SolidColorBrush(RedactRed, 0.1), rect);
        using (context.PushClip(rect))
        {
            var hatch = new Pen(new SolidColorBrush(RedactRed, 0.45), 1.5);
            for (var offset = -rect.Height; offset < rect.Width; offset += 6)
            {
                context.DrawLine(hatch, new Point(rect.X + offset, rect.Bottom), new Point(rect.X + offset + rect.Height, rect.Y));
            }
        }

        var border = selected ? new Pen(new SolidColorBrush(RedactRed), 2) : new Pen(new SolidColorBrush(RedactRed), 1.2, new DashStyle([4, 2.5], 0));
        context.DrawRectangle(null, border, rect);
    }

    /// <summary>Vorschau beim Aufziehen.</summary>
    private void DrawRedactionGesture(DrawingContext context)
    {
        if (_gesture == Tool.Redact && Document?.SelectedBox is null && IsDrag())
        {
            DrawRedaction(context, new RedactionAnnotation(Guid.Empty, DragRect()));
        }
    }
}
