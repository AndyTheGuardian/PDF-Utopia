using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

namespace PressureProbe;

/// <summary>
/// Einstiegspunkt des Stift-Prototyps.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Startet die Anwendung.
    /// </summary>
    /// <param name="args">Befehlszeilenargumente.</param>
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<ProbeApp>().UsePlatformDetect().LogToTrace().StartWithClassicDesktopLifetime(args);
}

/// <summary>
/// Minimale Anwendung mit einem Fenster aus Zeichenfläche und Messwertanzeige.
/// </summary>
internal sealed class ProbeApp : Application
{
    /// <summary>
    /// Lädt das Fluent-Theme.
    /// </summary>
    public override void Initialize() => Styles.Add(new FluentTheme());

    /// <summary>
    /// Erzeugt das Hauptfenster.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = CreateWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Baut das Fenster: oben die Messwerte, darunter die Zeichenfläche.
    /// </summary>
    /// <returns>Das Fenster.</returns>
    private static Window CreateWindow()
    {
        var live = new TextBlock { FontFamily = new FontFamily("monospace"), Margin = new Thickness(12, 8) };
        var summary = new TextBlock { FontFamily = new FontFamily("monospace"), Margin = new Thickness(12, 0, 12, 8), FontWeight = FontWeight.SemiBold };
        var canvas = new ProbeCanvas();
        canvas.LiveInfoChanged += (_, text) => live.Text = text;
        canvas.SummaryChanged += (_, text) => summary.Text = text;

        var clear = new Button { Content = "Leeren", Margin = new Thickness(12, 8), HorizontalAlignment = HorizontalAlignment.Right };
        clear.Click += (_, _) => canvas.Clear();

        var header = new DockPanel();
        DockPanel.SetDock(clear, Dock.Right);
        header.Children.Add(clear);
        header.Children.Add(new StackPanel { Children = { live, summary } });

        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        root.Children.Add(new Border { Background = new SolidColorBrush(Color.Parse("#F7F8FA")), Child = canvas });

        return new Window { Title = "PDF Utopia – Stift-Prototyp (E4)", Width = 1000, Height = 700, Content = root };
    }
}
