using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;

namespace PdfUtopia.App.Services;

/// <summary>
/// Holt Fenster nach vorn, die das Programm selbst öffnet (z. B. nach einem Sprachwechsel neu aufgebaut, P22).
/// Fenstermanager mit Schutz vor Fokusraub stellen solche Fenster sonst hinter andere Anwendungen.
/// </summary>
internal static class WindowFocus
{
    /// <summary>Größter Abstand zwischen Rahmen und Inhalt, der als Titelleiste bzw. Rand gilt.</summary>
    private const int MaxFrameOffset = 80;

    /// <summary>Gemessene Lage je Fenster.</summary>
    private static readonly ConditionalWeakTable<Window, Placement> Placements = new();

    /// <summary>
    /// Verfolgt ab jetzt die Lage eines Fensters (P22). Avalonia meldet zwei Lagen: Abfrage und Setzen
    /// (<see cref="Window.Position"/>) beziehen sich auf den Rahmen samt Titelleiste, werden beim Ziehen des Fensters aber
    /// nicht aktualisiert; die Bewegungsmeldungen (<c>PositionChanged</c>) kommen an, beziehen sich aber
    /// auf den Inhalt. Der Abstand zwischen beiden wird einmal gemessen, damit sich jede Bewegung als Rahmenlage
    /// ausdrücken lässt.
    /// </summary>
    /// <param name="window">Das Fenster, am besten vor dem Öffnen.</param>
    public static void Track(Window window) => Placements.GetValue(window, created => new Placement(created));

    /// <summary>
    /// Aktuelle Lage eines Fensters als Rahmenlage, die sich mit <see cref="Window.Position"/> wieder setzen lässt.
    /// </summary>
    /// <param name="window">Das Fenster.</param>
    /// <returns>Die Lage; ohne Messung die abgefragte.</returns>
    public static Avalonia.PixelPoint FramePosition(Window window) =>
        Placements.TryGetValue(window, out var placement) ? placement.FramePosition : window.Position;

    /// <summary>Lage eines verfolgten Fensters.</summary>
    private sealed class Placement
    {
        private readonly Window _window;
        private Avalonia.PixelPoint? _content;
        private Avalonia.PixelPoint? _offset;

        public Placement(Window window)
        {
            _window = window;
            window.PositionChanged += (_, e) =>
            {
                // Erste Meldung, die von der gesetzten Lage abweicht: das ist der Inhalt unter Titelleiste und Rand
                var difference = e.Point - window.Position;
                if (_offset is null && difference != default && difference.X is >= 0 and <= MaxFrameOffset
                    && difference.Y is >= 0 and <= MaxFrameOffset)
                {
                    _offset = difference;
                }

                _content = e.Point;
            };
        }

        public Avalonia.PixelPoint FramePosition =>
            _content is { } content && _offset is { } offset ? content - offset : _window.Position;
    }

    /// <summary>
    /// Aktiviert ein Fenster und hält es kurz im Vordergrund, damit der Fenstermanager es nach vorn stellt.
    /// </summary>
    /// <param name="window">Das gerade geöffnete Fenster.</param>
    public static void BringToFront(Window window)
    {
        window.Activate();
        if (window.Topmost)
        {
            return;
        }

        window.Topmost = true;
        Dispatcher.UIThread.Post(() =>
        {
            window.Topmost = false;
            window.Activate();
        }, DispatcherPriority.Background);
    }
}
