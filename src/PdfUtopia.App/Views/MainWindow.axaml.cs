using Avalonia.VisualTree;
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
/// Layout-Transformation anwenden, mit Mausrad und Tasten blättern, in den fortlaufenden Ansichten zur Seite scrollen
/// und die aktuelle Seite mitführen (A55), Fußnotentexte übernehmen, vor dem Schließen nach ungespeicherten Änderungen
/// fragen. Das Umsortieren der Seiten übernimmt <see cref="ThumbnailDragController"/>.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Rand um die Seite im Anzeigebereich (wie <c>ZoomHost.Margin</c>).</summary>
    private const double PageMargin = 32;

    /// <summary>Platz für die senkrechte Bildlaufleiste.</summary>
    private const double ScrollBarAllowance = 16;

    private readonly PageNavigator _navigator = new();
    private bool _closeConfirmed;
    private bool _scrollingToPage;
    private DocumentViewModel? _watchedDocument;
    private readonly ThumbnailDragController _thumbnailDrag;

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
        PageScroller.ScrollChanged += (_, _) => TrackCurrentPage();
        StatusBarGrid.SizeChanged += (_, _) => FitViewLabels();
        AddHandler(KeyDownEvent, OnNavigationKeyDown, RoutingStrategies.Tunnel);
        // Strg-Zustand für „Drehen gegen den Uhrzeigersinn“ (auch wenn ein Textfeld die Taste verarbeitet)
        AddHandler(KeyDownEvent, (_, e) => TrackControlKey(e.Key, e.KeyModifiers, pressed: true), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(KeyUpEvent, (_, e) => TrackControlKey(e.Key, e.KeyModifiers, pressed: false), RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerMovedEvent, (_, e) => SetReverseRotation(e.KeyModifiers.HasFlag(KeyModifiers.Control)), RoutingStrategies.Tunnel, handledEventsToo: true);
        Deactivated += (_, _) => SetReverseRotation(false);
        _thumbnailDrag = new ThumbnailDragController(ThumbnailList, DragLayer, DragGhost, DropLine,
            (index, modifiers) =>
            {
                ViewModel?.ActiveDocument?.ClickPage(index, modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta),
                    modifiers.HasFlag(KeyModifiers.Shift));
                // Tasten der Seitenliste (Strg+A, Entf, Esc) wirken, sobald sie angeklickt wurde
                PageListScroller.Focus();
            },
            (from, to) => ViewModel?.ActiveDocument?.MovePage(from, to));
        AddHandler(DragDrop.DragLeaveEvent, (_, _) =>
        {
            _thumbnailDrag.HideInsertLine();
            _thumbnailDrag.UpdateAutoScroll(null);
        });
        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    /// <summary>Spalte der linken Leiste (F30).</summary>
    private ColumnDefinition SidebarColumn => MainArea.ColumnDefinitions[0];

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

        SidebarColumn.Width = new GridLength(viewModel.SidebarWidth);

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
                    RevealCurrentThumbnail();
                    ScrollToCurrentPage();
                    FitViewLabels();
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
            _watchedDocument.LayoutChanged -= OnWatchedDocumentLayoutChanged;
            _watchedDocument.PageShown -= OnWatchedDocumentPageShown;
        }

        _watchedDocument = document;
        if (document is not null)
        {
            document.PropertyChanged += OnWatchedDocumentPropertyChanged;
            document.LayoutChanged += OnWatchedDocumentLayoutChanged;
            document.PageShown += OnWatchedDocumentPageShown;
        }
    }

    /// <summary>
    /// Neue aktuelle Seite: in der Seitenliste sichtbar machen.
    /// </summary>
    private void OnWatchedDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DocumentViewModel.CurrentIndex))
        {
            RevealCurrentThumbnail();
        }
    }

    /// <summary>
    /// Andere Zeilen oder Seitengrößen (Seitenwechsel bei Einzel- und Doppelseite, Drehung, Ansicht): neu anpassen.
    /// </summary>
    private void OnWatchedDocumentLayoutChanged(object? sender, EventArgs e) => Refit();

    /// <summary>
    /// Seite gezielt angezeigt (Miniatur, Lesezeichen, Anmerkungsübersicht, Blättern): dorthin scrollen.
    /// </summary>
    private void OnWatchedDocumentPageShown(object? sender, EventArgs e) => ScrollToCurrentPage();

    /// <summary>
    /// Scrollt die Seitenliste so weit, dass die angezeigte Seite ganz sichtbar ist (A40). Ist sie schon sichtbar,
    /// bewegt sich die Liste nicht. Erst nach dem Layout, damit neu erzeugte Einträge ihre Position kennen.
    /// </summary>
    private void RevealCurrentThumbnail() => Dispatcher.UIThread.Post(() =>
    {
        if (_watchedDocument is { } document && ThumbnailList.ContainerFromIndex(document.CurrentIndex) is { } container)
        {
            container.BringIntoView();
        }
    }, DispatcherPriority.Loaded);

    /// <summary>
    /// Berechnet den Zoom der gewählten Ansicht für den aktuellen Anzeigebereich.
    /// </summary>
    private void Refit()
    {
        if (ViewModel is not { ActiveDocument: { } document } viewModel)
        {
            return;
        }

        var (pageWidth, pageHeight) = ReferenceSize(document);
        var viewport = PageScroller.Bounds.Size;
        var zoom = ZoomFit.Calculate(viewModel.FitMode, viewport.Width - 2 * PageMargin - ScrollBarAllowance,
            viewport.Height - 2 * PageMargin, pageWidth, pageHeight);
        if (zoom is { } value)
        {
            viewModel.ApplyFitZoom(value);
        }
    }

    /// <summary>
    /// Größe, an die der Zoom angepasst wird (A55): die Zeile der aktuellen Seite (eine Seite oder ein Seitenpaar);
    /// in den fortlaufenden Ansichten die größte Zeile, damit der Zoom beim Scrollen und Drehen gleich bleibt.
    /// </summary>
    private static (double Width, double Height) ReferenceSize(DocumentViewModel document)
    {
        var mode = document.ViewMode;
        var rows = document.Rows.Select(row => row.Row).ToList();
        if (rows.Count == 0)
        {
            return document.CurrentView.DisplaySize;
        }

        (double Width, double Height) Size(PageRow row) => PageLayout.RowSize(row, index => document.ViewFor(document.Document.Pages[index].Id).DisplaySize);
        if (!PageLayout.IsContinuous(mode))
        {
            return Size(rows[PageLayout.RowOf(rows, document.CurrentIndex)]);
        }

        var sizes = rows.Select(Size).ToList();
        return (sizes.Max(size => size.Width), sizes.Max(size => size.Height));
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
        // In den fortlaufenden Ansichten scrollt das Mausrad einfach weiter
        if (ViewModel?.ActiveDocument is not { } document || e.KeyModifiers != KeyModifiers.None || PageLayout.IsContinuous(document.ViewMode))
        {
            return;
        }

        var (row, rows) = CurrentRow(document);
        var step = _navigator.Wheel(e.Delta.Y, CurrentScrollPosition(), row, rows.Count, TimeSpan.FromMilliseconds(e.Timestamp));
        e.Handled = Navigate(document, step, rows);
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

        // Blättern geht zeilenweise: Seite bzw. Seitenpaar; in den fortlaufenden Ansichten scrollen die Pfeiltasten
        // auf und ab immer, die übrigen Tasten springen zur Zeile
        var (row, rows) = CurrentRow(document);
        var position = PageLayout.IsContinuous(document.ViewMode) ? new ScrollPosition(false, false) : CurrentScrollPosition();
        var step = PageNavigator.Key(key, position, row, rows.Count);
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

        Navigate(document, step, rows);
        e.Handled = true;
    }

    /// <summary>Zeile der aktuellen Seite und alle Zeilen der gewählten Ansicht.</summary>
    private static (int Row, IReadOnlyList<PageRow> Rows) CurrentRow(DocumentViewModel document)
    {
        var rows = PageLayout.Rows(document.ViewMode, document.Document.Pages.Count, document.FirstPageAlone);
        return (PageLayout.RowOf(rows, document.CurrentIndex), rows);
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
    /// Führt einen Navigationsschritt aus (Ziel ist eine Zeile). Bei Einzel- und Doppelseite wird die neue Zeile nach
    /// dem Layout oben bzw. unten gezeigt; in den fortlaufenden Ansichten wird zu ihr gescrollt.
    /// </summary>
    /// <returns>Ob die Eingabe verbraucht ist (nicht an die Bildlaufleiste weitergeben).</returns>
    private bool Navigate(DocumentViewModel document, NavigationStep step, IReadOnlyList<PageRow> rows)
    {
        if (step.Kind != NavigationKind.GoTo)
        {
            return step.Kind == NavigationKind.Ignore;
        }

        document.Show(rows[Math.Clamp(step.TargetIndex, 0, rows.Count - 1)].First);
        if (!PageLayout.IsContinuous(document.ViewMode))
        {
            Dispatcher.UIThread.Post(() =>
            {
                var bottom = Math.Max(0, PageScroller.Extent.Height - PageScroller.Viewport.Height);
                PageScroller.Offset = new Vector(PageScroller.Offset.X, step.LandAtBottom ? bottom : 0);
            }, DispatcherPriority.Loaded);
        }

        return true;
    }

    /// <summary>
    /// Zeigt die aktuelle Seite: in den fortlaufenden Ansichten wird ihre Zeile oben in die Ansicht gescrollt (zweimal,
    /// weil die Höhen noch nicht aufgebauter Zeilen erst geschätzt sind), sonst beginnt die Ansicht oben.
    /// </summary>
    private void ScrollToCurrentPage()
    {
        if (_watchedDocument is not { } document)
        {
            return;
        }

        if (!PageLayout.IsContinuous(document.ViewMode))
        {
            Dispatcher.UIThread.Post(() => PageScroller.Offset = new Vector(PageScroller.Offset.X, 0), DispatcherPriority.Loaded);
            return;
        }

        var row = document.Rows.ToList().FindIndex(item => item.Row.Contains(document.CurrentIndex));
        if (row < 0)
        {
            return;
        }

        _scrollingToPage = true;
        PageRows.ScrollIntoView(row);
        void Align(int pass) => Dispatcher.UIThread.Post(() =>
        {
            if (PageRows.ContainerFromIndex(row) is { } container && container.TranslatePoint(default, PageScroller) is { } top)
            {
                PageScroller.Offset = new Vector(PageScroller.Offset.X, Math.Max(0, PageScroller.Offset.Y + top.Y - PageLayout.Gap));
            }

            if (pass < 2)
            {
                Align(pass + 1);
            }
            else
            {
                _scrollingToPage = false;
            }
        }, DispatcherPriority.Loaded);
        Align(1);
    }

    /// <summary>
    /// Fortlaufende Ansichten: beim Scrollen wird die Seite aktuell, deren Zeile den größten Teil der Ansicht einnimmt;
    /// steht die bisherige aktuelle Seite in dieser Zeile, bleibt sie es (A55).
    /// </summary>
    private void TrackCurrentPage()
    {
        if (_scrollingToPage || _watchedDocument is not { } document || !PageLayout.IsContinuous(document.ViewMode))
        {
            return;
        }

        var viewport = new Rect(PageScroller.Bounds.Size);
        PageRowViewModel? best = null;
        var bestArea = 0.0;
        foreach (var container in PageRows.GetRealizedContainers())
        {
            if (container.DataContext is not PageRowViewModel row || container.TransformToVisual(PageScroller) is not { } transform)
            {
                continue;
            }

            var visible = new Rect(container.Bounds.Size).TransformToAABB(transform).Intersect(viewport);
            var area = visible.Width * visible.Height;
            if (area > bestArea)
            {
                (best, bestArea) = (row, area);
            }
        }

        if (best is not null && !best.Row.Contains(document.CurrentIndex))
        {
            document.Activate(best.Row.First);
        }
    }

    /// <summary>
    /// Beschriftung der Ansichtsknöpfe nur, wenn die Statusleiste genug Platz hat; sonst nur Symbole mit Tooltip (A55).
    /// </summary>
    private void FitViewLabels()
    {
        ViewModeButtons.Classes.Set("compact", false);
        var needed = 0.0;
        foreach (var child in StatusBarGrid.Children.Where(child => child != StatusNoticeText && child.IsVisible))
        {
            child.Measure(Size.Infinity);
            needed += child.DesiredSize.Width;
        }

        ViewModeButtons.Classes.Set("compact", needed > StatusBarGrid.Bounds.Width);
    }

    /// <summary>
    /// Zeigt beim Ziehen von Dateien das Kopieren-Symbol; über der Seitenliste zusätzlich die Einfügelinie.
    /// </summary>
    private void OnFileDragOver(object? sender, DragEventArgs e)
    {
        var isFile = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = isFile ? DragDropEffects.Copy : DragDropEffects.None;
        if (isFile && IsOverPageList(e))
        {
            _thumbnailDrag.ShowInsertLine(e.GetPosition(ThumbnailList).Y);
            _thumbnailDrag.UpdateAutoScroll(ThumbnailList.FindAncestorOfType<ScrollViewer>() is { } viewer ? e.GetPosition(viewer) : null);
        }
        else
        {
            _thumbnailDrag.HideInsertLine();
            _thumbnailDrag.UpdateAutoScroll(null);
        }
    }

    /// <summary>
    /// Öffnet abgelegte PDF-Dateien; auf der Seitenliste abgelegt, werden ihre Seiten an dieser Stelle
    /// eingefügt (A41, F26).
    /// </summary>
    private async void OnFileDrop(object? sender, DragEventArgs e)
    {
        _thumbnailDrag.HideInsertLine();
        _thumbnailDrag.UpdateAutoScroll(null);
        var paths = e.DataTransfer.TryGetFiles()?.Select(item => item.TryGetLocalPath()).OfType<string>().ToList();
        if (paths is not { Count: > 0 } || ViewModel is not { } viewModel)
        {
            return;
        }

        if (IsOverPageList(e))
        {
            await viewModel.InsertPagesFromFilesAsync(paths, _thumbnailDrag.ShowInsertLine(e.GetPosition(ThumbnailList).Y));
            _thumbnailDrag.HideInsertLine();
        }
        else
        {
            await viewModel.OpenFilesAsync(paths);
        }
    }

    /// <summary>
    /// Ob sich der Mauszeiger über der Seitenliste eines geöffneten Dokuments befindet.
    /// </summary>
    private bool IsOverPageList(DragEventArgs e) =>
        ViewModel?.ActiveDocument is not null && PageListScroller.IsVisible &&
        new Rect(PageListScroller.Bounds.Size).Contains(e.GetPosition(PageListScroller));

    /// <summary>
    /// Linke Leiste ändert ihre Breite (auch während des Ziehens): Miniaturen mitwachsen lassen.
    /// </summary>
    private void OnSidebarSizeChanged(object? sender, SizeChangedEventArgs e) => ViewModel?.ResizeSidebar(e.NewSize.Width);

    /// <summary>
    /// Linke Leiste per Ziehen verbreitert oder verschmälert: Breite merken (F30).
    /// </summary>
    private void OnSidebarResized(object? sender, VectorEventArgs e) => ViewModel?.SaveSidebarWidth(SidebarColumn.ActualWidth);

    /// <summary>
    /// Merkt sich, ob Strg gedrückt ist: beim Drücken/Loslassen der Strg-Taste selbst und bei jeder anderen Taste.
    /// </summary>
    private void TrackControlKey(Key key, KeyModifiers modifiers, bool pressed) =>
        SetReverseRotation(key is Key.LeftCtrl or Key.RightCtrl ? pressed : modifiers.HasFlag(KeyModifiers.Control));

    /// <summary>
    /// Setzt die Drehrichtung der Drehen-Knöpfe (nur bei Änderung, damit Bindungen nicht unnötig auslösen).
    /// </summary>
    private void SetReverseRotation(bool reverse)
    {
        if (ViewModel is { } viewModel && viewModel.ReverseRotation != reverse)
        {
            viewModel.ReverseRotation = reverse;
        }
    }

    /// <summary>
    /// Zoom-Eingabe verlassen, ohne Enter zu drücken: aktuellen Zoom wieder anzeigen.
    /// </summary>
    private void OnZoomBoxLostFocus(object? sender, RoutedEventArgs e) => ViewModel?.ResetZoomInputCommand.Execute(null);

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

    /// <summary>Farbwahl geschlossen: einem gerade bearbeiteten Text den Fokus zurückgeben (P10).</summary>
    private void OnColorFlyoutClosed(object? sender, EventArgs e)
    {
        foreach (var editor in PageRows.GetVisualDescendants().OfType<Controls.PageEditor>())
        {
            editor.ResumeTextEditing();
        }
    }

    /// <summary>Schließt die Stempelauswahl, sobald ein Stempel gewählt oder „+ Eigener Text…“ angeklickt ist.</summary>
    private void OnStampChosen(object? sender, RoutedEventArgs e) =>
        // Erst nach dem Befehl schließen; sonst verliert die Schaltfläche ihre Bindung, bevor er ausgeführt wird
        Avalonia.Threading.Dispatcher.UIThread.Post(() => StampPicker.Flyout?.Hide());

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
