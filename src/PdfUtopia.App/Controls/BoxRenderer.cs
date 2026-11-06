using System.Globalization;
using Avalonia;
using Avalonia.Media;
using PdfUtopia.App.Services;
using PdfUtopia.Core.Annotations;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Zeichnet Stempel und Bilder (A51) wie in der gespeicherten PDF: in ihrem Rechteck, um die Mitte gedreht.
/// </summary>
internal static class BoxRenderer
{
    /// <summary>Schrift mit denselben Zeichenbreiten wie Helvetica.</summary>
    private static readonly FontFamily Font = new("Liberation Sans, Arial, Helvetica, sans-serif");

    /// <summary>
    /// Zeichnet einen Stempel oder ein Bild (Koordinaten der Anzeige).
    /// </summary>
    /// <param name="context">Zeichenfläche.</param>
    /// <param name="box">Stempel oder Bild.</param>
    public static void Draw(DrawingContext context, BoxAnnotation box)
    {
        var center = box.Center;
        var transform = Matrix.CreateTranslation(-box.Bounds.Width / 2, -box.Bounds.Height / 2)
            * Matrix.CreateRotation(box.Angle * Math.PI / 180)
            * Matrix.CreateTranslation(center.X, center.Y);
        using (context.PushTransform(transform))
        {
            switch (box)
            {
                case StampAnnotation stamp:
                    DrawStamp(context, stamp);
                    break;
                case ImageAnnotation image:
                    context.DrawImage(ImageLoader.BitmapOf(image.Image), new Rect(0, 0, image.Bounds.Width, image.Bounds.Height));
                    break;
            }
        }
    }

    /// <summary>
    /// Zeichnet einen Stempel ab der linken oberen Ecke, skaliert auf seine Breite.
    /// </summary>
    /// <param name="context">Zeichenfläche.</param>
    /// <param name="stamp">Der Stempel.</param>
    public static void DrawStamp(DrawingContext context, StampAnnotation stamp)
    {
        var layout = stamp.Layout;
        var scale = stamp.Bounds.Width / layout.Width;
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            var color = Color.Parse(stamp.ColorHex);
            var brush = new SolidColorBrush(color);
            var fill = new SolidColorBrush(color, StampLayout.FillOpacity);
            var outer = Inset(layout, StampLayout.OuterInset);
            context.DrawRectangle(fill, new Pen(brush, StampLayout.OuterStroke), outer, StampLayout.OuterRadius, StampLayout.OuterRadius);
            context.DrawRectangle(null, new Pen(brush, StampLayout.InnerStroke), Inset(layout, StampLayout.InnerInset), StampLayout.InnerRadius, StampLayout.InnerRadius);
            DrawLine(context, layout.Label, StampLayout.LabelSize, FontWeight.Bold, brush, layout.LabelLeft, layout.LabelBaseline);
            if (layout.HasDetail)
            {
                DrawLine(context, layout.Detail, StampLayout.DetailSize, FontWeight.Normal, brush, layout.DetailLeft, layout.DetailBaseline);
            }
        }
    }

    /// <summary>Rechteck des Stempels, an allen Seiten eingerückt.</summary>
    private static Rect Inset(StampLayout layout, double inset) =>
        new(inset, inset, layout.Width - 2 * inset, layout.Height - 2 * inset);

    /// <summary>Textzeile mit Grundlinie an der angegebenen Höhe.</summary>
    private static void DrawLine(DrawingContext context, string text, double size, FontWeight weight, IBrush brush, double left, double baseline)
    {
        var line = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Font, FontStyle.Normal, weight), size, brush);
        context.DrawText(line, new Point(left, baseline - line.Baseline));
    }
}

/// <summary>
/// Verkleinerte Ansicht eines Stempels, mittig eingepasst (Optionsleiste, Auswahl, Dialog).
/// </summary>
public sealed class StampPreview : Avalonia.Controls.Control
{
    /// <summary>Der Stempel.</summary>
    public static readonly StyledProperty<StampAnnotation?> StampProperty =
        AvaloniaProperty.Register<StampPreview, StampAnnotation?>(nameof(Stamp));

    static StampPreview() => AffectsRender<StampPreview>(StampProperty);

    /// <summary>Der Stempel.</summary>
    public StampAnnotation? Stamp
    {
        get => GetValue(StampProperty);
        set => SetValue(StampProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Stamp is not { } stamp || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        var size = stamp.Bounds;
        var scale = Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height);
        var offset = Matrix.CreateTranslation((Bounds.Width / scale - size.Width) / 2, (Bounds.Height / scale - size.Height) / 2);
        using (context.PushTransform(offset * Matrix.CreateScale(scale, scale)))
        {
            BoxRenderer.DrawStamp(context, stamp);
        }
    }
}
