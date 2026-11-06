using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Text;

namespace PdfUtopia.Core.Annotations;

/// <summary>
/// Stempel (A51): Text in Großbuchstaben in einem doppelten, abgerundeten Rahmen, darunter optional Datum und Name.
/// Die Größe folgt <see cref="BoxAnnotation.Bounds"/>; das Seitenverhältnis kommt aus <see cref="StampLayout"/>.
/// </summary>
/// <param name="Id">Kennung.</param>
/// <param name="ColorHex">Farbe von Rahmen und Text.</param>
/// <param name="Bounds">Ungedrehtes Rechteck.</param>
/// <param name="Angle">Drehung in Grad im Uhrzeigersinn.</param>
/// <param name="Text">Stempeltext, wie eingegeben (angezeigt in Großbuchstaben).</param>
/// <param name="Detail">Zweite Zeile, z. B. „06.11.2026 · A. Muster“; leer ohne Datum und Name.</param>
public sealed record StampAnnotation(Guid Id, string ColorHex, PdfRect Bounds, double Angle, string Text, string Detail)
    : BoxAnnotation(Id, ColorHex, Bounds, Angle)
{
    /// <summary>Breite eines per Klick gesetzten Stempels in Punkten.</summary>
    public const double DefaultWidth = 150;

    /// <summary>Höchstlänge eigener Stempeltexte.</summary>
    public const int MaxTextLength = 30;

    /// <summary>Wählbare Farben: Grün, Rot, Blau, Schwarz.</summary>
    public static readonly IReadOnlyList<string> Colors = ["#15803D", "#B91C1C", "#1D4ED8", "#111827"];

    /// <summary>Feste Stempel (F34) mit Schlüssel für die Übersetzung und Standardfarbe.</summary>
    public static readonly IReadOnlyList<(string Key, string ColorHex)> Presets =
    [
        ("Approved", Colors[0]), ("Checked", Colors[0]), ("Done", Colors[0]), ("Draft", Colors[2]),
        ("Confidential", Colors[1]), ("Rejected", Colors[1]), ("Copy", Colors[2]),
    ];

    /// <summary>Angezeigter Text in Großbuchstaben.</summary>
    public string Label => Text.ToUpperInvariant();

    /// <summary>Aufbau in natürlicher Größe.</summary>
    public StampLayout Layout => StampLayout.For(Label, Detail);

    /// <summary>
    /// Legt einen Stempel mit der Mitte an einer Stelle an; die Höhe folgt der Breite.
    /// </summary>
    /// <param name="text">Stempeltext.</param>
    /// <param name="detail">Zweite Zeile oder leer.</param>
    /// <param name="colorHex">Farbe.</param>
    /// <param name="center">Mitte.</param>
    /// <param name="width">Breite.</param>
    /// <param name="angle">Drehung in Grad.</param>
    /// <returns>Der Stempel.</returns>
    public static StampAnnotation Create(string text, string detail, string colorHex, PdfPoint center, double width, double angle = 0)
    {
        var layout = StampLayout.For(text.ToUpperInvariant(), detail);
        width = Math.Max(MinWidth, width);
        return new StampAnnotation(Guid.NewGuid(), colorHex, Centered(center, width, width * layout.Height / layout.Width), NormalizeAngle(angle), text, detail);
    }

    /// <summary>
    /// Zweite Zeile aus Datum und Name; fehlende Teile entfallen.
    /// </summary>
    /// <param name="date">Datum (bereits formatiert) oder <c>null</c>.</param>
    /// <param name="name">Name oder <c>null</c>.</param>
    /// <returns>z. B. „06.11.2026 · A. Muster“.</returns>
    public static string DetailLine(string? date, string? name) =>
        string.Join(" · ", new[] { date, name?.Trim() }.Where(part => !string.IsNullOrEmpty(part)));
}

/// <summary>
/// Aufbau eines Stempels in natürlicher Größe (Punkte, Ursprung links oben). Anzeige und gespeicherte PDF zeichnen
/// danach, skaliert auf die Breite des Stempels; die Textbreiten sind die der Helvetica.
/// </summary>
/// <param name="Width">Natürliche Breite.</param>
/// <param name="Height">Natürliche Höhe.</param>
/// <param name="Label">Text in Großbuchstaben.</param>
/// <param name="Detail">Zweite Zeile oder leer.</param>
public sealed record StampLayout(double Width, double Height, string Label, string Detail)
{
    /// <summary>Schriftgröße des Stempeltexts (fett).</summary>
    public const double LabelSize = 20;

    /// <summary>Schriftgröße der zweiten Zeile.</summary>
    public const double DetailSize = 10;

    /// <summary>Abstand des äußeren Rahmens vom Rand (halbe Strichstärke).</summary>
    public const double OuterInset = 1.5;

    /// <summary>Strichstärke des äußeren Rahmens.</summary>
    public const double OuterStroke = 2.5;

    /// <summary>Eckenradius des äußeren Rahmens.</summary>
    public const double OuterRadius = 7;

    /// <summary>Abstand des inneren Rahmens vom Rand.</summary>
    public const double InnerInset = 5;

    /// <summary>Strichstärke des inneren Rahmens.</summary>
    public const double InnerStroke = 1;

    /// <summary>Eckenradius des inneren Rahmens.</summary>
    public const double InnerRadius = 4;

    /// <summary>Deckkraft der Hinterlegung in Stempelfarbe.</summary>
    public const double FillOpacity = 0.06;

    private const double SidePadding = 15;
    private const double HeightWithDetail = 50;
    private const double HeightWithoutDetail = 36;

    /// <summary>Ob es eine zweite Zeile gibt.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>Grundlinie des Stempeltexts vom oberen Rand.</summary>
    public double LabelBaseline => HasDetail ? 26 : 25;

    /// <summary>Grundlinie der zweiten Zeile vom oberen Rand.</summary>
    public double DetailBaseline => 40;

    /// <summary>Linker Rand des zentrierten Stempeltexts.</summary>
    public double LabelLeft => (Width - Helvetica.Width(Label, LabelSize, bold: true)) / 2;

    /// <summary>Linker Rand der zentrierten zweiten Zeile.</summary>
    public double DetailLeft => (Width - Helvetica.Width(Detail, DetailSize)) / 2;

    /// <summary>
    /// Bestimmt den Aufbau aus den Textbreiten.
    /// </summary>
    /// <param name="label">Text in Großbuchstaben.</param>
    /// <param name="detail">Zweite Zeile oder leer.</param>
    /// <returns>Der Aufbau.</returns>
    public static StampLayout For(string label, string detail)
    {
        var textWidth = Math.Max(Helvetica.Width(label, LabelSize, bold: true), Helvetica.Width(detail, DetailSize));
        return new StampLayout(Math.Ceiling(textWidth + 2 * SidePadding), detail.Length > 0 ? HeightWithDetail : HeightWithoutDetail, label, detail);
    }
}
