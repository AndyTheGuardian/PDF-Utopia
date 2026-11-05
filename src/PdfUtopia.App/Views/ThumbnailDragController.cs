using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using PdfUtopia.App.ViewModels;

namespace PdfUtopia.App.Views;

/// <summary>
/// Oberflächenmechanik der Seitenliste (F6): Ein Klick zeigt die Seite an, Ziehen sortiert sie um. Ein Abbild
/// der Seite klebt am Mauszeiger, die Seite selbst bleibt blass an ihrem Platz, eine Linie in Akzentfarbe zeigt
/// die Einfügestelle; verschoben wird erst beim Loslassen (wie FileRenamer). Klicks auf die Schaltflächen Drehen und Löschen bleiben unberührt.
/// </summary>
public sealed class ThumbnailDragController
{
    private const string DraggingClass = "dragging";
    private const double DragThreshold = 6;

    private readonly ItemsControl _list;
    private readonly Visual _layer;
    private readonly Control _ghost;
    private readonly Control _dropLine;
    private readonly Action<int, KeyModifiers> _show;
    private readonly Action<int, int> _move;

    private Control? _container;
    private Point _pressPosition;
    private Point _grabOffset;
    private int _fromIndex = -1;
    private int _targetIndex = -1;
    private bool _dragging;
    private KeyModifiers _modifiers;

    /// <summary>
    /// Erzeugt den Controller und registriert die Zeigerereignisse an der Liste.
    /// </summary>
    /// <param name="list">Die Seitenliste.</param>
    /// <param name="layer">Ebene für Abbild und Einfügelinie (Canvas über der Liste).</param>
    /// <param name="ghost">Abbild der gezogenen Seite.</param>
    /// <param name="dropLine">Die Einfügelinie.</param>
    /// <param name="show">Klick auf die Seite mit diesem Index (mit Strg/Umschalt für die Mehrfachauswahl).</param>
    /// <param name="move">Verschiebt eine Seite von Index 1 nach Index 2.</param>
    public ThumbnailDragController(ItemsControl list, Visual layer, Control ghost, Control dropLine, Action<int, KeyModifiers> show, Action<int, int> move)
    {
        _list = list;
        _layer = layer;
        _ghost = ghost;
        _dropLine = dropLine;
        _show = show;
        _move = move;
        list.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
        list.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel);
        list.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel);
        list.PointerCaptureLost += (_, _) => Reset();
    }

    /// <summary>
    /// Merkt sich die gedrückte Seite (nicht bei Klicks auf Schaltflächen).
    /// </summary>
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (FindContainer(e.Source as Visual) is not { } container || !e.GetCurrentPoint(_list).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _container = container;
        _fromIndex = _targetIndex = _list.IndexFromContainer(container);
        _pressPosition = e.GetPosition(_list);
        _modifiers = e.KeyModifiers;
        _grabOffset = e.GetPosition(container);
        e.Pointer.Capture(_list);
        e.Handled = true;
    }

    /// <summary>
    /// Beginnt nach einer kleinen Bewegung das Ziehen und aktualisiert die Einfügestelle.
    /// </summary>
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_container is null)
        {
            return;
        }

        var position = e.GetPosition(_list);
        if (!_dragging && Math.Abs(position.Y - _pressPosition.Y) + Math.Abs(position.X - _pressPosition.X) < DragThreshold)
        {
            return;
        }

        if (!_dragging)
        {
            _dragging = true;
            _container.Classes.Add(DraggingClass);
            _ghost.DataContext = _container.DataContext;
            _ghost.Width = _container.Bounds.Width;
            _ghost.IsVisible = true;
        }

        PlaceGhost(e.GetPosition(_layer));
        _targetIndex = FindIndexAt(position.Y);
        PlaceDropLine();
        e.Handled = true;
    }

    /// <summary>
    /// Verschiebt beim Loslassen oder zeigt nach einem Klick die Seite an.
    /// </summary>
    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_container is null)
        {
            return;
        }

        var (from, to, dragging) = (_fromIndex, _targetIndex, _dragging);
        Reset();
        e.Pointer.Capture(null);
        if (dragging)
        {
            _move(from, to);
        }
        else
        {
            _show(from, _modifiers);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Setzt den Zustand zurück.
    /// </summary>
    private void Reset()
    {
        _container?.Classes.Remove(DraggingClass);
        _container = null;
        _dragging = false;
        _dropLine.IsVisible = false;
        _ghost.IsVisible = false;
        _ghost.DataContext = null;
    }

    /// <summary>
    /// Setzt das Abbild so, dass es an derselben Stelle am Mauszeiger hängt, an der die Seite gegriffen wurde.
    /// </summary>
    private void PlaceGhost(Point pointer)
    {
        Canvas.SetLeft(_ghost, pointer.X - _grabOffset.X);
        Canvas.SetTop(_ghost, pointer.Y - _grabOffset.Y);
    }

    /// <summary>
    /// Sucht den Listeneintrag zu einem angeklickten Element.
    /// </summary>
    private Control? FindContainer(Visual? source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Control { DataContext: PageThumbnailViewModel } control && _list.IndexFromContainer(control) >= 0)
            {
                return control;
            }
        }

        return null;
    }

    /// <summary>
    /// Linie über dem Zieleintrag (nach oben ziehen) bzw. darunter (nach unten ziehen).
    /// </summary>
    private void PlaceDropLine()
    {
        if (_targetIndex == _fromIndex || _list.ContainerFromIndex(_targetIndex) is not { } target ||
            target.TranslatePoint(default, _layer) is not { } origin)
        {
            _dropLine.IsVisible = false;
            return;
        }

        var y = _targetIndex < _fromIndex ? origin.Y - 4 : origin.Y + target.Bounds.Height - 4;
        Canvas.SetLeft(_dropLine, origin.X + 8);
        Canvas.SetTop(_dropLine, y);
        _dropLine.Width = Math.Max(0, target.Bounds.Width - 16);
        _dropLine.IsVisible = true;
    }

    /// <summary>
    /// Zeigt beim Ziehen einer Datei auf die Seitenliste die Einfügelinie (A41, F26).
    /// </summary>
    /// <param name="y">Senkrechte Position relativ zur Liste.</param>
    /// <returns>Position, an der eingefügt würde (0 bis Anzahl der Einträge).</returns>
    public int ShowInsertLine(double y)
    {
        var count = _list.ItemCount;
        var index = count;
        Control? anchor = null;
        for (var i = 0; i < count; i++)
        {
            if (_list.ContainerFromIndex(i) is not { } container || container.TranslatePoint(default, _list) is not { } origin)
            {
                continue;
            }

            anchor = container;
            if (y < origin.Y + container.Bounds.Height / 2)
            {
                index = i;
                break;
            }
        }

        if (anchor is null || anchor.TranslatePoint(default, _layer) is not { } position)
        {
            _dropLine.IsVisible = false;
            return index;
        }

        var lineY = index < count ? position.Y - 4 : position.Y + anchor.Bounds.Height - 4;
        Canvas.SetLeft(_dropLine, position.X + 8);
        Canvas.SetTop(_dropLine, lineY);
        _dropLine.Width = Math.Max(0, anchor.Bounds.Width - 16);
        _dropLine.IsVisible = true;
        return index;
    }

    /// <summary>
    /// Blendet die Einfügelinie aus.
    /// </summary>
    public void HideInsertLine() => _dropLine.IsVisible = false;

    /// <summary>
    /// Index des Eintrags an einer vertikalen Position (begrenzt auf erste/letzte Position).
    /// </summary>
    private int FindIndexAt(double y)
    {
        var count = _list.ItemCount;
        for (var index = 0; index < count; index++)
        {
            if (_list.ContainerFromIndex(index) is { } container && container.TranslatePoint(default, _list) is { } origin &&
                y < origin.Y + container.Bounds.Height)
            {
                return index;
            }
        }

        return Math.Max(count - 1, 0);
    }
}
