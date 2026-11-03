namespace PdfUtopia.Core.Pages;

/// <summary>Tasten, mit denen sich in der Seitenansicht blättern lässt (A38).</summary>
public enum NavigationKey
{
    /// <summary>Pfeil nach oben: scrollen, am oberen Rand zur vorigen Seite.</summary>
    Up,

    /// <summary>Pfeil nach unten: scrollen, am unteren Rand zur nächsten Seite.</summary>
    Down,

    /// <summary>Pfeil nach links: vorige Seite.</summary>
    Left,

    /// <summary>Pfeil nach rechts: nächste Seite.</summary>
    Right,

    /// <summary>Bild auf: vorige Seite.</summary>
    PageUp,

    /// <summary>Bild ab: nächste Seite.</summary>
    PageDown,

    /// <summary>Pos1: erste Seite.</summary>
    Home,

    /// <summary>Ende: letzte Seite.</summary>
    End,
}

/// <summary>Was mit einer Eingabe in der Seitenansicht geschieht.</summary>
public enum NavigationKind
{
    /// <summary>Innerhalb der Seite scrollen.</summary>
    Scroll,

    /// <summary>Eingabe verwerfen (z. B. kurz nach einem Seitenwechsel oder an der ersten/letzten Seite).</summary>
    Ignore,

    /// <summary>Zu einer anderen Seite wechseln.</summary>
    GoTo,
}

/// <summary>
/// Ergebnis einer Eingabe.
/// </summary>
/// <param name="Kind">Was geschieht.</param>
/// <param name="TargetIndex">Zielseite (0-basiert), nur bei <see cref="NavigationKind.GoTo"/>.</param>
/// <param name="LandAtBottom">Ob die Zielseite am unteren Rand gezeigt wird (rückwärts blättern beim Scrollen).</param>
public readonly record struct NavigationStep(NavigationKind Kind, int TargetIndex = 0, bool LandAtBottom = false)
{
    /// <summary>Innerhalb der Seite scrollen.</summary>
    public static NavigationStep Scroll => new(NavigationKind.Scroll);

    /// <summary>Eingabe verwerfen.</summary>
    public static NavigationStep Ignore => new(NavigationKind.Ignore);
}

/// <summary>
/// Lage der Bildlaufleiste: ob die Seite oben bzw. unten am Rand steht.
/// </summary>
/// <param name="AtTop">Oberer Rand erreicht (oder die Seite passt ganz in die Ansicht).</param>
/// <param name="AtBottom">Unterer Rand erreicht (oder die Seite passt ganz in die Ansicht).</param>
public readonly record struct ScrollPosition(bool AtTop, bool AtBottom)
{
    /// <summary>Toleranz in Pixeln, ab der ein Rand als erreicht gilt (Rundung beim Zoomen).</summary>
    public const double Tolerance = 1;

    /// <summary>
    /// Bestimmt die Lage aus den Werten der Bildlaufleiste.
    /// </summary>
    /// <param name="offset">Senkrechter Versatz.</param>
    /// <param name="extent">Gesamthöhe des Inhalts.</param>
    /// <param name="viewport">Sichtbare Höhe.</param>
    /// <returns>Die Lage.</returns>
    public static ScrollPosition From(double offset, double extent, double viewport) =>
        new(offset <= Tolerance, offset >= extent - viewport - Tolerance);
}

/// <summary>
/// Blättern in der Seitenansicht mit Mausrad und Tasten (A38): Ist die Seite größer als die Ansicht, wird zuerst
/// gescrollt; erst eine weitere Eingabe am Rand wechselt die Seite. Rückwärts per Scrollen landet man am unteren Rand
/// der vorigen Seite, damit sich das Lesen fortsetzt. Nach einem Wechsel per Mausrad werden Radbewegungen kurz
/// verworfen, damit ein Touchpad-Schwung nicht mehrere Seiten überspringt.
/// </summary>
public sealed class PageNavigator
{
    /// <summary>Wartezeit nach einem Seitenwechsel per Mausrad.</summary>
    public static readonly TimeSpan WheelCooldown = TimeSpan.FromMilliseconds(300);

    /// <summary>Radbewegung (in Rastungen), die am Rand für einen Seitenwechsel nötig ist.</summary>
    public const double WheelThreshold = 1;

    private double _accumulated;
    private TimeSpan? _lastFlip;

    /// <summary>
    /// Wertet eine Radbewegung aus.
    /// </summary>
    /// <param name="deltaY">Senkrechte Bewegung in Rastungen; positiv nach oben (rückwärts).</param>
    /// <param name="position">Lage der Bildlaufleiste.</param>
    /// <param name="currentIndex">Angezeigte Seite (0-basiert).</param>
    /// <param name="pageCount">Anzahl der Seiten.</param>
    /// <param name="timestamp">Zeitpunkt der Eingabe.</param>
    /// <returns>Was geschieht.</returns>
    public NavigationStep Wheel(double deltaY, ScrollPosition position, int currentIndex, int pageCount, TimeSpan timestamp)
    {
        if (_lastFlip is { } last && timestamp - last >= TimeSpan.Zero && timestamp - last < WheelCooldown)
        {
            return NavigationStep.Ignore;
        }

        if (deltaY == 0)
        {
            return NavigationStep.Scroll;
        }

        var backwards = deltaY > 0;
        var target = currentIndex + (backwards ? -1 : 1);
        var atEdge = backwards ? position.AtTop : position.AtBottom;
        if (!atEdge || target < 0 || target >= pageCount)
        {
            _accumulated = 0;
            return NavigationStep.Scroll;
        }

        if (Math.Sign(_accumulated) != Math.Sign(deltaY))
        {
            _accumulated = 0;
        }

        _accumulated += deltaY;
        if (Math.Abs(_accumulated) < WheelThreshold)
        {
            return NavigationStep.Ignore;
        }

        _accumulated = 0;
        _lastFlip = timestamp;
        return new NavigationStep(NavigationKind.GoTo, target, LandAtBottom: backwards);
    }

    /// <summary>
    /// Wertet einen Tastendruck aus.
    /// </summary>
    /// <param name="key">Die Taste.</param>
    /// <param name="position">Lage der Bildlaufleiste.</param>
    /// <param name="currentIndex">Angezeigte Seite (0-basiert).</param>
    /// <param name="pageCount">Anzahl der Seiten.</param>
    /// <returns>Was geschieht.</returns>
    public static NavigationStep Key(NavigationKey key, ScrollPosition position, int currentIndex, int pageCount) => key switch
    {
        NavigationKey.Up => position.AtTop ? Neighbour(currentIndex - 1, pageCount, landAtBottom: true) : NavigationStep.Scroll,
        NavigationKey.Down => position.AtBottom ? Neighbour(currentIndex + 1, pageCount, landAtBottom: false) : NavigationStep.Scroll,
        NavigationKey.Left or NavigationKey.PageUp => Neighbour(currentIndex - 1, pageCount, landAtBottom: false),
        NavigationKey.Right or NavigationKey.PageDown => Neighbour(currentIndex + 1, pageCount, landAtBottom: false),
        NavigationKey.Home => new NavigationStep(NavigationKind.GoTo, 0),
        NavigationKey.End => new NavigationStep(NavigationKind.GoTo, Math.Max(0, pageCount - 1)),
        _ => NavigationStep.Ignore,
    };

    /// <summary>Wechsel zur Nachbarseite, sofern es sie gibt.</summary>
    private static NavigationStep Neighbour(int target, int pageCount, bool landAtBottom) =>
        target >= 0 && target < pageCount ? new NavigationStep(NavigationKind.GoTo, target, landAtBottom) : NavigationStep.Ignore;
}
