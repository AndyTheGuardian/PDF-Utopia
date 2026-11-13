using System.Collections.ObjectModel;
using PdfUtopia.App.Localization;
using PdfUtopia.App.Services;
using PdfUtopia.Core.History;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PdfUtopia.App.ViewModels;

/// <summary>Wählbare Akzentfarbe im Einstellungsdialog.</summary>
/// <param name="hex">Farbwert.</param>
/// <param name="toolTip">Name und Hex-Code der Farbe.</param>
/// <param name="isCustom">Ob es eine eigene Farbe ist (entfernbar).</param>
public sealed partial class AccentSwatchViewModel(string hex, string toolTip, bool isCustom = false) : ObservableObject
{
    /// <summary>Farbwert.</summary>
    public string Hex { get; } = hex;

    /// <summary>Name und Hex-Code der Farbe.</summary>
    public string ToolTip { get; } = toolTip;

    /// <summary>Ob es eine eigene Farbe ist.</summary>
    public bool IsCustom { get; } = isCustom;

    /// <summary>Ob die Farbe gewählt ist.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Farbprofil in der Auswahl der Einstellungen (A56).</summary>
/// <param name="profile">Das Profil.</param>
public sealed class ColorProfileViewModel(ColorProfile profile)
{
    /// <summary>Das Profil.</summary>
    public ColorProfile Profile { get; } = profile;

    /// <summary>Anzeigename; mitgelieferte Profile übersetzt.</summary>
    public string DisplayName => DisplayNameOf(Profile);

    /// <summary>Ob das Profil mitgeliefert und schreibgeschützt ist.</summary>
    public bool IsBuiltIn => Profile.IsBuiltIn;

    /// <summary>Wirksame Akzentfarbe, für den Farbpunkt.</summary>
    public string AccentHex => Profile.EffectiveAccent();

    /// <summary>Fensterhintergrund mit Farbstich und Helligkeit, für den Farbpunkt.</summary>
    public string BackgroundHex => ThemePalette.For(Profile.IsDark, Profile.Colors.Tint).AppBackground;

    /// <summary>Anzeigename eines Profils.</summary>
    /// <param name="profile">Das Profil.</param>
    /// <returns>Der Name; mitgelieferte übersetzt.</returns>
    public static string DisplayNameOf(ColorProfile profile) => profile.IsBuiltIn ? Loc.T(profile.Name) : profile.Name;

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

/// <summary>Sprache im Einstellungsdialog; <c>null</c> = System.</summary>
/// <param name="Language">Die Sprache.</param>
public sealed record LanguageOption(UiLanguage? Language)
{
    /// <summary>Anzeigename; Sprachen in ihrer eigenen Schreibweise.</summary>
    public string Label => Language switch
    {
        UiLanguage.German => "Deutsch",
        UiLanguage.English => "English",
        _ => Loc.T("Settings.LanguageSystem"),
    };

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>Design im Einstellungsdialog.</summary>
/// <param name="Theme">Das Design.</param>
public sealed record ThemeOption(ThemePreference Theme)
{
    /// <summary>Anzeigename.</summary>
    public string Label => Loc.T("Theme." + Theme);

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>Was die gemeinsamen Regler im Einstellungsdialog bearbeiten (A56).</summary>
public enum AdjustTarget
{
    /// <summary>Die Akzentfarbe: Farbton, Sättigung und Hellwert (HSB) absolut.</summary>
    Accent,

    /// <summary>Der Farbstich des Hintergrunds: Farbton, Stärke und Helligkeitsverschiebung (A56).</summary>
    Background,
}

/// <summary>
/// Einstellungsdialog: Design, Sprache, Akzentfarbe (Farbreihen Pastell, Kräftig, Neutral und eigene Farben), Farbstich und
/// Helligkeit des Hintergrunds, Farbprofile, Rückgängig-Schritte und der Name für Stempel (übernommen aus dem
/// AI Asset Manager, A56). Akzent und Hintergrund teilen sich einen Satz Regler mit
/// Farbkreis und Hex-Feld; ein Umschalter wählt, was sie bearbeiten (A56).
/// Änderungen an der Darstellung wirken sofort als Vorschau; „Abbrechen“ stellt den alten Zustand wieder her.
/// Hell und Dunkel haben eigene Farben (A56): Der Dialog bearbeitet die Farben des gerade aktiven Designs; ein
/// Wechsel des Designs im Dialog lädt dessen Farben, die bisher bearbeiteten bleiben erhalten.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _theme;
    private readonly IDialogService _dialogs;
    private readonly AppSettings _original;
    private readonly bool _initialized;
    private bool _syncing;
    private ThemeColorSettings _light;
    private ThemeColorSettings _dark;
    private readonly List<ColorProfile> _customProfiles;
    private bool _selectingProfile;
    private ColorProfile? _lastLightProfile;
    private ColorProfile? _lastDarkProfile;

    /// <summary>Erzeugt den Dialog mit den aktuellen Einstellungen.</summary>
    /// <param name="settings">Aktuelle Einstellungen.</param>
    /// <param name="theme">Theme-Dienst für die Vorschau.</param>
    /// <param name="dialogs">Für die Ordnerauswahl.</param>
    public SettingsViewModel(AppSettings settings, IThemeService theme, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _original = settings;
        _theme = theme;
        _dialogs = dialogs;
        _light = settings.ColorsFor(dark: false);
        _dark = settings.ColorsFor(dark: true);
        _customProfiles = [.. settings.ColorProfiles];

        Themes = [new(ThemePreference.System), new(ThemePreference.Light), new(ThemePreference.Dark)];
        Languages = [new(null), new(UiLanguage.German), new(UiLanguage.English)];
        SelectedTheme = Themes.First(option => option.Theme == settings.Theme);
        SelectedLanguage = Languages.First(option => option.Language == settings.Language);
        EditingDark = IsDarkTheme(settings.Theme);
        LoadColors(settings.ColorsFor(EditingDark));
        foreach (var hex in settings.CustomColors)
        {
            Custom.Add(CustomSwatch(hex));
        }

        HexInput = string.Empty;
        UndoLimit = settings.UndoLimit;
        UserName = settings.UserName;
        RebuildSwatches();
        SyncSliders();
        RebuildProfiles();
        var otherDark = !EditingDark;
        SetLastProfile(otherDark, AllProfiles(otherDark).FirstOrDefault(p => p.Matches(settings.ColorsFor(otherDark))));
        _initialized = true;
    }

    /// <summary>Designs.</summary>
    public IReadOnlyList<ThemeOption> Themes { get; }

    /// <summary>Sprachen.</summary>
    public IReadOnlyList<LanguageOption> Languages { get; }

    /// <summary>Pastellfarben bei <see cref="PastelSaturation"/>; je kräftiger Farbe eine mit demselben Farbton.</summary>
    public ObservableCollection<AccentSwatchViewModel> Pastel { get; } = [];

    /// <summary>Kräftige Farben.</summary>
    public ObservableCollection<AccentSwatchViewModel> Strong { get; } = [];

    /// <summary>Neutrale Farben (95 % Schwarz, 95 % Weiß).</summary>
    public ObservableCollection<AccentSwatchViewModel> Neutral { get; } = [];

    /// <summary>Eigene Farben.</summary>
    public ObservableCollection<AccentSwatchViewModel> Custom { get; } = [];

    /// <summary>Ob es eigene Farben gibt.</summary>
    public bool HasCustomColors => Custom.Count > 0;

    /// <summary>Farbprofile des bearbeiteten Designs: mitgelieferte, dann eigene (A56).</summary>
    public ObservableCollection<ColorProfileViewModel> Profiles { get; } = [];

    /// <summary>
    /// Profil, dessen Farben gerade gelten; <c>null</c>, wenn die Farben zu keinem Profil passen. Die Wahl eines
    /// Profils übernimmt seine Farben.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomProfileSelected), nameof(HasProfileSelected))]
    [NotifyCanExecuteChangedFor(nameof(RenameProfileCommand), nameof(DeleteProfileCommand), nameof(ExportProfileCommand), nameof(ExportAllProfilesCommand))]
    public partial ColorProfileViewModel? SelectedProfile { get; set; }

    /// <summary>Ob ein eigenes Profil gewählt ist (umbenennen, löschen).</summary>
    public bool IsCustomProfileSelected => SelectedProfile is { IsBuiltIn: false };

    /// <summary>Ob ein Profil gewählt ist (exportieren).</summary>
    public bool HasProfileSelected => SelectedProfile is not null;

    /// <summary>Rückmeldung zu Import und Export.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProfileMessage))]
    public partial string? ProfileMessage { get; set; }

    /// <summary>Ob es eine Rückmeldung gibt.</summary>
    public bool HasProfileMessage => !string.IsNullOrEmpty(ProfileMessage);

    /// <summary>Gewähltes Design.</summary>
    [ObservableProperty]
    public partial ThemeOption SelectedTheme { get; set; }

    /// <summary>Gewählte Sprache.</summary>
    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    /// <summary>Gewählte Akzentfarbe; <c>null</c> = Standard des Themes.</summary>
    [ObservableProperty]
    public partial string? AccentHex { get; set; }

    /// <summary>Ob der Dialog gerade die Farben des dunklen Designs bearbeitet (A56).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditedThemeHint), nameof(ResetToolTip))]
    public partial bool EditingDark { get; private set; }

    /// <summary>Hinweis, für welches Design die Farben gelten.</summary>
    public string EditedThemeHint => Loc.F("Settings.ColorsFor", Loc.T(EditingDark ? "Theme.Dark" : "Theme.Light"));

    /// <summary>Sättigung der ganzen Pastell-Reihe (10–90 %).</summary>
    public int PastelSaturation { get; private set; }

    /// <summary>Farbton des Farbstichs (0–359).</summary>
    [ObservableProperty]
    public partial double TintHue { get; set; }

    /// <summary>Stärke des Farbstichs (0–100).</summary>
    [ObservableProperty]
    public partial double TintSaturation { get; set; }

    /// <summary>Helligkeit des Hintergrunds (−25 bis +25).</summary>
    [ObservableProperty]
    public partial double Brightness { get; set; }

    /// <summary>Was die Regler bearbeiten.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccentTarget), nameof(IsBackgroundTarget),
        nameof(SaturationLabel), nameof(BrightnessMinimum), nameof(BrightnessMaximum), nameof(HexHint))]
    public partial AdjustTarget Target { get; set; }

    /// <summary>Umschalter „Akzentfarbe“.</summary>
    public bool IsAccentTarget
    {
        get => Target == AdjustTarget.Accent;
        set
        {
            if (value)
            {
                Target = AdjustTarget.Accent;
            }
        }
    }

    /// <summary>Umschalter „Hintergrund“.</summary>
    public bool IsBackgroundTarget
    {
        get => Target == AdjustTarget.Background;
        set
        {
            if (value)
            {
                Target = AdjustTarget.Background;
            }
        }
    }

    /// <summary>Regler Farbton (0–359).</summary>
    [ObservableProperty]
    public partial double SliderHue { get; set; }

    /// <summary>Regler Sättigung bzw. Stärke (0–100).</summary>
    [ObservableProperty]
    public partial double SliderSaturation { get; set; }

    /// <summary>Regler Helligkeit: Akzent 0–100 (Hellwert), Hintergrund −25 bis +25.</summary>
    [ObservableProperty]
    public partial double SliderBrightness { get; set; }

    /// <summary>Zahlenfeld zum Farbton; leer wird ignoriert (A56).</summary>
    public decimal? HueInput
    {
        get => (decimal)Math.Round(SliderHue);
        set => ApplyInput(value, 0, 359, v => SliderHue = v, nameof(HueInput));
    }

    /// <summary>Zahlenfeld zu Sättigung bzw. Stärke; leer wird ignoriert (A56).</summary>
    public decimal? SaturationInput
    {
        get => (decimal)Math.Round(SliderSaturation);
        set => ApplyInput(value, SaturationMinimum, SaturationMaximum, v => SliderSaturation = v, nameof(SaturationInput));
    }

    /// <summary>Zahlenfeld zur Helligkeit; leer wird ignoriert (A56).</summary>
    public decimal? BrightnessInput
    {
        get => (decimal)Math.Round(SliderBrightness);
        set => ApplyInput(value, BrightnessMinimum, BrightnessMaximum, v => SliderBrightness = v, nameof(BrightnessInput));
    }

    /// <summary>
    /// Kleinster Wert des Sättigungsreglers: bei einer gewählten Pastellfarbe 10 %, weil der Regler dann die ganze
    /// Pastell-Reihe verschiebt (A56), sonst 0.
    /// </summary>
    public double SaturationMinimum => IsPastelSelected ? AccentPalette.MinSaturation : 0;

    /// <summary>Größter Wert des Sättigungsreglers: bei einer gewählten Pastellfarbe 90 %, sonst 100.</summary>
    public double SaturationMaximum => IsPastelSelected ? AccentPalette.MaxSaturation : 100;

    /// <summary>Ob die Regler gerade eine Pastellfarbe bearbeiten.</summary>
    public bool IsPastelSelected => Target == AdjustTarget.Accent && PastelIndex >= 0;

    /// <summary>Kleinster Wert des Helligkeitsreglers.</summary>
    public double BrightnessMinimum => Target == AdjustTarget.Accent ? 0 : ThemeTint.MinBrightness;

    /// <summary>Größter Wert des Helligkeitsreglers.</summary>
    public double BrightnessMaximum => Target == AdjustTarget.Accent ? 100 : ThemeTint.MaxBrightness;

    /// <summary>Beschriftung des Sättigungsreglers.</summary>
    public string SaturationLabel => Loc.T(Target == AdjustTarget.Accent ? "Settings.Saturation" : "Settings.TintSaturation");

    /// <summary>Beschriftung des Zurücksetzen-Knopfs.</summary>
    public string ResetLabel => Loc.T("Settings.Reset");

    /// <summary>Erklärung zum Zurücksetzen.</summary>
    public string ResetToolTip => LastProfile(EditingDark) is { } last
        ? Loc.F("Settings.ResetTip", ColorProfileViewModel.DisplayNameOf(last))
        : Loc.T("Settings.ResetTipOpening");

    /// <summary>Hinweis neben dem Hex-Feld.</summary>
    public string HexHint => Loc.T(Target == AdjustTarget.Accent ? "Settings.AccentHex" : "Settings.TintHex");

    /// <summary>Farbkreis: wirksame Akzentfarbe bzw. Farbe des Farbstichs (grau ohne Stärke).</summary>
    public string PreviewHex => Target == AdjustTarget.Accent ? EffectiveAccent : CurrentTintHex;

    /// <summary>Eingabe als Hex-Code für das gewählte Ziel.</summary>
    [ObservableProperty]
    public partial string HexInput { get; set; }

    /// <summary>Fehler beim Hex-Code.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHexError))]
    public partial string? HexError { get; set; }

    /// <summary>Ob es einen Fehler beim Hex-Code gibt.</summary>
    public bool HasHexError => HexError is not null;

    /// <summary>Kleinste Anzahl Rückgängig-Schritte.</summary>
    public decimal UndoMinimum => UndoHistory.MinLimit;

    /// <summary>Größte Anzahl Rückgängig-Schritte.</summary>
    public decimal UndoMaximum => UndoHistory.MaxLimit;

    /// <summary>Anzahl Rückgängig-Schritte.</summary>
    [ObservableProperty]
    public partial decimal? UndoLimit { get; set; }

    /// <summary>Name für Stempel.</summary>
    [ObservableProperty]
    public partial string UserName { get; set; }

    private int PastelIndex => AccentHex is null ? -1 : Pastel.ToList().FindIndex(swatch => swatch.Hex == AccentHex);

    private string EffectiveAccent => AccentHex ?? (EditingDark ? AccentPalette.DefaultAccentDark : AccentPalette.DefaultAccent);

    private string CurrentTintHex => ThemePalette.TintColor((int)Math.Round(TintHue), (int)Math.Round(TintSaturation));

    /// <summary>Wählt eine Akzentfarbe aus den Reihen; die Regler wechseln auf „Akzentfarbe“.</summary>
    /// <param name="swatch">Die Farbe.</param>
    [RelayCommand]
    public void ChooseSwatch(AccentSwatchViewModel swatch)
    {
        ArgumentNullException.ThrowIfNull(swatch);
        Target = AdjustTarget.Accent;
        ChooseAccent(swatch.Hex);
    }

    /// <summary>
    /// „Zurücksetzen“: stellt Akzentfarbe, Pastell-Sättigung, Farbstich und Helligkeit des zuletzt gewählten Profils
    /// dieses Designs wieder her; gab es keins, die Farben beim Öffnen des Dialogs (A56).
    /// </summary>
    [RelayCommand]
    public void ResetCurrent()
    {
        LoadColors(LastProfile(EditingDark)?.Colors ?? _original.ColorsFor(EditingDark));
        HexError = null;
        RebuildSwatches();
        SyncSliders();
        Preview();
    }

    /// <summary>
    /// Übernimmt den Hex-Code: als Akzentfarbe (und in „Eigene Farben“) bzw. als Farbstich, dessen Farbton und Stärke
    /// aus der Farbe kommen; die Helligkeit des Hintergrunds bleibt.
    /// </summary>
    [RelayCommand]
    public void ApplyHex()
    {
        if (!AccentPalette.IsValidHex(HexInput))
        {
            HexError = Loc.T("Settings.AccentInvalid");
            return;
        }

        var hex = AccentPalette.Normalize(HexInput);
        if (Target == AdjustTarget.Accent)
        {
            var updated = AccentPalette.AddCustomColor(Custom.Select(swatch => swatch.Hex).ToList(), hex, AccentPalette.DefaultSaturation);
            Custom.Clear();
            foreach (var color in updated)
            {
                Custom.Add(CustomSwatch(color));
            }

            OnPropertyChanged(nameof(HasCustomColors));
            ChooseAccent(hex);
            return;
        }

        var (hue, saturation) = ThemePalette.TintFromColor(hex);
        TintHue = hue;
        TintSaturation = saturation;
        HexError = null;
        SyncSliders();
        HexInput = hex;
        Preview();
    }

    /// <summary>Entfernt eine eigene Farbe; die gewählte Akzentfarbe bleibt.</summary>
    /// <param name="swatch">Die Farbe.</param>
    [RelayCommand]
    public void RemoveCustom(AccentSwatchViewModel swatch)
    {
        Custom.Remove(swatch);
        OnPropertyChanged(nameof(HasCustomColors));
    }

    /// <summary>
    /// Speichert die aktuellen Farben als eigenes Profil des bearbeiteten Designs. Ein gleichnamiges eigenes Profil wird
    /// nach Rückfrage ersetzt; mitgelieferte bleiben unverändert, der Name bekommt dann eine Nummer (A56).
    /// </summary>
    /// <returns>Ende der Eingabe.</returns>
    [RelayCommand]
    public async Task SaveProfileAsAsync()
    {
        var initial = SelectedProfile is { IsBuiltIn: false } own ? own.DisplayName : string.Empty;
        var name = ColorProfile.CleanName(await _dialogs.PromptTextAsync(
            Loc.T("Settings.ProfileSaveAsTitle"), Loc.T("Settings.ProfileName"), initial, Loc.T("Settings.ProfileSave")));
        if (name.Length == 0)
        {
            return;
        }

        var existing = _customProfiles.FindIndex(p => p.IsDark == EditingDark && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        var profile = new ColorProfile(name, EditingDark, CurrentColors().Normalized());
        if (existing >= 0)
        {
            if (!await _dialogs.ConfirmAsync(Loc.T("Settings.ProfileSaveAsTitle"), Loc.F("Settings.ProfileOverwrite", _customProfiles[existing].Name), Loc.T("Settings.ProfileOverwriteConfirm")))
            {
                return;
            }

            profile = profile with { Name = _customProfiles[existing].Name };
            _customProfiles[existing] = profile;
        }
        else
        {
            if (CustomProfileLimitReached())
            {
                return;
            }

            profile = profile with { Name = ColorProfile.UniqueName(BuiltInNames(EditingDark), name) };
            _customProfiles.Add(profile);
        }

        ProfileMessage = null;
        RebuildProfiles(profile);
    }

    /// <summary>Benennt das gewählte eigene Profil um; der Name muss im Design eindeutig sein.</summary>
    /// <returns>Ende der Eingabe.</returns>
    [RelayCommand(CanExecute = nameof(IsCustomProfileSelected))]
    public async Task RenameProfileAsync()
    {
        if (SelectedProfile is not { IsBuiltIn: false } selected)
        {
            return;
        }

        var name = ColorProfile.CleanName(await _dialogs.PromptTextAsync(
            Loc.T("Settings.ProfileRenameTitle"), Loc.T("Settings.ProfileName"), selected.DisplayName, Loc.T("Settings.ProfileRename")));
        if (name.Length == 0 || name == selected.Profile.Name)
        {
            return;
        }

        var index = _customProfiles.IndexOf(selected.Profile);
        var taken = TakenNames(EditingDark).Where(n => !string.Equals(n, selected.Profile.Name, StringComparison.OrdinalIgnoreCase));
        var renamed = selected.Profile with { Name = ColorProfile.UniqueName(taken, name) };
        _customProfiles[index] = renamed;
        RebuildProfiles(renamed);
    }

    /// <summary>Löscht das gewählte eigene Profil nach Rückfrage; die Farben bleiben eingestellt.</summary>
    /// <returns>Ende der Rückfrage.</returns>
    [RelayCommand(CanExecute = nameof(IsCustomProfileSelected))]
    public async Task DeleteProfileAsync()
    {
        if (SelectedProfile is not { IsBuiltIn: false } selected
            || !await _dialogs.ConfirmAsync(Loc.T("Settings.ProfileDeleteTitle"), Loc.F("Settings.ProfileDeleteMessage", selected.DisplayName), Loc.T("Settings.ProfileDelete")))
        {
            return;
        }

        _customProfiles.Remove(selected.Profile);
        RebuildProfiles();
    }

    /// <summary>Exportiert das gewählte Profil in eine Datei (A56), auch ein mitgeliefertes (A56).</summary>
    /// <returns>Ende des Exports.</returns>
    [RelayCommand(CanExecute = nameof(HasProfileSelected))]
    public Task ExportProfileAsync() =>
        SelectedProfile is { } selected ? ExportAsync([selected.Profile], selected.DisplayName) : Task.CompletedTask;

    /// <summary>Ob es eigene Profile gibt (zum Exportieren).</summary>
    public bool HasCustomProfiles => _customProfiles.Count > 0;

    /// <summary>Exportiert alle eigenen Profile beider Designs in eine Datei (A56); mitgelieferte hat jede Installation (A56).</summary>
    /// <returns>Ende des Exports.</returns>
    [RelayCommand(CanExecute = nameof(HasCustomProfiles))]
    public Task ExportAllProfilesAsync() => ExportAsync([.. _customProfiles], Loc.T("Settings.ProfileFileAll"));

    /// <summary>Exportiert alle Profile beider Designs, mitgelieferte und eigene, in eine Datei (A56).</summary>
    /// <returns>Ende des Exports.</returns>
    [RelayCommand]
    public Task ExportEveryProfileAsync() =>
        ExportAsync([.. BuiltInColorProfiles.Light, .. BuiltInColorProfiles.Dark, .. _customProfiles], Loc.T("Settings.ProfileFileEvery"));

    /// <summary>
    /// Liest Profile aus einer Datei und fügt sie als eigene Profile hinzu; gleiche Namen bekommen eine Nummer.
    /// Die eingestellten Farben ändern sich nicht.
    /// </summary>
    /// <returns>Ende des Imports.</returns>
    [RelayCommand]
    public async Task ImportProfilesAsync()
    {
        var path = await _dialogs.PickOpenFileAsync(Loc.T("Settings.ProfileImportTitle"), Loc.T("Settings.ProfileFileType"), "*" + ColorProfileFile.Extension);
        if (path is null)
        {
            return;
        }

        ColorProfileReadResult read;
        try
        {
            read = ColorProfileFile.Read(await File.ReadAllTextAsync(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ColorProfileFormatException)
        {
            ProfileMessage = Loc.F("Settings.ProfileImportFailed", exception.Message);
            return;
        }

        // Unveränderte mitgelieferte Profile (z. B. aus „Alle Profile exportieren“) gibt es schon; sie entfallen (A56).
        var incoming = read.Profiles.Where(profile => !IsBuiltInCopy(profile)).ToList();

        // Gleichnamige eigene Profile: überschreiben oder beide behalten (A56). Mitgelieferte werden nie überschrieben.
        var conflicts = incoming.Where(profile => FindCustom(profile) >= 0).Select(profile => profile.Name).Distinct().ToList();
        var overwrite = false;
        if (conflicts.Count > 0)
        {
            var choice = await _dialogs.ChooseAsync(
                Loc.T("Settings.ProfileImportTitle"),
                Loc.F("Settings.ProfileImportConflict", string.Join(", ", conflicts)),
                [Loc.T("Settings.ProfileImportOverwrite"), Loc.T("Settings.ProfileImportKeepBoth")]);
            if (choice is null)
            {
                return;
            }

            overwrite = choice == 0;
        }

        var imported = 0;
        var replaced = 0;
        foreach (var profile in incoming)
        {
            var existing = overwrite ? FindCustom(profile) : -1;
            if (existing >= 0)
            {
                _customProfiles[existing] = profile with { Name = _customProfiles[existing].Name };
                replaced++;
                continue;
            }

            if (_customProfiles.Count >= ColorProfile.MaxCustomProfiles)
            {
                continue;
            }

            _customProfiles.Add(profile with { Name = ColorProfile.UniqueName(TakenNames(profile.IsDark), profile.Name) });
            imported++;
        }

        var skipped = read.Skipped + incoming.Count - imported - replaced;
        ProfileMessage = (replaced, skipped) switch
        {
            (0, 0) => Loc.F("Settings.ProfileImported", imported),
            (0, _) => Loc.F("Settings.ProfileImportedSkipped", imported, skipped),
            (_, 0) => Loc.F("Settings.ProfileImportedReplaced", imported, replaced),
            _ => Loc.F("Settings.ProfileImportedReplacedSkipped", imported, replaced, skipped),
        };
        RebuildProfiles();
    }

    /// <summary>Ob ein Profil einem mitgelieferten gleicht: gleiches Design, gleicher angezeigter Name, gleiche Farben.</summary>
    private static bool IsBuiltInCopy(ColorProfile profile) =>
        BuiltInColorProfiles.For(profile.IsDark).Any(builtIn =>
            string.Equals(ColorProfileViewModel.DisplayNameOf(builtIn), profile.Name, StringComparison.OrdinalIgnoreCase)
            && builtIn.Matches(profile.Colors));

    /// <summary>Position des gleichnamigen eigenen Profils im selben Design, sonst −1.</summary>
    private int FindCustom(ColorProfile profile) =>
        _customProfiles.FindIndex(p => p.IsDark == profile.IsDark && string.Equals(p.Name, profile.Name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Die Einstellungen mit den Werten des Dialogs.</summary>
    /// <returns>Die neuen Einstellungen.</returns>
    public AppSettings ToSettings() => (_original with
    {
        Theme = SelectedTheme.Theme,
        Language = SelectedLanguage.Language,
        LightColors = EditingDark ? _light : CurrentColors(),
        DarkColors = EditingDark ? CurrentColors() : _dark,
        CustomColors = Custom.Select(swatch => swatch.Hex).ToList(),
        ColorProfiles = _customProfiles.ToList(),
        UndoLimit = UndoHistory.ClampLimit((int)(UndoLimit ?? _original.UndoLimit)),
        UserName = (UserName ?? string.Empty).Trim(),
    }).Normalized();

    /// <summary>Stellt nach „Abbrechen“ die alte Darstellung wieder her.</summary>
    public void Revert() => _theme.Apply(_original);

    /// <summary>
    /// Wendet eine gewählte Sprache sofort an, wie das Design (P22); setzt der Aufrufer, der das Hauptfenster neu
    /// aufbaut. <c>null</c> = System.
    /// </summary>
    public Action<UiLanguage?>? PreviewLanguage { get; set; }

    /// <summary>
    /// Wird ausgelöst, nachdem eine andere Sprache angewandt wurde: Der Dialog muss in der neuen Sprache neu
    /// aufgebaut werden; alle Eingaben bleiben in diesem ViewModel erhalten.
    /// </summary>
    public event EventHandler? LanguageApplied;

    /// <summary>Andere Sprache gewählt: sofort anwenden (P22).</summary>
    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (!_initialized || PreviewLanguage is null)
        {
            return;
        }

        PreviewLanguage(value.Language);
        LanguageApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Anderes Design gewählt: Die bisher bearbeiteten Farben bleiben für ihr Design erhalten, Regler und Farbreihen
    /// zeigen die Farben des neuen Designs (A56). Beim Standard-Akzent folgt außerdem der Farbkreis.
    /// </summary>
    partial void OnSelectedThemeChanged(ThemeOption value)
    {
        if (!_initialized)
        {
            return;
        }

        if (EditingDark)
        {
            _dark = CurrentColors();
        }
        else
        {
            _light = CurrentColors();
        }

        // Erst anwenden: Bei „System“ steht danach fest, welches Design das Betriebssystem zeigt.
        Preview();
        var dark = IsDarkTheme(value.Theme);
        if (dark != EditingDark)
        {
            EditingDark = dark;
            LoadColors(dark ? _dark : _light);
            HexError = null;
            RebuildSwatches();
            RebuildProfiles();
        }

        SyncSliders();
    }

    /// <summary>Ein Profil wurde gewählt: seine Farben gelten ab sofort als Vorschau.</summary>
    partial void OnSelectedProfileChanged(ColorProfileViewModel? value)
    {
        if (value is not null)
        {
            SetLastProfile(EditingDark, value.Profile);
        }

        if (_selectingProfile || !_initialized || value is null)
        {
            return;
        }

        LoadColors(value.Profile.Colors);
        HexError = null;
        RebuildSwatches();
        SyncSliders();
        Preview();
    }

    partial void OnTargetChanged(AdjustTarget value)
    {
        HexError = null;
        SyncSliders();
    }

    partial void OnSliderHueChanged(double value)
    {
        OnPropertyChanged(nameof(HueInput));
        OnSliderChanged(saturationOnly: false);
    }

    partial void OnSliderSaturationChanged(double value)
    {
        OnPropertyChanged(nameof(SaturationInput));
        OnSliderChanged(saturationOnly: true);
    }

    partial void OnSliderBrightnessChanged(double value)
    {
        OnPropertyChanged(nameof(BrightnessInput));
        OnSliderChanged(saturationOnly: false);
    }

    /// <summary>
    /// Übernimmt eine Eingabe aus einem Zahlenfeld. Ein geleertes Feld liefert <c>null</c>; dann bleibt der bisherige
    /// Wert und das Feld zeigt ihn wieder an (A56).
    /// </summary>
    private void ApplyInput(decimal? value, double minimum, double maximum, Action<double> apply, string propertyName)
    {
        if (value is { } number)
        {
            apply(Math.Clamp(Math.Round((double)number), minimum, maximum));
        }

        OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// Ein Regler wurde bewegt: Wert auf das gewählte Ziel übertragen, ohne die Regler zurückzusetzen. Bei einer
    /// gewählten Pastellfarbe verschiebt die Sättigung die ganze Pastell-Reihe; die Auswahl bleibt auf derselben
    /// Pastellfarbe, statt eine neue Einzelfarbe zu erzeugen (A56).
    /// </summary>
    private void OnSliderChanged(bool saturationOnly)
    {
        if (_syncing || !_initialized)
        {
            return;
        }

        if (saturationOnly && IsPastelSelected)
        {
            var index = PastelIndex;
            PastelSaturation = AccentPalette.ClampSaturation((int)Math.Round(SliderSaturation));
            Fill(Pastel, AccentPalette.Pastel(PastelSaturation));
            ChooseAccent(Pastel[index].Hex);
            return;
        }

        if (Target == AdjustTarget.Accent)
        {
            AccentHex = AccentPalette.FromHsv(SliderHue, SliderSaturation / 100, SliderBrightness / 100);
            RebuildSwatches();
        }
        else
        {
            TintHue = SliderHue;
            TintSaturation = SliderSaturation;
            Brightness = SliderBrightness;
        }

        HexError = null;
        HexInput = PreviewHex;
        OnPropertyChanged(nameof(PreviewHex));
        Preview();
    }

    private bool IsDarkTheme(ThemePreference theme) => theme switch
    {
        ThemePreference.Dark => true,
        ThemePreference.Light => false,
        _ => _theme.IsDark,
    };

    private ThemeColorSettings CurrentColors() => new()
    {
        AccentColor = AccentHex,
        PastelSaturation = PastelSaturation,
        TintHue = (int)Math.Round(TintHue),
        TintSaturation = (int)Math.Round(TintSaturation),
        Brightness = (int)Math.Round(Brightness),
    };

    private void LoadColors(ThemeColorSettings colors)
    {
        AccentHex = colors.AccentColor;
        PastelSaturation = colors.PastelSaturation;
        TintHue = colors.TintHue;
        TintSaturation = colors.TintSaturation;
        Brightness = colors.Brightness;
    }

    private void ChooseAccent(string? hex)
    {
        AccentHex = hex;
        HexError = null;
        RebuildSwatches();
        SyncSliders();
        Preview();
    }

    /// <summary>Stellt Regler, Farbkreis und Hex-Feld auf die Werte des gewählten Ziels.</summary>
    private void SyncSliders()
    {
        _syncing = true;
        try
        {
            // Bereich zuerst melden, damit der Regler den neuen Wert nicht auf den alten Bereich beschneidet.
            OnPropertyChanged(nameof(IsPastelSelected));
            OnPropertyChanged(nameof(SaturationMinimum));
            OnPropertyChanged(nameof(SaturationMaximum));
            if (Target == AdjustTarget.Accent)
            {
                var (hue, saturation, value) = AccentPalette.ToHsv(EffectiveAccent);
                SliderHue = Math.Round(hue);
                SliderSaturation = IsPastelSelected ? PastelSaturation : Math.Round(saturation * 100);
                SliderBrightness = Math.Round(value * 100);
            }
            else
            {
                SliderHue = TintHue;
                SliderSaturation = TintSaturation;
                SliderBrightness = Brightness;
            }

            HexInput = PreviewHex;
            OnPropertyChanged(nameof(PreviewHex));
        }
        finally
        {
            _syncing = false;
        }
    }

    private void RebuildSwatches()
    {
        Fill(Pastel, AccentPalette.Pastel(PastelSaturation));
        Fill(Strong, AccentPalette.Strong);
        Fill(Neutral, AccentPalette.Neutral);
        foreach (var swatch in Pastel.Concat(Strong).Concat(Neutral).Concat(Custom))
        {
            swatch.IsSelected = swatch.Hex == AccentHex;
        }
    }

    private static void Fill(ObservableCollection<AccentSwatchViewModel> target, IEnumerable<ColorSwatch> palette)
    {
        target.Clear();
        foreach (var swatch in palette)
        {
            target.Add(new AccentSwatchViewModel(swatch.Hex, $"{Loc.T(swatch.NameKey)} {swatch.Hex}"));
        }
    }

    private static AccentSwatchViewModel CustomSwatch(string hex) => new(hex, $"{Loc.T("Settings.AccentCustomName")} {hex}", isCustom: true);

    private void Preview()
    {
        // Während des Konstruktors sind noch nicht alle Werte gesetzt.
        if (!_initialized)
        {
            return;
        }

        _theme.Apply(ToSettings());
        SelectMatchingProfile();
    }

    /// <summary>Füllt die Profilauswahl für das bearbeitete Design und wählt das passende Profil.</summary>
    /// <param name="preferred">Profil, das gewählt werden soll, wenn seine Farben gelten (z. B. gerade gespeichert).</param>
    private void RebuildProfiles(ColorProfile? preferred = null)
    {
        _selectingProfile = true;
        try
        {
            Profiles.Clear();
            foreach (var profile in BuiltInColorProfiles.For(EditingDark).Concat(_customProfiles.Where(p => p.IsDark == EditingDark)))
            {
                Profiles.Add(new ColorProfileViewModel(profile));
            }
        }
        finally
        {
            _selectingProfile = false;
        }

        OnPropertyChanged(nameof(HasCustomProfiles));
        ExportAllProfilesCommand.NotifyCanExecuteChanged();
        SelectMatchingProfile(preferred);
    }

    /// <summary>
    /// Wählt das Profil, dessen Farben gelten: das bisher gewählte, wenn es noch passt, sonst das erste passende
    /// eigene, sonst das erste passende mitgelieferte; ohne Treffer keines.
    /// </summary>
    private void SelectMatchingProfile(ColorProfile? preferred = null)
    {
        var colors = CurrentColors();
        var match = Profiles.FirstOrDefault(p => preferred is not null && p.Profile == preferred && p.Profile.Matches(colors))
            ?? (SelectedProfile is { } current && Profiles.Contains(current) && current.Profile.Matches(colors) ? current : null)
            ?? Profiles.Where(p => !p.IsBuiltIn).Concat(Profiles.Where(p => p.IsBuiltIn)).FirstOrDefault(p => p.Profile.Matches(colors));
        _selectingProfile = true;
        try
        {
            SelectedProfile = match;
        }
        finally
        {
            _selectingProfile = false;
        }
    }

    private async Task ExportAsync(IReadOnlyList<ColorProfile> profiles, string suggestedName)
    {
        var path = await _dialogs.PickSaveFileAsync(Loc.T("Settings.ProfileExportTitle"), suggestedName + ColorProfileFile.Extension,
            Loc.T("Settings.ProfileFileType"), "*" + ColorProfileFile.Extension);
        if (path is null)
        {
            return;
        }

        try
        {
            var named = profiles.Select(p => p with { Name = ColorProfileViewModel.DisplayNameOf(p) });
            await File.WriteAllTextAsync(path, ColorProfileFile.Write(named));
            ProfileMessage = Loc.F("Settings.ProfileExported", profiles.Count, Path.GetFileName(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ProfileMessage = Loc.F("Settings.ProfileExportFailed", exception.Message);
        }
    }

    private bool CustomProfileLimitReached()
    {
        if (_customProfiles.Count < ColorProfile.MaxCustomProfiles)
        {
            return false;
        }

        ProfileMessage = Loc.F("Settings.ProfileLimit", ColorProfile.MaxCustomProfiles);
        return true;
    }

    private ColorProfile? LastProfile(bool dark) => dark ? _lastDarkProfile : _lastLightProfile;

    private void SetLastProfile(bool dark, ColorProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        if (dark)
        {
            _lastDarkProfile = profile;
        }
        else
        {
            _lastLightProfile = profile;
        }

        OnPropertyChanged(nameof(ResetToolTip));
    }

    private IEnumerable<ColorProfile> AllProfiles(bool dark) =>
        BuiltInColorProfiles.For(dark).Concat(_customProfiles.Where(p => p.IsDark == dark));

    private static IEnumerable<string> BuiltInNames(bool dark) => BuiltInColorProfiles.For(dark).Select(ColorProfileViewModel.DisplayNameOf);

    private IEnumerable<string> TakenNames(bool dark) =>
        BuiltInNames(dark).Concat(_customProfiles.Where(p => p.IsDark == dark).Select(p => p.Name));
}
