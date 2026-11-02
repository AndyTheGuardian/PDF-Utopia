namespace PdfUtopia.Core.Pages;

/// <summary>
/// Drehung einer Seite im Uhrzeigersinn in Vierteldrehungen; der Zahlenwert entspricht den Grad.
/// </summary>
public enum PageRotation
{
    /// <summary>Nicht gedreht.</summary>
    None = 0,

    /// <summary>90° im Uhrzeigersinn.</summary>
    Clockwise90 = 90,

    /// <summary>180°.</summary>
    Rotate180 = 180,

    /// <summary>270° im Uhrzeigersinn (90° gegen den Uhrzeigersinn).</summary>
    Clockwise270 = 270,
}

/// <summary>
/// Rechenfunktionen für <see cref="PageRotation"/>.
/// </summary>
public static class PageRotationExtensions
{
    /// <summary>
    /// Dreht um eine Vierteldrehung weiter.
    /// </summary>
    /// <param name="rotation">Aktuelle Drehung.</param>
    /// <param name="clockwise"><c>true</c> im, <c>false</c> gegen den Uhrzeigersinn.</param>
    /// <returns>Die neue Drehung.</returns>
    public static PageRotation Turn(this PageRotation rotation, bool clockwise = true) =>
        (PageRotation)((((int)rotation + (clockwise ? 90 : 270)) % 360 + 360) % 360);
}
