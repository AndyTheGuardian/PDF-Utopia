using Avalonia;

namespace PdfUtopia.App;

/// <summary>
/// Einstiegspunkt der Desktop-Anwendung.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Startet die Anwendung; als Argument übergebene PDF-Dateien werden geöffnet.
    /// </summary>
    /// <param name="args">Befehlszeilenargumente.</param>
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Konfiguriert Avalonia (wird auch vom Designer und für Bildschirmfotos verwendet).
    /// </summary>
    /// <returns>Der konfigurierte AppBuilder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
