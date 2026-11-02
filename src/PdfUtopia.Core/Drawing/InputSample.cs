using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Core.Drawing;

/// <summary>
/// Art des Eingabegeräts, wie es das Betriebssystem meldet.
/// </summary>
public enum PointerKind
{
    /// <summary>Maus (auch Tablets, die sich als Maus melden).</summary>
    Mouse,

    /// <summary>Stift.</summary>
    Pen,

    /// <summary>Finger auf einem Touchscreen.</summary>
    Touch,
}

/// <summary>
/// Ein Messpunkt eines Eingabegeräts, unabhängig von der Oberflächenbibliothek.
/// </summary>
/// <param name="Position">Position auf der Seite.</param>
/// <param name="Pressure">Gemeldeter Andruck 0–1; 0, wenn das Gerät keinen meldet.</param>
/// <param name="IsPrimaryButtonPressed">Ob die Haupttaste gedrückt ist bzw. der Stift aufliegt.</param>
/// <param name="Kind">Gerätetyp.</param>
/// <param name="IsPrimary">Ob es der Hauptzeiger ist (bei Multi-Touch nur der erste Finger).</param>
/// <param name="IsEraser">Ob das Radiererende des Stifts benutzt wird; es radiert unabhängig vom Werkzeug (A27).</param>
public readonly record struct InputSample(
    PdfPoint Position, float Pressure, bool IsPrimaryButtonPressed, PointerKind Kind, bool IsPrimary = true, bool IsEraser = false)
{
    /// <summary>
    /// Kontakt: Haupttaste gedrückt oder Andruck größer 0. Manche Treiber melden bei Kontakt keine Taste (A12).
    /// </summary>
    public bool IsInContact => IsPrimaryButtonPressed || Pressure > 0;

    /// <summary>Weitere Finger bei Multi-Touch werden ignoriert.</summary>
    public bool IsExtraTouch => Kind == PointerKind.Touch && !IsPrimary;
}
