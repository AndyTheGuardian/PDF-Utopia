namespace PdfUtopia.Core.Text;

/// <summary>
/// Zeichenbreiten der PDF-Standardschrift Helvetica (aus den Adobe-Schriftmetriken). Werden im Core gebraucht,
/// damit Bildschirm und gespeicherte PDF-Datei Text gleich umbrechen (Text-Werkzeug, Fußnoten).
/// </summary>
public static class Helvetica
{
    /// <summary>Oberlänge in 1/1000 Schriftgröße.</summary>
    public const double Ascent = 718;

    /// <summary>Unterlänge in 1/1000 Schriftgröße (positiv).</summary>
    public const double Descent = 207;

    /// <summary>Breite unbekannter Zeichen in 1/1000 Schriftgröße.</summary>
    private const int DefaultWidth = 556;

    /// <summary>Fette Schrift ist im Mittel etwa 5 % breiter.</summary>
    private const double BoldFactor = 1.05;

    private static readonly Dictionary<char, int> Widths = BuildWidths();

    /// <summary>
    /// Breite eines Textes in Punkten.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <param name="size">Schriftgröße in Punkten.</param>
    /// <param name="bold">Fette Schrift.</param>
    /// <returns>Die Breite.</returns>
    public static double Width(string text, double size, bool bold = false) =>
        text.Sum(c => Widths.GetValueOrDefault(c, DefaultWidth)) * size / 1000 * (bold ? BoldFactor : 1);

    /// <summary>
    /// Zeichenbreiten der Helvetica (Adobe AFM), gruppiert nach Breite.
    /// </summary>
    private static Dictionary<char, int> BuildWidths()
    {
        var groups = new (string Chars, int Width)[]
        {
            (" !,./:;[\\]fIt·íìîïÍÌÎÏ", 278), ("ijl", 222), ("'", 191), ("|¦", 260), ("()-`r¨¯´¸ˆ˜¡²³¹‹›„“”", 333),
            ("{}", 334), ("\"", 355), ("ª", 370), ("º", 365), ("*", 389), ("°", 400), ("^", 469),
            ("Jckvxyzsçýÿšž", 500), ("¶", 537), ("#$0123456789?L_abdeghnopquäåàáâãéèêëöóòôõüúùûñµ€§¢£¥«»ðþƒ†‡–", 556),
            ("+<=>~×÷¬±", 584), ("FTZßø¿Ž", 611), ("&ABEKPSVXYÄÅÀÁÂÃÉÈÊËÝÞŠŸ", 667), ("CDHNRUwÇÜÚÙÛÑÐ", 722),
            ("©®", 737), ("GOQÖØÓÒÔÕ", 778), ("Mm", 833), ("¼½¾", 834), ("%æ", 889), ("Wœ", 944),
            ("—…ÆŒ‰™", 1000), ("@", 1015), ("•", 350), ("‘’‚", 222),
        };
        var widths = new Dictionary<char, int>();
        foreach (var (chars, width) in groups)
        {
            foreach (var c in chars)
            {
                widths[c] = width;
            }
        }

        return widths;
    }
}
