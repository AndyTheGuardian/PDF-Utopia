namespace PdfUtopia.Core.Text;

/// <summary>
/// Bricht Text in Helvetica auf eine Breite um: an Leerzeichen, zu lange Wörter zeichenweise. Zeilenumbrüche im
/// Text beginnen eine neue Zeile.
/// </summary>
public static class TextWrapper
{
    /// <summary>
    /// Bricht einen Text um.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <param name="width">Verfügbare Breite in Punkten.</param>
    /// <param name="fontSize">Schriftgröße in Punkten.</param>
    /// <returns>Die Zeilen; mindestens eine (ggf. leere).</returns>
    public static IReadOnlyList<string> Wrap(string text, double width, double fontSize)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            WrapParagraph(paragraph, width, fontSize, lines);
        }

        return lines.Count == 0 ? [string.Empty] : lines;
    }

    /// <summary>
    /// Bricht einen Absatz um.
    /// </summary>
    private static void WrapParagraph(string paragraph, double width, double fontSize, List<string> lines)
    {
        var current = string.Empty;
        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : $"{current} {word}";
            if (Fits(candidate, width, fontSize))
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
            }

            current = BreakLongWord(word, width, fontSize, lines);
        }

        lines.Add(current);
    }

    /// <summary>
    /// Teilt ein Wort, das allein nicht in eine Zeile passt; gibt den Rest zurück.
    /// </summary>
    private static string BreakLongWord(string word, double width, double fontSize, List<string> lines)
    {
        var rest = word;
        while (!Fits(rest, width, fontSize) && rest.Length > 1)
        {
            var length = rest.Length - 1;
            while (length > 1 && !Fits(rest[..length], width, fontSize))
            {
                length--;
            }

            lines.Add(rest[..length]);
            rest = rest[length..];
        }

        return rest;
    }

    /// <summary>Ob ein Text in die Breite passt.</summary>
    private static bool Fits(string text, double width, double fontSize) => Helvetica.Width(text, fontSize) <= width;
}
