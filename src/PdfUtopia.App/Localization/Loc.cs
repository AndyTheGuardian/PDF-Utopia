using System.Globalization;
using Avalonia.Markup.Xaml;
using PdfUtopia.Core.Settings;

namespace PdfUtopia.App.Localization;

/// <summary>
/// Liefert die Texte der Oberfläche in der aktuellen Sprache (F9). Bei einem Sprachwechsel baut die App das
/// Hauptfenster neu auf (wie FileRenamer).
/// </summary>
public static class Loc
{
    /// <summary>Aktuelle Sprache.</summary>
    public static UiLanguage Language { get; set; } = UiLanguage.German;

    /// <summary>
    /// Liefert einen Text.
    /// </summary>
    /// <param name="key">Schlüssel.</param>
    /// <returns>Der Text; bei unbekanntem Schlüssel der Schlüssel selbst (fällt beim Test auf).</returns>
    public static string T(string key) =>
        Strings.All.TryGetValue(key, out var text) ? (Language == UiLanguage.German ? text.German : text.English) : key;

    /// <summary>
    /// Liefert einen Text mit eingesetzten Werten (Platzhalter {0}, {1}, …).
    /// </summary>
    /// <param name="key">Schlüssel.</param>
    /// <param name="values">Die Werte.</param>
    /// <returns>Der Text.</returns>
    public static string F(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(key), values);
}

/// <summary>
/// XAML-Erweiterung für übersetzte Texte: <c>{loc:T Menu.File}</c>.
/// </summary>
/// <param name="key">Schlüssel des Texts.</param>
public sealed class TExtension(string key) : MarkupExtension
{
    /// <summary>Schlüssel des Texts.</summary>
    public string Key { get; } = key;

    /// <summary>
    /// Liefert den Text in der aktuellen Sprache.
    /// </summary>
    /// <param name="serviceProvider">Nicht verwendet.</param>
    /// <returns>Der Text.</returns>
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.T(Key);
}
