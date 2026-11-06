using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using PdfUtopia.Core.Signatures;

namespace PdfUtopia.App.Controls;

/// <summary>
/// Verkleinerte Ansicht einer gespeicherten Unterschrift, mittig und im Seitenverhältnis eingepasst (A49).
/// </summary>
public sealed class SignaturePreview : Control
{
    /// <summary>Die Unterschrift.</summary>
    public static readonly StyledProperty<Signature?> SignatureProperty =
        AvaloniaProperty.Register<SignaturePreview, Signature?>(nameof(Signature));

    private const double Padding = 3;

    static SignaturePreview() => AffectsRender<SignaturePreview>(SignatureProperty);

    /// <summary>Die Unterschrift.</summary>
    public Signature? Signature
    {
        get => GetValue(SignatureProperty);
        set => SetValue(SignatureProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        if (Signature is not { HasStrokes: true } signature || Bounds.Width <= 2 * Padding || Bounds.Height <= 2 * Padding)
        {
            return;
        }

        var bounds = signature.Bounds;
        var scale = Math.Min((Bounds.Width - 2 * Padding) / bounds.Width, (Bounds.Height - 2 * Padding) / bounds.Height);
        var offsetX = (Bounds.Width - bounds.Width * scale) / 2 - bounds.X * scale;
        var offsetY = (Bounds.Height - bounds.Height * scale) / 2 - bounds.Y * scale;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            StrokeRenderer.Draw(context, signature.Strokes, signature.ColorHex, Signature.PadStrokeWidth, signature.PressureSensitive, Signature.Smoothing);
        }
    }
}
