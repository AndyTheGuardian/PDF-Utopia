using System.Diagnostics;
using System.Text;
using PdfUtopia.Core.Geometry;
using PdfUtopia.Core.Pages;
using PdfUtopia.Core.Text;
using PdfUtopia.Pdf.Rendering;

namespace PdfUtopia.Pdf.Ocr;

/// <summary>
/// Texterkennung mit dem separat installierten Programm Tesseract (A53, F41). Jede Seite wird so gerendert, wie sie
/// angezeigt wird, als Graustufenbild an Tesseract übergeben und die erkannten Wörter auf die Seite zurückgerechnet.
/// </summary>
public sealed class TesseractEngine
{
    /// <summary>Auflösung des Bildes für die Erkennung.</summary>
    public const double Dpi = 300;

    private const double PointsPerInch = 72;

    /// <summary>Mindestsicherheit der Ausrichtungserkennung, ab der eine Seite gedreht erkannt wird.</summary>
    private const double MinOrientationConfidence = 1.5;

    private TesseractEngine(string executablePath, string version, IReadOnlyList<string> languages, bool canDetectOrientation)
    {
        ExecutablePath = executablePath;
        Version = version;
        Languages = languages;
        CanDetectOrientation = canDetectOrientation;
    }

    /// <summary>Ob die Sprachdaten zur Ausrichtungserkennung („osd“) installiert sind.</summary>
    public bool CanDetectOrientation { get; }

    /// <summary>Pfad des Programms.</summary>
    public string ExecutablePath { get; }

    /// <summary>Versionsangabe, z. B. „5.3.4“.</summary>
    public string Version { get; }

    /// <summary>Installierte Sprachen (Tesseract-Kürzel wie „deu“, „eng“), ohne die Schriftlagen-Erkennung „osd“.</summary>
    public IReadOnlyList<string> Languages { get; }

    /// <summary>
    /// Sucht Tesseract im Suchpfad und an den üblichen Installationsorten.
    /// </summary>
    /// <returns>Die Engine oder <c>null</c>, wenn Tesseract nicht gefunden wurde oder nicht startet.</returns>
    public static TesseractEngine? Find()
    {
        foreach (var candidate in Candidates().Where(File.Exists).Distinct())
        {
            var version = Run(candidate, "--version");
            var languages = Run(candidate, "--list-langs");
            if (version is null || languages is null)
            {
                continue;
            }

            return new TesseractEngine(candidate, ParseVersion(version), ParseLanguages(languages),
                languages.Split('\n').Any(line => line.Trim() == "osd"));
        }

        return null;
    }

    /// <summary>
    /// Liest die Versionsnummer aus der Ausgabe von <c>tesseract --version</c>.
    /// </summary>
    /// <param name="output">Die Ausgabe.</param>
    /// <returns>z. B. „5.3.4“ oder leer.</returns>
    public static string ParseVersion(string output)
    {
        var first = output.Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;
        return first.StartsWith("tesseract ", StringComparison.OrdinalIgnoreCase) ? first["tesseract ".Length..].Trim().TrimStart('v') : first;
    }

    /// <summary>
    /// Liest die Sprachen aus der Ausgabe von <c>tesseract --list-langs</c> (erste Zeile ist eine Überschrift).
    /// </summary>
    /// <param name="output">Die Ausgabe.</param>
    /// <returns>Die Sprachkürzel ohne „osd“.</returns>
    public static IReadOnlyList<string> ParseLanguages(string output) =>
        output.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0 && !line.Contains(' ') && !line.EndsWith(':') && line != "osd").ToList();

    /// <summary>
    /// Liest aus der Ausgabe der Ausrichtungserkennung (<c>--psm 0</c>), um wie viel Grad im Uhrzeigersinn das Bild zu
    /// drehen ist, damit der Text aufrecht steht.
    /// </summary>
    /// <param name="output">Die Ausgabe.</param>
    /// <returns>0, 90, 180 oder 270; 0, wenn nichts Verlässliches erkannt wurde.</returns>
    public static int ParseRotation(string output)
    {
        int? rotate = null;
        var confidence = 0.0;
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (parts[0] == "Rotate" && int.TryParse(parts[1], out var value))
            {
                rotate = value;
            }
            else if (parts[0] == "Orientation confidence")
            {
                double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out confidence);
            }
        }

        return rotate is 90 or 180 or 270 && confidence >= MinOrientationConfidence ? rotate.Value : 0;
    }

    /// <summary>
    /// Erkennt den Text einer Seite.
    /// </summary>
    /// <param name="source">Die Datei.</param>
    /// <param name="sourcePageNumber">Seitennummer in der Datei (1-basiert).</param>
    /// <param name="rotation">Drehung aus der Bearbeitung (die Seite wird so erkannt, wie sie angezeigt wird).</param>
    /// <param name="languages">Sprachen, z. B. „deu+eng“.</param>
    /// <param name="cancellation">Abbruch.</param>
    /// <returns>Der erkannte Text in Koordinaten der ungedrehten Seite.</returns>
    /// <exception cref="InvalidOperationException">Wenn Tesseract mit einem Fehler endet.</exception>
    public async Task<RecognizedPage> RecognizeAsync(
        PdfSource source, int sourcePageNumber, PageRotation rotation, string languages, CancellationToken cancellation = default)
    {
        var scale = Dpi / PointsPerInch;
        var (width, height) = source.PageSize(sourcePageNumber);
        var image = Path.Combine(Path.GetTempPath(), $"pdfutopia-ocr-{Guid.NewGuid():N}.pgm");
        var dpi = ((int)Dpi).ToString(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            await File.WriteAllBytesAsync(image, ToPgm(source.Render(sourcePageNumber, scale, rotation, drawForms: false)), cancellation);
            // Quer oder kopfüber gescannte Seiten aufrecht erkennen, damit Wörter und Textebene in Leserichtung liegen
            if (CanDetectOrientation && await TryRunAsync([image, "stdout", "--psm", "0", "--dpi", dpi], cancellation) is { } osd
                && ParseRotation(osd) is var turn and not 0)
            {
                rotation = (PageRotation)(((int)rotation + turn) % 360);
                await File.WriteAllBytesAsync(image, ToPgm(source.Render(sourcePageNumber, scale, rotation, drawForms: false)), cancellation);
            }

            var tsv = await RunAsync(ExecutablePath, [image, "stdout", "-l", languages, "--dpi", dpi, "tsv"], cancellation);
            return TesseractTsv.Parse(tsv, rect => ToPage(rect, scale, rotation, width, height), rotation);
        }
        finally
        {
            File.Delete(image);
        }
    }

    /// <summary>
    /// Rechnet ein Rechteck in Bildpunkten der gedrehten Anzeige auf die ungedrehte Seite um.
    /// </summary>
    /// <param name="pixels">Rechteck im Bild.</param>
    /// <param name="scale">Bildpunkte je Punkt.</param>
    /// <param name="rotation">Drehung des Bildes.</param>
    /// <param name="width">Breite der ungedrehten Seite.</param>
    /// <param name="height">Höhe der ungedrehten Seite.</param>
    /// <returns>Rechteck auf der ungedrehten Seite.</returns>
    public static PdfRect ToPage(PdfRect pixels, double scale, PageRotation rotation, double width, double height)
    {
        var first = PageTransform.Unrotate(new PdfPoint(pixels.X / scale, pixels.Y / scale), rotation, width, height);
        var second = PageTransform.Unrotate(new PdfPoint(pixels.Right / scale, pixels.Bottom / scale), rotation, width, height);
        return PdfRect.FromCorners(first, second);
    }

    /// <summary>
    /// Wandelt BGRA-Pixel in ein Graustufenbild im PGM-Format (von Tesseract direkt lesbar).
    /// </summary>
    private static byte[] ToPgm(RenderedPage page)
    {
        var header = Encoding.ASCII.GetBytes($"P5\n{page.Width} {page.Height}\n255\n");
        var data = new byte[header.Length + page.Width * page.Height];
        header.CopyTo(data, 0);
        var target = header.Length;
        for (var y = 0; y < page.Height; y++)
        {
            for (var x = 0; x < page.Width; x++)
            {
                var offset = y * page.Stride + x * 4;
                data[target++] = (byte)((page.Pixels[offset + 2] * 299 + page.Pixels[offset + 1] * 587 + page.Pixels[offset] * 114) / 1000);
            }
        }

        return data;
    }

    /// <summary>Mögliche Orte des Programms.</summary>
    private static IEnumerable<string> Candidates()
    {
        var name = OperatingSystem.IsWindows() ? "tesseract.exe" : "tesseract";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return Path.Combine(directory, name);
        }

        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tesseract-OCR", name);
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Tesseract-OCR", name);
        }
        else
        {
            yield return "/opt/homebrew/bin/tesseract";
            yield return "/usr/local/bin/tesseract";
            yield return "/usr/bin/tesseract";
        }
    }

    /// <summary>Startet das Programm kurz und liefert die Ausgabe oder <c>null</c> bei einem Fehler.</summary>
    private static string? Run(string executable, string argument)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, argument)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            });
            if (process is null)
            {
                return null;
            }

            // Ältere Versionen schreiben --version auf die Fehlerausgabe
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            return process.WaitForExit(10_000) && process.ExitCode == 0 ? output : null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Führt Tesseract aus; liefert <c>null</c> statt eines Fehlers (z. B. zu wenig Text für die Ausrichtung).</summary>
    private async Task<string?> TryRunAsync(IReadOnlyList<string> arguments, CancellationToken cancellation)
    {
        try
        {
            return await RunAsync(ExecutablePath, arguments, cancellation);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Führt Tesseract aus; bei Abbruch wird der Prozess beendet.</summary>
    private static async Task<string> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Tesseract konnte nicht gestartet werden.");
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(cancellation);
            var error = process.StandardError.ReadToEndAsync(cancellation);
            await process.WaitForExitAsync(cancellation);
            return process.ExitCode == 0 ? await output : throw new InvalidOperationException($"Tesseract meldet einen Fehler: {(await error).Trim()}");
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
