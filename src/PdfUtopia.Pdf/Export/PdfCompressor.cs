using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.Filters;
using PdfSharp.Pdf.IO;
using PdfUtopia.Core.Compression;
using SkiaSharp;

namespace PdfUtopia.Pdf.Export;

/// <summary>
/// Verkleinert eine PDF-Datei (A57): Bilder werden auf die Auflösung der Stufe verkleinert und als JPEG neu
/// gespeichert, alle übrigen Datenströme mit bester Flate-Komprimierung geschrieben. Bearbeitet werden nur Bilder,
/// die sich verlustfrei ersetzen lassen: 8 Bit Grau oder RGB, DCT- oder Flate-kodiert, ohne Maske. Das Ergebnis ist
/// nie größer als die Eingabe.
/// </summary>
public static class PdfCompressor
{
    private const string ImageSubtype = "/Image";

    /// <summary>
    /// Komprimiert eine ungeschützte PDF-Datei im Speicher.
    /// </summary>
    /// <param name="pdf">Die Datei, z. B. aus <see cref="PdfExporter.SaveUnprotected"/>.</param>
    /// <param name="preset">Die Stufe.</param>
    /// <param name="progress">Fortschritt von 0 bis 1 oder <c>null</c>.</param>
    /// <param name="cancellation">Abbruch.</param>
    /// <returns>Die komprimierte Datei oder <paramref name="pdf"/>, wenn sie nicht kleiner wird.</returns>
    /// <exception cref="OperationCanceledException">Bei Abbruch.</exception>
    public static byte[] Compress(byte[] pdf, CompressionPreset preset, IProgress<double>? progress = null, CancellationToken cancellation = default)
    {
        using var document = PdfReader.Open(new MemoryStream(pdf, writable: false), PdfDocumentOpenMode.Modify);
        document.Options.NoCompression = false;
        document.Options.CompressContentStreams = true;
        document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;

        // Bilddaten nacheinander lesen, rechenintensiv parallel umwandeln, Ergebnisse nacheinander einsetzen
        var jobs = ImageCollector.Collect(document)
            .Select(entry => ImageRecompressor.Prepare(entry.Image, entry.PageSide))
            .OfType<ImageRecompressor.Job>()
            .ToList();
        var results = new ImageRecompressor.Result?[jobs.Count];
        var done = 0;
        var progressLock = new Lock();
        Parallel.For(0, jobs.Count, new ParallelOptions { CancellationToken = cancellation }, index =>
        {
            results[index] = ImageRecompressor.Recompress(jobs[index], preset);
            lock (progressLock)
            {
                progress?.Report(++done / (jobs.Count + 1.0));
            }
        });

        for (var index = 0; index < jobs.Count; index++)
        {
            if (results[index] is { } result)
            {
                ImageRecompressor.Apply(jobs[index].Image, result);
            }
        }

        cancellation.ThrowIfCancellationRequested();
        CompressStreams(document);
        using var buffer = new MemoryStream();
        document.Save(buffer, closeStream: false);
        progress?.Report(1);
        return buffer.Length < pdf.Length ? buffer.ToArray() : pdf;
    }

    /// <summary>
    /// Schreibt eine komprimierte Datei, auf Wunsch mit Passwortschutz. Ein Fehler beim Schreiben lässt eine
    /// vorhandene Zieldatei unversehrt.
    /// </summary>
    /// <param name="pdf">Die ungeschützte Datei.</param>
    /// <param name="targetPath">Zieldatei.</param>
    /// <param name="password">Passwort oder <c>null</c> für eine ungeschützte Datei.</param>
    public static void Save(byte[] pdf, string targetPath, string? password)
    {
        if (password is null)
        {
            WriteAtomically(pdf, targetPath);
            return;
        }

        using var document = PdfReader.Open(new MemoryStream(pdf, writable: false), PdfDocumentOpenMode.Modify);
        PdfExporter.Protect(document, password);
        PdfExporter.SaveAtomically(document, targetPath);
    }

    /// <summary>
    /// Schreibt Bytes erst in eine temporäre Datei und ersetzt dann das Ziel.
    /// </summary>
    private static void WriteAtomically(byte[] data, string targetPath)
    {
        var temporary = $"{targetPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporary, data);
            File.Move(temporary, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    /// <summary>
    /// Komprimiert alle Datenströme außer Bildern und Metadaten mit bester Flate-Stufe; bereits Flate-kodierte
    /// werden neu komprimiert, wenn das kleiner wird.
    /// </summary>
    private static void CompressStreams(PdfDocument document)
    {
        foreach (var item in document.Internals.GetAllObjects())
        {
            if (item is not PdfDictionary { Stream: { } stream } dictionary
                || dictionary.Elements.GetName("/Subtype") == ImageSubtype
                || dictionary.Elements.GetName("/Type") == "/Metadata")
            {
                continue;
            }

            if (!stream.IsFiltered())
            {
                stream.Zip();
                continue;
            }

            if (dictionary.Elements.GetName("/Filter") == "/FlateDecode" && !dictionary.Elements.ContainsKey("/DecodeParms"))
            {
                Recompress(dictionary, stream);
            }
        }
    }

    /// <summary>
    /// Entpackt und packt einen Flate-Strom neu; behält die alte Fassung, wenn die neue nicht kleiner ist.
    /// </summary>
    private static void Recompress(PdfDictionary dictionary, PdfDictionary.PdfStream stream)
    {
        var original = stream.Value;
        if (!stream.TryUncompress())
        {
            return;
        }

        stream.Zip();
        if (stream.Value.Length >= original.Length)
        {
            stream.Value = original;
            dictionary.Elements["/Filter"] = new PdfName("/FlateDecode");
        }
    }

    /// <summary>
    /// Findet die Bilder der Seiten (auch in Formular-XObjects und Erscheinungsbildern von Anmerkungen) mit der
    /// längsten Kante der größten Seite, auf der sie vorkommen.
    /// </summary>
    private sealed class ImageCollector
    {
        private readonly Dictionary<PdfDictionary, double> _images = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<PdfDictionary> _visited = new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Sammelt die Bilder eines Dokuments.
        /// </summary>
        public static IReadOnlyList<(PdfDictionary Image, double PageSide)> Collect(PdfDocument document)
        {
            var collector = new ImageCollector();
            foreach (var page in document.Pages)
            {
                var box = page.MediaBox;
                var side = Math.Max(Math.Abs(box.Width), Math.Abs(box.Height));
                collector._visited.Clear();
                collector.VisitResources(page.Elements["/Resources"], side);
                collector.VisitAnnotations(Resolve(page.Elements["/Annots"]) as PdfArray, side);
            }

            return collector._images.Select(entry => (entry.Key, entry.Value)).ToList();
        }

        private void VisitResources(PdfItem? resources, double side)
        {
            if (Resolve(resources) is not PdfDictionary dictionary || Resolve(dictionary.Elements["/XObject"]) is not PdfDictionary objects)
            {
                return;
            }

            foreach (var key in objects.Elements.Keys)
            {
                VisitXObject(Resolve(objects.Elements[key]) as PdfDictionary, side);
            }
        }

        private void VisitXObject(PdfDictionary? xObject, double side)
        {
            if (xObject is null || !_visited.Add(xObject))
            {
                return;
            }

            switch (xObject.Elements.GetName("/Subtype"))
            {
                case ImageSubtype:
                    _images[xObject] = Math.Max(side, _images.GetValueOrDefault(xObject));
                    break;
                case "/Form":
                    VisitResources(xObject.Elements["/Resources"], side);
                    break;
            }
        }

        private void VisitAnnotations(PdfArray? annotations, double side)
        {
            if (annotations is null)
            {
                return;
            }

            foreach (var item in annotations.Elements)
            {
                if (Resolve(item) is not PdfDictionary annotation
                    || Resolve(annotation.Elements["/AP"]) is not PdfDictionary appearance)
                {
                    continue;
                }

                foreach (var key in appearance.Elements.Keys)
                {
                    // Erscheinungsbild direkt als Strom oder als Wörterbuch von Zuständen
                    if (Resolve(appearance.Elements[key]) is not PdfDictionary entry)
                    {
                        continue;
                    }

                    if (entry.Stream is not null)
                    {
                        VisitXObject(entry, side);
                        continue;
                    }

                    foreach (var state in entry.Elements.Keys)
                    {
                        VisitXObject(Resolve(entry.Elements[state]) as PdfDictionary, side);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Löst einen indirekten Verweis auf.
    /// </summary>
    private static PdfItem? Resolve(PdfItem? item) => item is PdfReference reference ? reference.Value : item;

    /// <summary>
    /// Verkleinert ein einzelnes Bild und speichert es als JPEG, wenn das kleiner wird.
    /// </summary>
    internal static class ImageRecompressor
    {
        /// <summary>
        /// Liest ein geeignetes Bild für <see cref="Recompress"/> aus; greift als einziger Schritt auf das Dokument zu.
        /// </summary>
        /// <param name="image">Das Bild-XObject.</param>
        /// <param name="pageSide">Längste Seitenkante in Punkten.</param>
        /// <returns>Die Arbeitsdaten oder <c>null</c>, wenn das Bild nicht bearbeitet werden kann.</returns>
        public static Job? Prepare(PdfDictionary image, double pageSide) =>
            image.Stream is { } stream && Describe(image) is { } description
                ? new Job(image, description, stream.Value, image.Elements["/Filter"], image.Elements["/DecodeParms"], pageSide)
                : null;

        /// <summary>
        /// Dekodiert, verkleinert und kodiert das Bild als JPEG. Greift nicht auf das Dokument zu und kann daher
        /// parallel laufen.
        /// </summary>
        /// <param name="job">Die Arbeitsdaten.</param>
        /// <param name="preset">Die Stufe.</param>
        /// <returns>Das neue Bild oder <c>null</c>, wenn es nicht kleiner wird.</returns>
        public static Result? Recompress(Job job, CompressionPreset preset)
        {
            var description = job.Description;
            using var bitmap = Decode(job);
            if (bitmap is null)
            {
                return null;
            }

            var (width, height) = preset.TargetSize(description.Width, description.Height, job.PageSide);
            using var resized = width == bitmap.Width && height == bitmap.Height
                ? null
                : bitmap.Resize(new SKImageInfo(width, height, bitmap.ColorType, SKAlphaType.Opaque), Sampling((double)width / bitmap.Width));
            using var data = (resized ?? bitmap).Encode(SKEncodedImageFormat.Jpeg, preset.JpegQuality);
            return data is null || data.Size >= job.Data.Length ? null : new Result(data.ToArray(), width, height);
        }

        /// <summary>
        /// Abtastung beim Verkleinern: bis auf die Hälfte bikubisch (schnell und scharf), stärker über Mipmaps, damit
        /// keine Treppen- und Moiré-Effekte entstehen.
        /// </summary>
        private static SKSamplingOptions Sampling(double factor) => factor >= 0.5
            ? new SKSamplingOptions(SKCubicResampler.Mitchell)
            : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        /// <summary>
        /// Setzt das neue Bild ein.
        /// </summary>
        /// <param name="image">Das Bild-XObject.</param>
        /// <param name="result">Das neue JPEG.</param>
        public static void Apply(PdfDictionary image, Result result)
        {
            image.Stream!.Value = result.Jpeg;
            image.Elements["/Filter"] = new PdfName("/DCTDecode");
            image.Elements.Remove("/DecodeParms");
            image.Elements["/Width"] = new PdfInteger(result.Width);
            image.Elements["/Height"] = new PdfInteger(result.Height);
            image.Elements["/BitsPerComponent"] = new PdfInteger(8);
        }

        /// <summary>
        /// Prüft, ob das Bild bearbeitet werden kann, und liest Größe, Farbkanäle und Kodierung.
        /// </summary>
        private static ImageDescription? Describe(PdfDictionary image)
        {
            var elements = image.Elements;
            if (elements.GetBoolean("/ImageMask") || elements.ContainsKey("/SMask") || elements.ContainsKey("/Mask")
                || elements.ContainsKey("/Decode") || elements.GetInteger("/BitsPerComponent") != 8)
            {
                return null;
            }

            var width = elements.GetInteger("/Width");
            var height = elements.GetInteger("/Height");
            var components = Components(Resolve(elements["/ColorSpace"]));
            var filter = Filter(Resolve(elements["/Filter"]));
            return width > 0 && height > 0 && components is 1 or 3 && filter is not null
                ? new ImageDescription(width, height, components, filter)
                : null;
        }

        /// <summary>
        /// Farbkanäle des Farbraums: 1 (Grau) oder 3 (RGB), sonst 0 (nicht unterstützt).
        /// </summary>
        private static int Components(PdfItem? colorSpace) => colorSpace switch
        {
            PdfName { Value: "/DeviceGray" } => 1,
            PdfName { Value: "/DeviceRGB" } => 3,
            PdfArray { Elements.Count: 2 } array when array.Elements.GetName(0) == "/ICCBased"
                && Resolve(array.Elements[1]) is PdfDictionary profile => profile.Elements.GetInteger("/N") is var n and (1 or 3) ? n : 0,
            _ => 0,
        };

        /// <summary>
        /// Kodierung: <c>/DCTDecode</c>, <c>/FlateDecode</c> oder <c>""</c> (unkodiert); <c>null</c> bei anderen
        /// oder mehreren Filtern.
        /// </summary>
        private static string? Filter(PdfItem? filter) => filter switch
        {
            null => "",
            PdfName { Value: "/DCTDecode" or "/FlateDecode" } name => name.Value,
            PdfArray { Elements.Count: 1 } array => Filter(Resolve(array.Elements[0])),
            _ => null,
        };

        /// <summary>
        /// Dekodiert das Bild in ein deckendes Bitmap (Grau oder RGBA); <c>null</c> bei unerwarteten Daten.
        /// </summary>
        private static SKBitmap? Decode(Job job)
        {
            var (data, description) = (job.Data, job.Description);
            if (description.Filter == "/DCTDecode")
            {
                var decoded = SKBitmap.Decode(data);
                if (decoded is null || decoded.Width != description.Width || decoded.Height != description.Height)
                {
                    decoded?.Dispose();
                    return null;
                }

                var wanted = description.Components == 1 ? SKColorType.Gray8 : SKColorType.Rgba8888;
                if (decoded.ColorType == wanted)
                {
                    return decoded;
                }

                var converted = decoded.Copy(wanted);
                decoded.Dispose();
                return converted;
            }

            byte[] raw;
            try
            {
                raw = description.Filter == "" ? data
                    : job.DecodeParms is null ? Inflate(data)
                    : Filtering.Decode(data, job.Filter!, job.DecodeParms);
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotImplementedException or IOException or ArgumentException or InvalidDataException)
            {
                return null;
            }

            return raw is null ? null : FromSamples(raw, description);
        }

        /// <summary>
        /// Entpackt Flate-Daten ohne Prädiktor mit der zlib der Laufzeit (deutlich schneller als der Dekoder von
        /// PDFsharp).
        /// </summary>
        private static byte[] Inflate(byte[] data)
        {
            using var input = new System.IO.Compression.ZLibStream(new MemoryStream(data, writable: false), System.IO.Compression.CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>
        /// Baut ein Bitmap aus unkomprimierten 8-Bit-Abtastwerten.
        /// </summary>
        private static SKBitmap? FromSamples(byte[] raw, ImageDescription description)
        {
            var (width, height, components) = (description.Width, description.Height, description.Components);
            if (raw.Length < (long)width * height * components)
            {
                return null;
            }

            var gray = components == 1;
            var bitmap = new SKBitmap(new SKImageInfo(width, height, gray ? SKColorType.Gray8 : SKColorType.Rgba8888, SKAlphaType.Opaque));
            var pixels = new byte[bitmap.ByteCount];
            var rowBytes = bitmap.RowBytes;
            for (var y = 0; y < height; y++)
            {
                var source = y * width * components;
                var target = y * rowBytes;
                if (gray)
                {
                    Buffer.BlockCopy(raw, source, pixels, target, width);
                    continue;
                }

                for (var x = 0; x < width; x++, source += 3, target += 4)
                {
                    pixels[target] = raw[source];
                    pixels[target + 1] = raw[source + 1];
                    pixels[target + 2] = raw[source + 2];
                    pixels[target + 3] = 255;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            return bitmap;
        }

        /// <summary>Größe, Farbkanäle und Kodierung eines Bildes.</summary>
        internal sealed record ImageDescription(int Width, int Height, int Components, string Filter);

        /// <summary>Ausgelesenes Bild mit allem, was das Umwandeln braucht.</summary>
        internal sealed record Job(PdfDictionary Image, ImageDescription Description, byte[] Data, PdfItem? Filter, PdfItem? DecodeParms, double PageSide);

        /// <summary>Neues JPEG mit seiner Größe.</summary>
        internal sealed record Result(byte[] Jpeg, int Width, int Height);
    }
}
