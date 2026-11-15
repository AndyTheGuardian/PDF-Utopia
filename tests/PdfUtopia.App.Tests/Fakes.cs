using PdfUtopia.App.Services;
using PdfUtopia.App.ViewModels;
using PdfUtopia.Core.Signatures;
using PdfUtopia.Pdf.Export;
using PdfUtopia.Core.Settings;
using PdfUtopia.Core.Theming;

namespace PdfUtopia.App.Tests;

/// <summary>Dialoge für die Tests des Einstellungsdialogs; übrige Dialoge werden hier nicht gebraucht.</summary>
internal sealed class FakeDialogs : IDialogService
{
    public bool SettingsResult { get; set; }

    public Action<SettingsViewModel>? EditSettings { get; set; }

    public List<string> Messages { get; } = [];

    public bool ConfirmResult { get; set; } = true;

    public List<string> Confirmations { get; } = [];

    public string? PromptAnswer { get; set; }

    public string? OpenFile { get; set; }

    public string? SaveFile { get; set; }

    public int? ChoiceAnswer { get; set; }

    public List<string> Choices { get; } = [];

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        Confirmations.Add(message);
        return Task.FromResult(ConfirmResult);
    }

    public Task<int?> ChooseAsync(string title, string message, IReadOnlyList<string> choices)
    {
        Choices.Add(message);
        return Task.FromResult(ChoiceAnswer);
    }

    public Task<string?> PromptTextAsync(string title, string label, string initial, string confirmText) => Task.FromResult(PromptAnswer);

    public Task<string?> PickOpenFileAsync(string title, string fileTypeName, string pattern) => Task.FromResult(OpenFile);

    public Task<string?> PickSaveFileAsync(string title, string suggestedName, string fileTypeName, string pattern) => Task.FromResult(SaveFile);

    public Task<bool> ShowSettingsAsync(SettingsViewModel viewModel)
    {
        EditSettings?.Invoke(viewModel);
        return Task.FromResult(SettingsResult);
    }

    public Task<IReadOnlyList<string>> PickPdfFilesAsync() => throw new NotSupportedException();

    public Task<string?> PickSaveFileAsync(string suggestedName) => throw new NotSupportedException();

    public Task<string?> PromptPasswordAsync(string fileName, bool previousAttemptWrong) => throw new NotSupportedException();

    public Task<(PasswordMode Mode, string? NewPassword)?> AskPasswordModeAsync(bool sourceIsProtected) => throw new NotSupportedException();

    public Task<IReadOnlyList<int>?> AskPagesToExtractAsync(int pageCount, string? preset = null) => throw new NotSupportedException();

    public Task<string?> PickPdfFileAsync() => throw new NotSupportedException();

    public Task<InsertChoice?> AskPagesToInsertAsync(InsertRequest request) => throw new NotSupportedException();

    public Task<IReadOnlyList<Signature>> EditSignaturesAsync(IReadOnlyList<Signature> signatures, int startSlot, Action<IReadOnlyList<Signature>> persist) => throw new NotSupportedException();

    public Task<string?> PickImageFileAsync() => throw new NotSupportedException();

    public Task<CustomStamp?> AskCustomStampAsync(string detail) => throw new NotSupportedException();

    public Task<LoadedImage?> ReadClipboardImageAsync() => throw new NotSupportedException();

    public Task<bool> ConfirmRedactionAsync(IReadOnlyList<int> pageNumbers, int areaCount, string confirmText) => throw new NotSupportedException();

    public Task<OcrOutcome?> RecognizeTextAsync(OcrRequest request) => throw new NotSupportedException();

    public Task<CompressOutcome?> CompressAsync(CompressRequest request) => throw new NotSupportedException();

    public Task<bool> ShowTesseractMissingAsync() => throw new NotSupportedException();

    public Task<UnsavedChangesChoice> AskUnsavedChangesAsync(string fileName) => throw new NotSupportedException();
}

/// <summary>Merkt sich angewandte Designs und Farben, statt die Oberfläche zu ändern.</summary>
internal sealed class FakeTheme : IThemeService
{
    public List<(ThemePreference Theme, string? Accent, ThemeTint Tint)> Applied { get; } = [];

    /// <summary>Was das Betriebssystem bei „System“ zeigt.</summary>
    public bool SystemDark { get; set; }

    public bool IsDark { get; private set; }

    /// <summary>Merkt sich Design und die Farben des danach aktiven Designs.</summary>
    public void Apply(AppSettings settings)
    {
        IsDark = settings.Theme switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => SystemDark,
        };
        var colors = settings.ColorsFor(IsDark);
        Applied.Add((settings.Theme, colors.AccentColor, colors.Tint));
    }
}
