using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PdfUtopia.Core.Annotations;
using PdfUtopia.Core.Geometry;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Wandelt Annotationen in JSON und zurück, damit sie beim Wiederöffnen gespeicherter Dateien bearbeitbar sind (F23).
/// Das Format trägt eine Versionsnummer; unbekannte oder beschädigte Daten ergeben <c>null</c>.
/// </summary>
internal static class AnnotationSerializer
{
    /// <summary>Aktuelle Formatversion.</summary>
    public const int Version = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { AddAnnotationTypes } },
        Converters = { new PdfRectConverter() },
    };

    /// <summary>
    /// Wandelt eine Annotation in JSON.
    /// </summary>
    /// <param name="annotation">Die Annotation.</param>
    /// <returns>Das JSON.</returns>
    public static string Serialize(Annotation annotation) => JsonSerializer.Serialize(new Envelope(Version, annotation), Options);

    /// <summary>
    /// Liest eine Annotation aus JSON.
    /// </summary>
    /// <param name="json">Das JSON.</param>
    /// <returns>Die Annotation oder <c>null</c> bei unbekannter Version oder beschädigten Daten.</returns>
    public static Annotation? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
            return envelope is { Version: Version } ? envelope.Annotation : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Macht die Annotationsarten über ein Typfeld unterscheidbar, ohne den Core an JSON zu binden.
    /// </summary>
    private static void AddAnnotationTypes(JsonTypeInfo info)
    {
        if (info.Type != typeof(Annotation))
        {
            return;
        }

        info.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "kind",
            DerivedTypes =
            {
                new JsonDerivedType(typeof(InkAnnotation), "ink"),
                new JsonDerivedType(typeof(HighlightAnnotation), "highlight"),
                new JsonDerivedType(typeof(RectangleAnnotation), "rectangle"),
                new JsonDerivedType(typeof(CommentAnnotation), "comment"),
                new JsonDerivedType(typeof(TextAnnotation), "text"),
                new JsonDerivedType(typeof(LineAnnotation), "line"),
                new JsonDerivedType(typeof(EllipseAnnotation), "ellipse"),
                new JsonDerivedType(typeof(SignatureAnnotation), "signature"),
            },
        };
    }

    /// <summary>
    /// Hülle mit Versionsnummer.
    /// </summary>
    /// <param name="Version">Formatversion.</param>
    /// <param name="Annotation">Die Annotation.</param>
    private sealed record Envelope(int Version, Annotation Annotation);

    /// <summary>
    /// Liest und schreibt <see cref="PdfRect"/> über seinen Konstruktor (System.Text.Json würde bei
    /// Strukturen sonst den leeren Konstruktor verwenden und nur Nullwerte liefern).
    /// </summary>
    private sealed class PdfRectConverter : JsonConverter<PdfRect>
    {
        /// <inheritdoc />
        public override PdfRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var values = JsonSerializer.Deserialize<double[]>(ref reader, options);
            return values is { Length: 4 } ? new PdfRect(values[0], values[1], values[2], values[3]) : throw new JsonException("Rechteck erwartet.");
        }

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, PdfRect value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new[] { value.X, value.Y, value.Width, value.Height }, options);
    }
}
