using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using PdfUtopia.App.Localization;
using PdfUtopia.App.Services;
using PdfUtopia.App.ViewModels;
using PdfUtopia.App.Views;
using PdfUtopia.Core.Settings;

namespace PdfUtopia.App;

/// <summary>
/// Avalonia-Anwendung: lädt Ressourcen, verdrahtet Dienste und öffnet das Hauptfenster. Nach einem
/// Sprachwechsel wird das Hauptfenster mit demselben ViewModel neu aufgebaut (wie FileRenamer).
/// </summary>
public sealed class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private DialogService? _dialogs;

    /// <summary>
    /// Lädt die XAML-Ressourcen der Anwendung.
    /// </summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Stellt die Sprache ein, erzeugt Hauptfenster und ViewModel und öffnet übergebene Dateien.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new SettingsStore(SettingsStore.DefaultPath());
            Loc.Language = store.Load().ResolveLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);

            var window = new MainWindow();
            _desktop = desktop;
            _dialogs = new DialogService(window);
            var viewModel = new MainWindowViewModel(store, _dialogs, new ThemeService(this));
            viewModel.LanguageChanged += OnLanguageChanged;
            window.DataContext = viewModel;
            desktop.MainWindow = window;
            if (desktop.Args is { Length: > 0 } args)
            {
                window.Opened += async (_, _) => await viewModel.OpenFilesAsync(args);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Baut das Fenster erst nach dem laufenden Menü-Klick neu auf.
    /// </summary>
    private void OnLanguageChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() => RebuildMainWindow(sender));

    /// <summary>
    /// Ersetzt das Hauptfenster durch ein neu aufgebautes in der neuen Sprache; Größe, Lage und ViewModel bleiben.
    /// </summary>
    private void RebuildMainWindow(object? viewModel)
    {
        if (_desktop?.MainWindow is not MainWindow oldWindow || _dialogs is null)
        {
            return;
        }

        var newWindow = new MainWindow
        {
            DataContext = viewModel,
            Width = oldWindow.Width,
            Height = oldWindow.Height,
            Position = oldWindow.Position,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowState = oldWindow.WindowState,
        };
        _dialogs.Owner = newWindow;
        _desktop.MainWindow = newWindow;
        newWindow.Show();
        oldWindow.DataContext = null;
        oldWindow.Close();
    }
}
