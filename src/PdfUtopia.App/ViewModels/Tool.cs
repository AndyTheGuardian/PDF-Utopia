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

    /// <summary>Rechteck.</summary>
    Rectangle,

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

    /// <summary>Rechteck.</summary>
    Rectangle,

    /// <summary>Kommentar.</summary>
    Comment,
}
