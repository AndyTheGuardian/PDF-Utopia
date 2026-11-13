using PdfUtopia.App.Localization;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.Tests;

public sealed class SettingsViewModelTests
{
    private readonly FakeTheme _theme = new();

    [Fact]
    public void Constructor_DoesNotPreview_SlidersShowAccent()
    {
        // Regression: Die Vorschau lief im Konstruktor, bevor alle Werte gesetzt waren (NullReferenceException).
        var vm = Create(new AppSettings { Language = UiLanguage.English, LightColors = new() { AccentColor = "#2F6FDF" } });

        Assert.Empty(_theme.Applied);
        Assert.Equal(UiLanguage.English, vm.SelectedLanguage.Language);
        Assert.True(vm.Strong.Single(s => s.Hex == "#2F6FDF").IsSelected);
        Assert.Equal(AdjustTarget.Accent, vm.Target);
        var (hue, saturation, value) = AccentPalette.ToHsv("#2F6FDF");
        Assert.Equal((Math.Round(hue), Math.Round(saturation * 100), Math.Round(value * 100)), (vm.SliderHue, vm.SliderSaturation, vm.SliderBrightness));
        Assert.Equal("#2F6FDF", vm.HexInput);
        Assert.Equal("#2F6FDF", vm.PreviewHex);
    }

    [Fact]
    public void Palette_PastelAtSavedSaturation_StrongNeutralCustom()
    {
        var vm = Create(new AppSettings { CustomColors = ["#123456"], LightColors = new() { PastelSaturation = 50 } });

        // PDF Utopia behält seine Pastell-Reihe (gemeinsam mit der Werkzeug-Farbwahl, A56)
        Assert.Equal(AccentPalette.Pastel(50).Select(s => s.Hex), vm.Pastel.Select(s => s.Hex));
        Assert.Equal(AccentPalette.Strong.Select(s => s.Hex), vm.Strong.Select(s => s.Hex));
        Assert.Equal(["#0D0D0D", "#F2F2F2"], vm.Neutral.Select(s => s.Hex));
        Assert.True(Assert.Single(vm.Custom).IsCustom);
    }

    [Fact]
    public void ChooseSwatch_SwitchesToAccent_PreviewsAndSyncsSliders()
    {
        var vm = Create(new AppSettings());
        vm.Target = AdjustTarget.Background;

        vm.ChooseSwatch(vm.Strong.Single(s => s.Hex == "#C93434"));

        Assert.Equal(AdjustTarget.Accent, vm.Target);
        Assert.True(vm.Strong.Single(s => s.Hex == "#C93434").IsSelected);
        Assert.Equal("#C93434", _theme.Applied[^1].Accent);
        Assert.Equal("#C93434", vm.HexInput);
        Assert.Equal(100, vm.BrightnessMaximum);
    }

    [Fact]
    public void AccentSliders_ChangeAccentColor()
    {
        var vm = Create(new AppSettings { LightColors = new() { AccentColor = "#FF0000" } });

        vm.SliderHue = 120;

        Assert.Equal("#00FF00", vm.AccentHex);
        Assert.Equal("#00FF00", vm.HexInput);
        Assert.Equal("#00FF00", _theme.Applied[^1].Accent);
        Assert.DoesNotContain(vm.Strong, s => s.IsSelected);
    }

    [Fact]
    public void PastelSelected_SaturationMovesWholeRow_SelectionStays()
    {
        var vm = Create(new AppSettings());
        vm.ChooseSwatch(vm.Pastel[2]);
        Assert.True(vm.IsPastelSelected);
        Assert.Equal((10d, 90d, 30d), (vm.SaturationMinimum, vm.SaturationMaximum, vm.SliderSaturation));

        vm.SliderSaturation = 60;

        Assert.Equal(AccentPalette.Pastel(60).Select(s => s.Hex), vm.Pastel.Select(s => s.Hex));
        Assert.Equal(AccentPalette.Pastel(60)[2].Hex, vm.AccentHex);
        Assert.True(vm.Pastel[2].IsSelected);
        Assert.Empty(vm.Custom);
        Assert.Equal(60, vm.ToSettings().ColorsFor(dark: false).PastelSaturation);
        Assert.Equal(vm.AccentHex, _theme.Applied[^1].Accent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PastelSelected_HueOrBrightness_CreatesSeparateColor(bool hue)
    {
        var vm = Create(new AppSettings());
        vm.ChooseSwatch(vm.Pastel[0]);
        var row = vm.Pastel.Select(s => s.Hex).ToList();

        if (hue)
        {
            vm.SliderHue = 10;
        }
        else
        {
            vm.SliderBrightness = 50;
        }

        Assert.DoesNotContain(vm.AccentHex, row);
        Assert.Equal(row, vm.Pastel.Select(s => s.Hex));
        Assert.False(vm.IsPastelSelected);
        Assert.Equal(30, vm.ToSettings().ColorsFor(dark: false).PastelSaturation);
    }

    [Fact]
    public void StrongSelected_SaturationChangesOnlyAccent()
    {
        var vm = Create(new AppSettings { LightColors = new() { AccentColor = "#C93434" } });
        var row = vm.Pastel.Select(s => s.Hex).ToList();

        vm.SliderSaturation = 20;

        Assert.Equal((0d, 100d), (vm.SaturationMinimum, vm.SaturationMaximum));
        Assert.NotEqual("#C93434", vm.AccentHex);
        Assert.Equal(row, vm.Pastel.Select(s => s.Hex));
    }

    [Fact]
    public void Target_Background_SlidersShowTint()
    {
        var vm = Create(new AppSettings { LightColors = new() { TintHue = 30, TintSaturation = 40, Brightness = -3 } });

        vm.Target = AdjustTarget.Background;

        Assert.Equal((30d, 40d, -3d), (vm.SliderHue, vm.SliderSaturation, vm.SliderBrightness));
        Assert.Equal((-25d, 25d), (vm.BrightnessMinimum, vm.BrightnessMaximum));
        Assert.Equal(ThemePalette.TintColor(30, 40), vm.PreviewHex);
        Assert.Equal("Zurücksetzen", vm.ResetLabel);
    }

    [Fact]
    public void BackgroundSliders_ChangeTint_NotAccent()
    {
        var vm = Create(new AppSettings { LightColors = new() { AccentColor = "#1F9D55" } });
        vm.Target = AdjustTarget.Background;

        vm.SliderHue = 120;
        vm.SliderSaturation = 100;
        vm.SliderBrightness = 10;

        var settings = vm.ToSettings();
        Assert.Equal((120, 100, 10), (settings.ColorsFor(dark: false).TintHue, settings.ColorsFor(dark: false).TintSaturation, settings.ColorsFor(dark: false).Brightness));
        Assert.Equal("#1F9D55", settings.ColorsFor(dark: false).AccentColor);
        Assert.Equal("#00FF00", vm.HexInput);
        Assert.Equal(new ThemeTint(120, 100, 10), _theme.Applied[^1].Tint);
    }

    [Fact]
    public void ApplyHex_Accent_AddsCustomColor()
    {
        var vm = Create(new AppSettings());
        vm.HexInput = "#abcdef";

        vm.ApplyHex();

        Assert.Equal("#ABCDEF", vm.AccentHex);
        Assert.True(Assert.Single(vm.Custom).IsSelected);
        Assert.Equal(["#ABCDEF"], vm.ToSettings().CustomColors);
    }

    [Fact]
    public void ApplyHex_Background_SetsHueAndStrength_KeepsBrightness()
    {
        var vm = Create(new AppSettings { LightColors = new() { Brightness = 4 } });
        vm.Target = AdjustTarget.Background;
        vm.HexInput = "#d98c40";

        vm.ApplyHex();

        var settings = vm.ToSettings();
        Assert.Equal((30, 67, 4), (settings.ColorsFor(dark: false).TintHue, settings.ColorsFor(dark: false).TintSaturation, settings.ColorsFor(dark: false).Brightness));
        Assert.Equal("#D98C40", vm.HexInput);
        Assert.Empty(vm.Custom);
        Assert.Equal(67, vm.SliderSaturation);
    }

    [Fact]
    public void ApplyHex_Invalid_ShowsError()
    {
        var vm = Create(new AppSettings());
        vm.HexInput = "blau";

        vm.ApplyHex();

        Assert.True(vm.HasHexError);
        Assert.Null(vm.AccentHex); // unverändert: Standard-Akzent des Startprofils „Standard“
    }

    [Fact]
    public void RemoveCustom_KeepsChosenAccent()
    {
        var vm = Create(new AppSettings { CustomColors = ["#123456"], LightColors = new() { AccentColor = "#123456" } });

        vm.RemoveCustom(vm.Custom[0]);

        Assert.Empty(vm.ToSettings().CustomColors);
        Assert.Equal("#123456", vm.ToSettings().ColorsFor(dark: false).AccentColor);
    }

    [Fact]
    public void Reset_RestoresLastChosenProfile_AccentAndBackground()
    {
        // kein fester Standard mehr; „Zurücksetzen“ stellt das zuletzt gewählte Profil wieder her
        var vm = Create(new AppSettings { Theme = ThemePreference.Light });
        var fjord = vm.Profiles.Single(p => p.Profile.Name == "Profile.Fjord");
        vm.SelectedProfile = fjord;
        vm.ChooseSwatch(vm.Strong.Single(s => s.Hex == "#C93434"));
        vm.Target = AdjustTarget.Background;
        vm.SliderBrightness = 20;
        Assert.Null(vm.SelectedProfile);

        vm.ResetCurrent();

        Assert.Equal(fjord.Profile.Colors, vm.ToSettings().LightColors);
        Assert.Equal(fjord, vm.SelectedProfile);
        Assert.Equal((double)fjord.Profile.Colors.Brightness, vm.SliderBrightness);
        Assert.Contains("Fjord", vm.ResetToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void Reset_WithoutProfile_RestoresColorsFromOpening()
    {
        var opening = new ThemeColorSettings { AccentColor = "#123456", TintHue = 30, TintSaturation = 40, Brightness = -3 };
        var vm = Create(new AppSettings { Theme = ThemePreference.Light, LightColors = opening });
        Assert.Null(vm.SelectedProfile);
        vm.ChooseSwatch(vm.Strong.Single(s => s.Hex == "#C93434"));

        vm.ResetCurrent();

        Assert.Equal(opening.Normalized(), vm.ToSettings().LightColors);
        Assert.Equal(Loc.T("Settings.ResetTipOpening"), vm.ResetToolTip);
    }

    [Fact]
    public void Reset_RemembersLastProfilePerTheme()
    {
        var vm = Create(new AppSettings { Theme = ThemePreference.Light });
        vm.SelectedProfile = vm.Profiles.Single(p => p.Profile.Name == "Profile.Fjord");
        vm.SelectedTheme = vm.Themes.Single(t => t.Theme == ThemePreference.Dark);
        vm.SelectedProfile = vm.Profiles.Single(p => p.Profile.Name == "Profile.Amber");
        vm.ChooseSwatch(vm.Strong.Single(s => s.Hex == "#C93434"));

        vm.ResetCurrent();

        Assert.Equal("#E0A458", vm.ToSettings().ColorsFor(dark: true).AccentColor);
        Assert.Equal("#3C7DA6", vm.ToSettings().ColorsFor(dark: false).AccentColor);
    }

    [Fact]
    public void Swatches_HaveNoDefaultMark()
    {
        var vm = Create(new AppSettings());

        Assert.DoesNotContain(vm.Strong, s => s.ToolTip.Contains("Standard", StringComparison.Ordinal));
    }

    [Fact]
    public void SwitchTheme_LoadsColorsOfThatTheme_KeepsEditsOfTheOther()
    {
        // Hell und Dunkel haben eigene Akzentfarbe und eigenen Hintergrund.
        var vm = Create(new AppSettings
        {
            Theme = ThemePreference.Light,
            LightColors = new() { AccentColor = "#C93434" },
            DarkColors = new() { AccentColor = "#1F9D55", TintHue = 200, TintSaturation = 30, Brightness = -4 },
        });
        Assert.False(vm.EditingDark);
        vm.Target = AdjustTarget.Background;
        vm.SliderSaturation = 20;

        vm.SelectedTheme = vm.Themes.Single(t => t.Theme == ThemePreference.Dark);

        Assert.True(vm.EditingDark);
        Assert.Equal((200d, 30d, -4d), (vm.SliderHue, vm.SliderSaturation, vm.SliderBrightness));
        Assert.True(vm.Strong.Single(s => s.Hex == "#1F9D55").IsSelected);
        Assert.Equal((ThemePreference.Dark, "#1F9D55"), (_theme.Applied[^1].Theme, _theme.Applied[^1].Accent));
        Assert.Contains("Dunkel", vm.EditedThemeHint);

        vm.ChooseSwatch(vm.Strong.Single(s => s.Hex == "#2F6FDF"));
        var settings = vm.ToSettings();
        Assert.Equal(("#C93434", 20), (settings.ColorsFor(dark: false).AccentColor, settings.ColorsFor(dark: false).TintSaturation));
        Assert.Equal(("#2F6FDF", 30), (settings.ColorsFor(dark: true).AccentColor, settings.ColorsFor(dark: true).TintSaturation));
    }

    [Fact]
    public void SystemTheme_EditsColorsOfWhatTheSystemShows()
    {
        _theme.SystemDark = true;
        _theme.Apply(new AppSettings());
        var vm = Create(new AppSettings { DarkColors = new() { AccentColor = "#1F9D55" } });

        Assert.True(vm.EditingDark);
        Assert.True(vm.Strong.Single(s => s.Hex == "#1F9D55").IsSelected);

        vm.SelectedTheme = vm.Themes.Single(t => t.Theme == ThemePreference.Light);

        Assert.False(vm.EditingDark);
        // Hell behält das Startprofil „Standard“ (Indigo, gespeichert als Standard = null, A56)
        Assert.Null(vm.ToSettings().ColorsFor(dark: false).AccentColor);
        Assert.Equal("#1F9D55", vm.ToSettings().ColorsFor(dark: true).AccentColor);
    }

    [Fact]
    public void GeneralSettings_UndoLimitAndUserName_AreTakenOver()
    {
        var vm = Create(new AppSettings { UndoLimit = 80, UserName = "Andy" });
        Assert.Equal(80, vm.UndoLimit);
        Assert.Equal("Andy", vm.UserName);

        vm.UndoLimit = 9999;
        vm.UserName = "  Erika  ";

        Assert.Equal(PdfUtopia.Core.History.UndoHistory.MaxLimit, vm.ToSettings().UndoLimit);
        Assert.Equal("Erika", vm.ToSettings().UserName);
    }

    private SettingsViewModel Create(AppSettings settings)
    {
        PdfUtopia.App.Localization.Loc.Language = UiLanguage.German;
        return new SettingsViewModel(settings, _theme, new FakeDialogs());
    }
}
