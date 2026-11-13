using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.Core.Tests.Theming;

public sealed class ColorProfileTests
{
    [Fact]
    public void BuiltIn_SevenPerTheme_StandardFirst_ThenNeutral()
    {
        foreach (var dark in new[] { false, true })
        {
            var profiles = BuiltInColorProfiles.For(dark);

            Assert.Equal(7, profiles.Count);
            Assert.All(profiles, p => Assert.True(p.IsBuiltIn));
            Assert.All(profiles, p => Assert.Equal(dark, p.IsDark));
            // „Standard“ sind die bisherigen Farben von PDF Utopia (A56)
            Assert.Equal("Profile.Standard", profiles[0].Name);
            Assert.True(profiles[0].Matches(new ThemeColorSettings()));
            Assert.Equal("Profile.Neutral", profiles[1].Name);
            Assert.True(profiles[1].Matches(new ThemeColorSettings { AccentColor = dark ? "#F2F2F2" : "#0D0D0D", Brightness = 8 }));
            Assert.False(profiles[1].Matches(new ThemeColorSettings()));
            Assert.Equal(7, profiles.Select(p => p.Name).Distinct().Count());
        }

        var sage = BuiltInColorProfiles.Light.Single(p => p.Name == "Profile.Sage");
        Assert.Equal("#779E88", sage.Colors.AccentColor);
        Assert.Equal((104, 26), (sage.Colors.TintHue, sage.Colors.TintSaturation));
    }

    [Fact]
    public void Matches_IgnoresHueWithoutTint_AndTreatsDefaultAccentAsNull()
    {
        var standard = new ColorProfile("Standard", false, new ThemeColorSettings());

        Assert.True(standard.Matches(new ThemeColorSettings { TintHue = 40 }));
        Assert.True(standard.Matches(new ThemeColorSettings { AccentColor = AccentPalette.DefaultAccent }));
        Assert.False(standard.Matches(new ThemeColorSettings { Brightness = 1 }));
        Assert.False(standard.Matches(new ThemeColorSettings { TintSaturation = 5 }));
        Assert.False((standard with { IsDark = true }).Matches(new ThemeColorSettings { AccentColor = AccentPalette.DefaultAccent }));
    }

    [Fact]
    public void UniqueName_AppendsNumber_CaseInsensitive()
    {
        Assert.Equal("Salbei", ColorProfile.UniqueName([], "  Salbei "));
        Assert.Equal("Salbei (2)", ColorProfile.UniqueName(["salbei"], "Salbei"));
        Assert.Equal("Salbei (3)", ColorProfile.UniqueName(["Salbei", "Salbei (2)"], "Salbei"));

        var longName = new string('x', 80);
        var unique = ColorProfile.UniqueName([new string('x', ColorProfile.MaxNameLength)], longName);
        Assert.Equal(ColorProfile.MaxNameLength, unique.Length);
        Assert.EndsWith(" (2)", unique, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeCustom_DropsEmptyAndDuplicateNamesPerTheme()
    {
        var colors = new ThemeColorSettings { Brightness = 99 };
        var result = ColorProfile.NormalizeCustom(
        [
            new ColorProfile(" Mein ", false, colors) { IsBuiltIn = true },
            new ColorProfile("mein", false, colors),
            new ColorProfile("Mein", true, colors),
            new ColorProfile("  ", false, colors),
            null,
        ]);

        Assert.Equal([("Mein", false), ("Mein", true)], result.Select(p => (p.Name, p.IsDark)));
        Assert.All(result, p => Assert.False(p.IsBuiltIn));
        Assert.All(result, p => Assert.Equal(ThemeTint.MaxBrightness, p.Colors.Brightness));
    }

    [Fact]
    public void File_RoundTrip_KeepsColorsWithinRounding()
    {
        var profiles = BuiltInColorProfiles.Light.Concat(BuiltInColorProfiles.Dark).Select(p => p with { Name = p.Name[8..] }).ToList();

        var read = ColorProfileFile.Read(ColorProfileFile.Write(profiles));

        Assert.Equal(0, read.Skipped);
        Assert.Equal(profiles.Count, read.Profiles.Count);
        foreach (var (original, copy) in profiles.Zip(read.Profiles))
        {
            Assert.Equal((original.Name, original.IsDark), (copy.Name, copy.IsDark));
            Assert.Equal(original.EffectiveAccent(), copy.Colors.AccentColor);
            Assert.Equal(original.Colors.Brightness, copy.Colors.Brightness);
            Assert.InRange(copy.Colors.TintSaturation, original.Colors.TintSaturation - 1, original.Colors.TintSaturation + 1);
            if (original.Colors.TintSaturation > 0)
            {
                Assert.InRange(Math.Abs(copy.Colors.TintHue - original.Colors.TintHue), 0, 2);
            }

            Assert.False(copy.IsBuiltIn);
        }
    }

    [Fact]
    public void File_Write_UsesDocumentedFields()
    {
        var json = ColorProfileFile.Write([BuiltInColorProfiles.Light[2] with { Name = "Salbei" }]);

        Assert.Contains("\"format\": \"color-profiles\"", json, StringComparison.Ordinal);
        Assert.Contains("\"version\": 1", json, StringComparison.Ordinal);
        Assert.Contains("\"theme\": \"light\"", json, StringComparison.Ordinal);
        Assert.Contains("\"accent\": \"#779E88\"", json, StringComparison.Ordinal);
        Assert.Contains("\"background\": \"#", json, StringComparison.Ordinal);
        Assert.Contains("\"pastelSaturation\": 30", json, StringComparison.Ordinal);
    }

    [Fact]
    public void File_Read_SkipsInvalidEntries_AndDefaultsMissingNumbers()
    {
        const string json = """
            { "format": "color-profiles", "version": 1, "profiles": [
              { "name": "Ok", "theme": "dark", "accent": "#112233" },
              { "name": "", "theme": "dark", "accent": "#112233" },
              { "name": "Theme", "theme": "blue", "accent": "#112233" },
              { "name": "Farbe", "theme": "light", "accent": "rot" },
              42
            ] }
            """;

        var read = ColorProfileFile.Read(json);

        var profile = Assert.Single(read.Profiles);
        Assert.Equal(4, read.Skipped);
        Assert.Equal(("Ok", true, 0, 0), (profile.Name, profile.IsDark, profile.Colors.TintSaturation, profile.Colors.Brightness));
        Assert.Equal(AccentPalette.DefaultSaturation, profile.Colors.PastelSaturation);
    }

    [Theory]
    [InlineData("kein json")]
    [InlineData("[]")]
    [InlineData("""{ "format": "andere", "version": 1 }""")]
    [InlineData("""{ "format": "color-profiles", "version": 2, "profiles": [] }""")]
    [InlineData("""{ "format": "color-profiles", "profiles": [] }""")]
    public void File_Read_RejectsForeignOrNewerFiles(string json) =>
        Assert.Throws<ColorProfileFormatException>(() => ColorProfileFile.Read(json));

    [Fact]
    public void Settings_RoundTrip_KeepsCustomProfiles()
    {
        var folder = Path.Combine(Path.GetTempPath(), "AssetManagerProfiles", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(Path.Combine(folder, "settings.json"));
            var profile = new ColorProfile("Mein Profil", true, new ThemeColorSettings { AccentColor = "#112233", TintHue = 200, TintSaturation = 40, Brightness = -3 });
            store.Save(new AppSettings { ColorProfiles = [profile] });

            Assert.Equal(profile, Assert.Single(store.Load().ColorProfiles));
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
