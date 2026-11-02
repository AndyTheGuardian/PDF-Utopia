namespace PdfUtopia.Pdf.Tests;

/// <summary>
/// Temporäres Verzeichnis je Testklasse; wird danach gelöscht.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>
    /// Legt das Verzeichnis an (mit Umlaut und Leerzeichen, um Pfadprobleme aufzudecken).
    /// </summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdfutopia tests ä {Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>Pfad des Verzeichnisses.</summary>
    public string Path { get; }

    /// <summary>
    /// Pfad einer Datei im Verzeichnis.
    /// </summary>
    /// <param name="name">Dateiname.</param>
    /// <returns>Der Pfad.</returns>
    public string File(string name) => System.IO.Path.Combine(Path, name);

    /// <inheritdoc />
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
