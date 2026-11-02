using System.Text;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// PDF-Standardschrift Helvetica mit WinAnsi-Kodierung: in jedem PDF-Betrachter vorhanden und muss nicht
/// eingebettet werden. Liefert Zeichenbreiten (aus den Adobe-Schriftmetriken) für den Zeilenumbruch und die
/// Kodierung von Text für Inhaltsströme. Zeichen außerhalb von WinAnsi werden als „?“ ausgegeben (E5).
/// </summary>
internal static class StandardFont
{
    /// <summary>Ressourcenname der normalen Schrift.</summary>
    public const string Regular = "/Helv";

    /// <summary>Ressourcenname der fetten Schrift.</summary>
    public const string Bold = "/HelvB";

    /// <summary>Breite unbekannter Zeichen in 1/1000 Schriftgröße.</summary>
    private const int DefaultWidth = 556;

    /// <summary>Fette Schrift ist im Mittel etwa 5 % breiter.</summary>
    private const double BoldFactor = 1.05;

    private static readonly Dictionary<char, int> Widths = BuildWidths();
    private static readonly Dictionary<char, byte> WinAnsiSpecials = BuildWinAnsiSpecials();

    /// <summary>
    /// Breite eines Textes in Punkten.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <param name="size">Schriftgröße in Punkten.</param>
    /// <param name="bold">Ob die fette Schrift verwendet wird.</param>
    /// <returns>Die Breite.</returns>
    public static double Width(string text, double size, bool bold = false) =>
        text.Sum(c => Widths.GetValueOrDefault(c, DefaultWidth)) * size / 1000 * (bold ? BoldFactor : 1);

    /// <summary>
    /// Kodiert Text als WinAnsi-Bytes; nicht darstellbare Zeichen werden zu „?“.
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <returns>Die Bytes.</returns>
    public static byte[] Encode(string text) => text.Select(EncodeChar).ToArray();

    /// <summary>
    /// Ob ein Zeichen in WinAnsi darstellbar ist.
    /// </summary>
    /// <param name="c">Das Zeichen.</param>
    /// <returns><c>true</c>, wenn darstellbar.</returns>
    public static bool CanEncode(char c) => c is >= ' ' and <= '~' or >= ' ' and <= 'ÿ' || WinAnsiSpecials.ContainsKey(c);

    /// <summary>
    /// Kodiert ein Zeichen.
    /// </summary>
    private static byte EncodeChar(char c) => c switch
    {
        >= ' ' and <= '~' => (byte)c,
        >= ' ' and <= 'ÿ' => (byte)c,
        _ => WinAnsiSpecials.GetValueOrDefault(c, (byte)'?'),
    };

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

    /// <summary>
    /// WinAnsi-Zeichen im Bereich 0x80–0x9F, die nicht mit Latin-1 übereinstimmen.
    /// </summary>
    private static Dictionary<char, byte> BuildWinAnsiSpecials()
    {
        const string chars = "€‚ƒ„…†‡ˆ‰Š‹ŒŽ‘’“”•–—˜™š›œžŸ";
        byte[] codes = [0x80, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8A, 0x8B, 0x8C, 0x8E, 0x91, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0x9B, 0x9C, 0x9E, 0x9F];
        return chars.Zip(codes).ToDictionary(pair => pair.First, pair => pair.Second);
    }

    /// <summary>
    /// Wandelt WinAnsi-Bytes in eine PDF-Zeichenkette mit Escapes (für Inhaltsströme, nur ASCII).
    /// </summary>
    /// <param name="text">Der Text.</param>
    /// <returns>Die Zeichenkette inklusive Klammern, z. B. <c>(Gr\374\337e)</c>.</returns>
    public static string ToPdfString(string text)
    {
        var builder = new StringBuilder("(");
        foreach (var b in Encode(text))
        {
            builder.Append(b switch
            {
                (byte)'(' or (byte)')' or (byte)'\\' => "\\" + (char)b,
                < 0x20 or >= 0x7F => "\\" + Convert.ToString(b, 8).PadLeft(3, '0'),
                _ => ((char)b).ToString(),
            });
        }

        return builder.Append(')').ToString();
    }
}
