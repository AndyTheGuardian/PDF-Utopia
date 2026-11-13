using PdfUtopia.App.Localization;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.Tests;

public sealed class ColorProfileSettingsTests : IDisposable
{
    private readonly FakeTheme _theme = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "AssetManagerProfileUi", Guid.NewGuid().ToString("N"));

    public ColorProfileSettingsTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void Profiles_ShowBuiltInsOfTheme_StandardSelectedByDefault()
    {
        // Startprofil ist „Standard“ (die bisherigen Farben von PDF Utopia), danach „Neutral“
        var vm = Create(new AppSettings { Theme = ThemePreference.Light });

        Assert.Equal(7, vm.Profiles.Count);
        Assert.Equal("Standard", vm.SelectedProfile?.DisplayName);
        Assert.Equal("Neutral", vm.Profiles[1].DisplayName);
        Assert.Equal("Salbei", vm.Profiles[2].DisplayName);
        Assert.False(vm.IsCustomProfileSelected);
        Assert.False(vm.RenameProfileCommand.CanExecute(null));
        Assert.Equal((null, 0), (vm.ToSettings().ColorsFor(dark: false).AccentColor, vm.ToSettings().ColorsFor(dark: false).Brightness));
    }

    [Fact]
    public void ChoosingProfile_AppliesItsColors_AndEditingClearsSelection()
    {
        var vm = Create(new AppSettings { Theme = ThemePreference.Light });

        vm.SelectedProfile = vm.Profiles.Single(p => p.Profile.Name == "Profile.Fjord");

        Assert.Equal("#3C7DA6", vm.AccentHex);
        Assert.Equal("#3C7DA6", _theme.Applied[^1].Accent);
        Assert.Equal("#3C7DA6", vm.ToSettings().ColorsFor(dark: false).AccentColor);

        vm.Target = AdjustTarget.Background;
        vm.SliderBrightness = 7;

        Assert.Null(vm.SelectedProfile);
    }

    [Fact]
    public void ThemeSwitch_ShowsProfilesOfOtherTheme()
    {
        var vm = Create(new AppSettings { Theme = ThemePreference.Light });

        vm.SelectedTheme = vm.Themes.Single(t => t.Theme == ThemePreference.Dark);

        Assert.All(vm.Profiles, p => Assert.True(p.Profile.IsDark));
        Assert.Contains(vm.Profiles, p => p.DisplayName == "Mitternacht");
    }

    [Fact]
    public async Task SaveAs_AddsCustomProfile_SelectedAndSaved()
    {
        var vm = Create(new AppSettings { Theme = ThemePreference.Dark, DarkColors = new() { AccentColor = "#112233", Brightness = 4 } });
        Assert.Null(vm.SelectedProfile);
        _dialogs.PromptAnswer = "  Mein Abend ";

        await vm.SaveProfileAsAsync();

        Assert.Equal("Mein Abend", vm.SelectedProfile?.DisplayName);
        Assert.True(vm.IsCustomProfileSelected);
        var saved = Assert.Single(vm.ToSettings().ColorProfiles);
        Assert.Equal(("Mein Abend", true, "#112233", 4), (saved.Name, saved.IsDark, saved.Colors.AccentColor, saved.Colors.Brightness));
    }

    [Fact]
    public async Task SaveAs_BuiltInName_GetsNumber_ExistingOwnName_ReplacedAfterConfirm()
    {
        var own = new ColorProfile("Abend", false, new ThemeColorSettings { AccentColor = "#AA0000" });
        var vm = Create(new AppSettings { Theme = ThemePreference.Light, ColorProfiles = [own], LightColors = new() { AccentColor = "#00AA00" } });

        _dialogs.PromptAnswer = "salbei";
        await vm.SaveProfileAsAsync();
        Assert.Equal("salbei (2)", vm.SelectedProfile?.DisplayName);

        _dialogs.PromptAnswer = "ABEND";
        await vm.SaveProfileAsAsync();

        Assert.Single(_dialogs.Confirmations);
        var abend = vm.ToSettings().ColorProfiles.Single(p => p.Name == "Abend");
        Assert.Equal("#00AA00", abend.Colors.AccentColor);
        Assert.Equal(2, vm.ToSettings().ColorProfiles.Count);
    }

    [Fact]
    public async Task RenameAndDelete_OnlyCustomProfiles()
    {
        var colors = new ThemeColorSettings { AccentColor = "#AA0000" };
        var vm = Create(new AppSettings
        {
            Theme = ThemePreference.Light,
            LightColors = colors,
            ColorProfiles = [new ColorProfile("Rot", false, colors), new ColorProfile("Blau", false, new ThemeColorSettings { AccentColor = "#0000AA" })],
        });
        Assert.Equal("Rot", vm.SelectedProfile?.DisplayName);

        _dialogs.PromptAnswer = "Blau";
        await vm.RenameProfileAsync();
        Assert.Equal("Blau (2)", vm.SelectedProfile?.DisplayName);

        await vm.DeleteProfileAsync();

        Assert.Null(vm.SelectedProfile);
        Assert.Equal(["Blau"], vm.ToSettings().ColorProfiles.Select(p => p.Name));
        Assert.Equal("#AA0000", vm.ToSettings().ColorsFor(dark: false).AccentColor);
    }

    [Fact]
    public async Task ExportAll_OnlyCustomProfiles_ThenImport_AddsWithUniqueNames()
    {
        var vm = Create(new AppSettings
        {
            Theme = ThemePreference.Light,
            ColorProfiles = [new ColorProfile("Eigen", true, new ThemeColorSettings()), new ColorProfile("Hell eins", false, new ThemeColorSettings { Brightness = 5 })],
        });
        _dialogs.SaveFile = Path.Combine(_folder, "alle.colorprofiles.json");

        await vm.ExportAllProfilesAsync();

        Assert.Equal(Loc.F("Settings.ProfileExported", 2, "alle.colorprofiles.json"), vm.ProfileMessage);
        var written = ColorProfileFile.Read(await File.ReadAllTextAsync(_dialogs.SaveFile, TestContext.Current.CancellationToken));
        Assert.Equal(["Eigen", "Hell eins"], written.Profiles.Select(p => p.Name));

        _dialogs.OpenFile = _dialogs.SaveFile;
        _dialogs.ChoiceAnswer = 1; // „Beide behalten“
        await vm.ImportProfilesAsync();

        Assert.Contains("Eigen, Hell eins", Assert.Single(_dialogs.Choices), StringComparison.Ordinal);
        var imported = vm.ToSettings().ColorProfiles;
        Assert.Equal(4, imported.Count);
        Assert.Contains(imported, p => p.Name == "Hell eins (2)" && !p.IsDark);
        Assert.Contains(imported, p => p.Name == "Eigen (2)" && p.IsDark);
        Assert.Equal(Loc.F("Settings.ProfileImported", 2), vm.ProfileMessage);
        Assert.Equal(7 + 2, vm.Profiles.Count);
    }

    [Fact]
    public async Task ExportSingle_AllowsBuiltIn_ExportAllNeedsCustomProfiles()
    {
        var vm = Create(new AppSettings { Theme = ThemePreference.Dark });
        vm.SelectedProfile = vm.Profiles.Single(p => p.Profile.Name == "Profile.Amber");
        _dialogs.SaveFile = Path.Combine(_folder, "bernstein.colorprofiles.json");

        Assert.False(vm.ExportAllProfilesCommand.CanExecute(null));
        Assert.True(vm.ExportProfileCommand.CanExecute(null));
        await vm.ExportProfileAsync();

        var profile = Assert.Single(ColorProfileFile.Read(await File.ReadAllTextAsync(_dialogs.SaveFile, TestContext.Current.CancellationToken)).Profiles);
        Assert.Equal(("Bernstein", true, "#E0A458"), (profile.Name, profile.IsDark, profile.Colors.AccentColor));
    }

    [Fact]
    public async Task ExportEvery_WritesBuiltInAndCustomProfilesOfBothThemes()
    {
        var vm = Create(new AppSettings { ColorProfiles = [new ColorProfile("Eigen", true, new ThemeColorSettings())] });
        _dialogs.SaveFile = Path.Combine(_folder, "alle.colorprofiles.json");

        await vm.ExportEveryProfileAsync();

        var written = ColorProfileFile.Read(await File.ReadAllTextAsync(_dialogs.SaveFile, TestContext.Current.CancellationToken)).Profiles;
        Assert.Equal(15, written.Count);
        Assert.Contains(written, p => p.Name == "Salbei" && !p.IsDark);
        Assert.Contains(written, p => p.Name == "Mitternacht" && p.IsDark);
        Assert.Contains(written, p => p.Name == "Eigen" && p.IsDark);
        Assert.Equal(Loc.F("Settings.ProfileExported", 15, "alle.colorprofiles.json"), vm.ProfileMessage);
        // Wieder einlesen: unveränderte mitgelieferte entfallen, nur das eigene kommt als Kopie hinzu
        _dialogs.OpenFile = _dialogs.SaveFile;
        _dialogs.ChoiceAnswer = 1; // „Beide behalten“
        await vm.ImportProfilesAsync();

        Assert.Equal(["Eigen", "Eigen (2)"], vm.ToSettings().ColorProfiles.Select(p => p.Name));
        Assert.Equal(Loc.F("Settings.ProfileImported", 1), vm.ProfileMessage);
    }

    [Fact]
    public async Task Import_Overwrite_ReplacesSameNamedCustomProfiles_NeverBuiltIns()
    {
        var vm = Create(new AppSettings
        {
            Theme = ThemePreference.Light,
            ColorProfiles = [new ColorProfile("Abend", false, new ThemeColorSettings { AccentColor = "#111111" })],
        });
        _dialogs.OpenFile = Path.Combine(_folder, "neu.colorprofiles.json");
        await File.WriteAllTextAsync(_dialogs.OpenFile, ColorProfileFile.Write(
        [
            new ColorProfile("abend", false, new ThemeColorSettings { AccentColor = "#222222" }),
            new ColorProfile("Salbei", false, new ThemeColorSettings { AccentColor = "#333333" }),
            new ColorProfile("Abend", true, new ThemeColorSettings { AccentColor = "#444444" }),
        ]), TestContext.Current.CancellationToken);
        _dialogs.ChoiceAnswer = 0; // „Überschreiben“

        await vm.ImportProfilesAsync();

        var profiles = vm.ToSettings().ColorProfiles;
        Assert.Equal(("Abend", "#222222"), (profiles[0].Name, profiles[0].Colors.AccentColor)); // Name und Platz bleiben
        Assert.Contains(profiles, p => p.Name == "Salbei (2)" && !p.IsDark); // mitgeliefert: nie überschreiben
        Assert.Contains(profiles, p => p.Name == "Abend" && p.IsDark); // anderes Design: kein Konflikt
        Assert.Equal(3, profiles.Count);
        Assert.Equal(Loc.F("Settings.ProfileImportedReplaced", 2, 1), vm.ProfileMessage);
    }

    [Fact]
    public async Task Import_ConflictCancelled_ChangesNothing()
    {
        var own = new ColorProfile("Abend", false, new ThemeColorSettings { AccentColor = "#111111" });
        var vm = Create(new AppSettings { Theme = ThemePreference.Light, ColorProfiles = [own] });
        _dialogs.OpenFile = Path.Combine(_folder, "neu.colorprofiles.json");
        await File.WriteAllTextAsync(_dialogs.OpenFile, ColorProfileFile.Write([own with { Colors = new ThemeColorSettings { AccentColor = "#222222" } }]),
            TestContext.Current.CancellationToken);
        _dialogs.ChoiceAnswer = null;

        await vm.ImportProfilesAsync();

        Assert.Equal(own, Assert.Single(vm.ToSettings().ColorProfiles));
        Assert.Null(vm.ProfileMessage);
    }

    [Fact]
    public async Task Import_InvalidFile_ShowsMessage_KeepsProfiles()
    {
        var vm = Create(new AppSettings());
        _dialogs.OpenFile = Path.Combine(_folder, "fremd.json");
        await File.WriteAllTextAsync(_dialogs.OpenFile, """{ "format": "etwas" }""", TestContext.Current.CancellationToken);

        await vm.ImportProfilesAsync();

        Assert.StartsWith("Import nicht möglich", vm.ProfileMessage, StringComparison.Ordinal);
        Assert.Empty(vm.ToSettings().ColorProfiles);
    }

    [Fact]
    public async Task ExportSelected_WritesOneCustomProfile_AndEnablesExportAllAfterSave()
    {
        var vm = Create(new AppSettings { Theme = ThemePreference.Dark, DarkColors = new() { AccentColor = "#E0A458", Brightness = 9 } });
        Assert.False(vm.ExportAllProfilesCommand.CanExecute(null));
        _dialogs.PromptAnswer = "Abend";
        await vm.SaveProfileAsAsync();
        _dialogs.SaveFile = Path.Combine(_folder, "abend.colorprofiles.json");

        Assert.True(vm.ExportAllProfilesCommand.CanExecute(null));
        await vm.ExportProfileAsync();

        var profile = Assert.Single(ColorProfileFile.Read(await File.ReadAllTextAsync(_dialogs.SaveFile, TestContext.Current.CancellationToken)).Profiles);
        Assert.Equal(("Abend", true, "#E0A458", 9), (profile.Name, profile.IsDark, profile.Colors.AccentColor, profile.Colors.Brightness));
    }

    [Fact]
    public void NumberFields_EmptyKeepsValue_ValuesApplyAndClamp()
    {
        var vm = Create(new AppSettings { Theme = ThemePreference.Light });
        vm.Target = AdjustTarget.Background;
        vm.BrightnessInput = 5;
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.BrightnessInput = null; // Feld geleert: keine Ausnahme, Wert bleibt, Feld zeigt ihn wieder

        Assert.Equal(5, vm.SliderBrightness);
        Assert.Equal(5m, vm.BrightnessInput);
        Assert.Contains(nameof(SettingsViewModel.BrightnessInput), changed);

        vm.BrightnessInput = 99;
        vm.HueInput = 120.4m;
        vm.SaturationInput = null;

        Assert.Equal(ThemeTint.MaxBrightness, vm.ToSettings().ColorsFor(dark: false).Brightness);
        Assert.Equal(120, vm.ToSettings().ColorsFor(dark: false).TintHue);
        Assert.Equal(120m, vm.HueInput);
    }

    private SettingsViewModel Create(AppSettings settings)
    {
        Loc.Language = UiLanguage.German;
        return new SettingsViewModel(settings, _theme, _dialogs);
    }
}
