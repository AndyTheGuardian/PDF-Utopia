namespace PdfUtopia.App.ViewModels;

/// <summary>
/// Werkzeuge der Werkzeugleiste.
/// </summary>
public enum Tool
{
    /// <summary>Auswählen (keine Bearbeitung auf der Seite).</summary>
    Select,

    /// <summary>Kommentar: Absatz anklicken oder Bereich aufziehen.</summary>
    Comment,

    /// <summary>Textmarker.</summary>
    Marker,

    /// <summary>Freihand.</summary>
    Freehand,

    /// <summary>Formen: Rechteck, Ellipse, Linie, Pfeil (A44).</summary>
    Shape,

    /// <summary>Freier Text (A43).</summary>
    Text,

    /// <summary>Unterschrift (A49).</summary>
    Signature,

    /// <summary>Radierer (Objekt-Radierer).</summary>
    Eraser,
}

/// <summary>
/// Ziel der gemeinsamen Farbwahl (A15).
/// </summary>
public enum ColorTarget
{
    /// <summary>Akzentfarbe der Oberfläche.</summary>
    Accent,

    /// <summary>Freihand.</summary>
    Freehand,

    /// <summary>Formen.</summary>
    Shape,

    /// <summary>Kommentar.</summary>
    Comment,

    /// <summary>Text-Werkzeug.</summary>
    Text,
}
