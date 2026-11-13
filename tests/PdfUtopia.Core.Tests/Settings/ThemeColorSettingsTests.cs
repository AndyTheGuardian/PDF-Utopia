using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.Core.Tests.Settings;

/// <summary>
/// Tests der Farben je Design und der Übernahme alter Einstellungen (A56).
/// </summary>
public sealed class ThemeColorSettingsTests
{
    [Fact]
    public void Normalized_OldAccentColor_MovesIntoBothThemes()
    {
        var settings = new AppSettings { AccentColor = "#2f6fdf", PastelSaturation = 50 }.Normalized();

        Assert.Equal("#2F6FDF", settings.ColorsFor(dark: false).AccentColor);
        Assert.Equal("#2F6FDF", settings.ColorsFor(dark: true).AccentColor);
        Assert.Equal(50, settings.ColorsFor(dark: false).PastelSaturation);
        Assert.Equal(0, settings.ColorsFor(dark: true).TintSaturation);
        Assert.Null(settings.AccentColor);
    }

    [Fact]
    public void Normalized_NewInstallation_StartsWithStandardProfile()
    {
        var settings = new AppSettings().Normalized();

        Assert.True(BuiltInColorProfiles.Light[0].Matches(settings.ColorsFor(dark: false)));
        Assert.True(BuiltInColorProfiles.Dark[0].Matches(settings.ColorsFor(dark: true)));
    }

    [Fact]
    public void Normalized_KeepsSavedThemeColors_AndCleansInvalidValues()
    {
        var settings = new AppSettings
        {
            AccentColor = "#123456",
            LightColors = new() { AccentColor = "#ABCDEF", TintHue = 400, TintSaturation = 120, Brightness = 99 },
            DarkColors = new() { AccentColor = "kaputt" },
        }.Normalized();

        Assert.Equal("#ABCDEF", settings.ColorsFor(dark: false).AccentColor);
        Assert.Equal((40, 100, ThemeTint.MaxBrightness), (settings.ColorsFor(false).TintHue, settings.ColorsFor(false).TintSaturation, settings.ColorsFor(false).Brightness));
        Assert.Null(settings.ColorsFor(dark: true).AccentColor);
    }

    [Fact]
    public void WithColors_ChangesOnlyThatTheme()
    {
        var settings = new AppSettings().Normalized().WithColors(dark: true, new ThemeColorSettings { AccentColor = "#123456" });

        Assert.Equal("#123456", settings.ColorsFor(dark: true).AccentColor);
        Assert.Null(settings.ColorsFor(dark: false).AccentColor);
    }

    [Fact]
    public void SettingsStore_RoundTrip_KeepsThemeColorsAndProfiles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfu-colors-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            var profile = new ColorProfile("Mein Profil", true, new ThemeColorSettings { AccentColor = "#C58BD8", TintHue = 280, TintSaturation = 30, Brightness = 2 });
            store.Save(new AppSettings { ColorProfiles = [profile] }.Normalized().WithColors(dark: false, new ThemeColorSettings { TintSaturation = 40, TintHue = 100 }));

            var loaded = store.Load();

            Assert.Equal((100, 40), (loaded.ColorsFor(false).TintHue, loaded.ColorsFor(false).TintSaturation));
            Assert.Single(loaded.ColorProfiles);
            Assert.True(loaded.ColorProfiles[0].Matches(profile.Colors));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
