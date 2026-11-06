using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Images;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Eingefügtes Bild (A51), verzerrungsfrei in <see cref="BoxAnnotation.Bounds"/> und um die Mitte gedreht.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="Bounds">Ungedrehtes Rechteck.</param>
/// <param name="Angle">Drehung in Grad im Uhrzeigersinn.</param>
/// <param name="Image">Die Bilddaten.</param>
public sealed record ImageAnnotation(Guid Id, PdfRect Bounds, double Angle, ImageData Image)
    : BoxAnnotation(Id, FrameColor, Bounds, Angle)
{
    /// <summary>Farbe für Rahmen in Übersicht und Hervorhebung.</summary>
    public const string FrameColor = "#64748B";

    /// <summary>Breite eines per Klick gesetzten Bildes in Punkten.</summary>
    public const double DefaultWidth = 200;

    /// <summary>
    /// Legt ein Bild mit der Mitte an einer Stelle an; die Höhe folgt dem Seitenverhältnis.
    /// </summary>
    /// <param name="image">Die Bilddaten.</param>
    /// <param name="center">Mitte.</param>
    /// <param name="width">Breite.</param>
    /// <param name="angle">Drehung in Grad.</param>
    /// <returns>Das Bild.</returns>
    public static ImageAnnotation Create(ImageData image, PdfPoint center, double width, double angle = 0)
    {
        width = Math.Max(MinWidth, width);
        return new ImageAnnotation(Guid.NewGuid(), Centered(center, width, width * image.AspectRatio), NormalizeAngle(angle), image);
    }
}
