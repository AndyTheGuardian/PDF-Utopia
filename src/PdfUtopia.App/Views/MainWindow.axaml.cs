using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;

namespace PdfUtopia.App.Views;

/// <summary>
/// Hauptfenster. Der Code-Behind enthält nur Oberflächenlogik: Dateien per Drag &amp; Drop annehmen, Zoom als
/// Layout-Transformation anwenden, mit Mausrad und Tasten blättern, Fußnotentexte übernehmen, vor dem Schließen nach
/// ungespeicherten Änderungen fragen. Das Umsortieren der Seiten übernimmt <see cref="ThumbnailDragController"/>.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Rand um die Seite im Anzeigebereich (wie <c>ZoomHost.Margin</c>).</summary>
    private const double PageMargin = 32;

    /// <summary>Platz für die senkrechte Bildlaufleiste.</summary>
    private const double ScrollBarAllowance = 16;

    private readonly PageNavigator _navigator = new();
    private bool _closeConfirmed;
    private DocumentViewModel? _watchedDocument;

    /// <summary>
    /// Initialisiert das Fenster.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnFileDragOver);
        AddHandler(DragDrop.DropEvent, OnFileDrop);
        // Vor der Bildlaufleiste auswerten: am Rand blättern statt (wirkungslos) weiterscrollen
        PageScroller.AddHandler(PointerWheelChangedEvent, OnPageWheel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnNavigationKeyDown, RoutingStrategies.Tunnel);
        _ = new ThumbnailDragController(ThumbnailList, DragLayer, DragGhost, DropLine,
            index => ViewModel?.ActiveDocument?.Show(index),
            (from, to) => ViewModel?.ActiveDocument?.MovePage(from, to));
        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    /// <summary>Das ViewModel des Fensters.</summary>
    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    /// <summary>
    /// Fragt vor dem Schließen nach ungespeicherten Änderungen.
    /// </summary>
    /// <param name="e">Ereignisdaten.</param>
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || ViewModel is not { } viewModel || !viewModel.Documents.Any(document => document.Document.IsModified))
        {
            return;
        }

        e.Cancel = true;
        if (await viewModel.ConfirmCloseAllAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    /// <summary>
    /// Wendet den Zoom an und beobachtet Änderungen, die eine neue Anpassung an den Anzeigebereich erfordern:
    /// Fenstergröße (auch Maximieren), Ansicht, Dokument, Seite und Drehung (A33).
    /// </summary>
    private void OnViewModelChanged()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        ApplyZoom(viewModel.Zoom);
        PageScroller.SizeChanged += (_, _) => Refit();
        viewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainWindowViewModel.Zoom):
                    ApplyZoom(viewModel.Zoom);
                    break;
                case nameof(MainWindowViewModel.FitMode):
                    Refit();
                    break;
                case nameof(MainWindowViewModel.ActiveDocument):
                    WatchDocument(viewModel.ActiveDocument);
                    Refit();
                    break;
            }
        };
        WatchDocument(viewModel.ActiveDocument);
    }

    /// <summary>
    /// Beobachtet Seitenwechsel und Drehungen des angezeigten Dokuments.
    /// </summary>
    private void WatchDocument(DocumentViewModel? document)
    {
        if (_watchedDocument is not null)
        {
            _watchedDocument.PropertyChanged -= OnWatchedDocumentPropertyChanged;
            _watchedDocument.CurrentPageAnnotationsChanged -= OnWatchedDocumentPageChanged;
        }

        _watchedDocument = document;
        if (document is not null)
        {
            document.PropertyChanged += OnWatchedDocumentPropertyChanged;
            document.CurrentPageAnnotationsChanged += OnWatchedDocumentPageChanged;
        }
    }

    /// <summary>
    /// Neue Seite: neu anpassen.
    /// </summary>
    private void OnWatchedDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.CurrentIndex))
        {
            Refit();
        }
    }

    /// <summary>
    /// Seite gedreht oder neu angezeigt: neu anpassen.
    /// </summary>
    private void OnWatchedDocumentPageChanged(object? sender, EventArgs e) => Refit();

    /// <summary>
    /// Berechnet den Zoom der gewählten Ansicht für den aktuellen Anzeigebereich.
    /// </summary>
    private void Refit()
    {
        if (ViewModel is not { ActiveDocument: { } document } viewModel)
        {
            return;
        }

        var (pageWidth, pageHeight) = document.CurrentDisplaySize;
        var viewport = PageScroller.Bounds.Size;
        var zoom = ZoomFit.Calculate(viewModel.FitMode, viewport.Width - 2 * PageMargin - ScrollBarAllowance,
            viewport.Height - 2 * PageMargin, pageWidth, pageHeight);
        if (zoom is { } value)
        {
            viewModel.ApplyFitZoom(value);
        }
    }

    /// <summary>
    /// Skaliert Seite und Fußnoten.
    /// </summary>
    private void ApplyZoom(double zoom) => ZoomHost.LayoutTransform = new ScaleTransform(zoom, zoom);

    /// <summary>
    /// Mausrad in der Seitenansicht: innerhalb der Seite scrollen, am Rand blättern (A38). Strg und Umschalt bleiben
    /// der Bildlaufleiste überlassen.
    /// </summary>
    private void OnPageWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ViewModel?.ActiveDocument is not { } document || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        var step = _navigator.Wheel(e.Delta.Y, CurrentScrollPosition(), document.CurrentIndex, document.Document.Pages.Count,
            TimeSpan.FromMilliseconds(e.Timestamp));
        e.Handled = Navigate(document, step);
    }

    /// <summary>
    /// Pfeiltasten, Bild auf/ab, Pos1 und Ende blättern (A38) – außer in Bedienelementen, die diese Tasten selbst
    /// brauchen (Textfelder, Schieberegler, Auswahllisten, Menüs).
    /// </summary>
    private void OnNavigationKeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel?.ActiveDocument is not { } document || e.KeyModifiers != KeyModifiers.None || ToNavigationKey(e.Key) is not { } key
            || FocusManager?.GetFocusedElement() is TextBox or RangeBase or ComboBox or NumericUpDown or MenuItem)
        {
            return;
        }

        var step = PageNavigator.Key(key, CurrentScrollPosition(), document.CurrentIndex, document.Document.Pages.Count);
        if (step.Kind == NavigationKind.Scroll)
        {
            if (key == NavigationKey.Up)
            {
                PageScroller.LineUp();
            }
            else
            {
                PageScroller.LineDown();
            }
        }

        Navigate(document, step);
        e.Handled = true;
    }

    /// <summary>Taste zur Navigation oder <c>null</c>.</summary>
    private static NavigationKey? ToNavigationKey(Key key) => key switch
    {
        Key.Up => NavigationKey.Up,
        Key.Down => NavigationKey.Down,
        Key.Left => NavigationKey.Left,
        Key.Right => NavigationKey.Right,
        Key.PageUp => NavigationKey.PageUp,
        Key.PageDown => NavigationKey.PageDown,
        Key.Home => NavigationKey.Home,
        Key.End => NavigationKey.End,
        _ => null,
    };

    /// <summary>Lage der senkrechten Bildlaufleiste der Seitenansicht.</summary>
    private ScrollPosition CurrentScrollPosition() =>
        ScrollPosition.From(PageScroller.Offset.Y, PageScroller.Extent.Height, PageScroller.Viewport.Height);

    /// <summary>
    /// Führt einen Navigationsschritt aus. Die neue Seite wird nach dem Layout oben bzw. unten gezeigt.
    /// </summary>
    /// <returns>Ob die Eingabe verbraucht ist (nicht an die Bildlaufleiste weitergeben).</returns>
    private bool Navigate(DocumentViewModel document, NavigationStep step)
    {
        if (step.Kind != NavigationKind.GoTo)
        {
            return step.Kind == NavigationKind.Ignore;
        }

        document.Show(step.TargetIndex);
        Dispatcher.UIThread.Post(() =>
        {
            var bottom = Math.Max(0, PageScroller.Extent.Height - PageScroller.Viewport.Height);
            PageScroller.Offset = new Vector(PageScroller.Offset.X, step.LandAtBottom ? bottom : 0);
        }, DispatcherPriority.Loaded);
        return true;
    }

    /// <summary>
    /// Zeigt beim Ziehen von Dateien das Kopieren-Symbol.
    /// </summary>
    private void OnFileDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    /// <summary>
    /// Öffnet abgelegte PDF-Dateien.
    /// </summary>
    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?.Select(item => item.TryGetLocalPath()).OfType<string>().ToList();
        if (paths is { Count: > 0 } && ViewModel is { } viewModel)
        {
            await viewModel.OpenFilesAsync(paths);
        }
    }

    /// <summary>
    /// Übernimmt einen bearbeiteten Fußnotentext beim Verlassen des Felds (rückgängig machbar).
    /// </summary>
    private void OnFootnoteLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: FootnoteViewModel footnote })
        {
            footnote.Commit();
        }
    }

    /// <summary>
    /// Setzt den Fokus in das Suchfeld (Menü „Suchen“, Strg+F).
    /// </summary>
    private void OnFindClick(object? sender, RoutedEventArgs e) => FocusSearch();

    /// <summary>
    /// Strg+F setzt den Fokus in das Suchfeld.
    /// </summary>
    /// <param name="e">Ereignisdaten.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            FocusSearch();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Fokussiert das Suchfeld und markiert den Inhalt.
    /// </summary>
    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    /// <summary>
    /// Öffnet die Farbwahl am Farbpunkt (Menü „Ansicht → Akzentfarbe…“).
    /// </summary>
    private void OnAccentMenuClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.PrepareColorPickerCommand.Execute(ColorTarget.Accent);
        AccentButton.Flyout?.ShowAt(AccentButton);
    }

    /// <summary>
    /// Beendet die Anwendung.
    /// </summary>
    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
