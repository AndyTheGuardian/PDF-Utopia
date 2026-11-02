using PdfUtopia.Core.Settings;

namespace PdfUtopia.Core.Tests.Settings;

/// <summary>
/// Tests der Einstellungsdatei: Hin- und Rückweg, beschädigte Dateien, Bereinigung ungültiger Werte.
/// </summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"pdfutopia-settings-{Guid.NewGuid():N}");

    private string FilePath => Path.Combine(_directory, "sub", "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults() =>
        Assert.Equal(50, new SettingsStore(FilePath).Load().UndoLimit);

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var store = new SettingsStore(FilePath);
        var settings = new AppSettings
        {
            Theme = ThemePreference.Dark,
            Language = UiLanguage.English,
            AccentColor = "#facc15",
            CustomColors = ["#336699"],
            UndoLimit = 120,
            PressureSensitive = true,
            Smoothing = 70,
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(ThemePreference.Dark, loaded.Theme);
        Assert.Equal(UiLanguage.English, loaded.Language);
        Assert.Equal("#FACC15", loaded.AccentColor);
        Assert.Equal(["#336699"], loaded.CustomColors);
        Assert.Equal(120, loaded.UndoLimit);
        Assert.True(loaded.PressureSensitive);
        Assert.Equal(70, loaded.Smoothing);
        Assert.Contains("\"Dark\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Load_BrokenFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, "{ kaputt");

        Assert.Equal(ThemePreference.System, new SettingsStore(FilePath).Load().Theme);
    }

    [Fact]
    public void Normalized_ReplacesInvalidValues()
    {
        var settings = new AppSettings
        {
            AccentColor = "rot",
            UndoLimit = 9999,
            FreehandColor = "",
            MarkerColor = "#123456",
            Smoothing = 200,
            EraserRadius = 0,
            CustomColors = ["#111111", "falsch", "#111111"],
        }.Normalized();

        Assert.Null(settings.AccentColor);
        Assert.Equal(500, settings.UndoLimit);
        Assert.Equal("#C93434", settings.FreehandColor);
        Assert.Equal("#FDE047", settings.MarkerColor);
        Assert.Equal(100, settings.Smoothing);
        Assert.Equal(2, settings.EraserRadius);
        Assert.Equal(["#111111"], settings.CustomColors);
    }

    [Theory]
    [InlineData(null, "de", UiLanguage.German)]
    [InlineData(null, "fr", UiLanguage.English)]
    [InlineData(UiLanguage.English, "de", UiLanguage.English)]
    public void ResolveLanguage_UsesChoiceOrSystem(UiLanguage? chosen, string system, UiLanguage expected) =>
        Assert.Equal(expected, new AppSettings { Language = chosen }.ResolveLanguage(system));
}
