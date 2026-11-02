using PdfUtopia.Core.Theming;

namespace PdfUtopia.Core.Tests.Theming;

/// <summary>
/// Tests der gemeinsamen Farbwahl (A14–A16), abgestimmt mit den im Mockup geprüften Werten.
/// </summary>
public sealed class AccentPaletteTests
{
    private const string LightSurface = "#F7F8FA";
    private const string DarkSurface = "#1F1F1F";

    [Fact]
    public void Palettes_ContainYellowBetweenGreenAndOrange()
    {
        var strong = AccentPalette.Strong.Select(swatch => swatch.NameKey).ToList();
        var pastel = AccentPalette.PastelBase.Select(swatch => swatch.NameKey).ToList();

        Assert.Equal(strong.IndexOf("Color.Green") + 1, strong.IndexOf("Color.Yellow"));
        Assert.Equal(strong.IndexOf("Color.Orange") - 1, strong.IndexOf("Color.Yellow"));
        Assert.Equal(pastel.IndexOf("Color.Mint") + 1, pastel.IndexOf("Color.PastelYellow"));
        Assert.Equal("#FACC15", AccentPalette.Strong.Single(swatch => swatch.NameKey == "Color.Yellow").Hex);
    }

    [Fact]
    public void DefaultAccent_IsIndigoAndFirstStrongColor() =>
        Assert.Equal(AccentPalette.DefaultAccent, AccentPalette.Strong[0].Hex);

    [Theory]
    [InlineData(30, "#A2C4E7")]
    [InlineData(50, "#74ADE7")]
    [InlineData(90, "#177FE7")]
    [InlineData(10, "#D0DBE7")]
    public void Pastel_BlueFollowsHsvSaturation(int saturation, string expected) =>
        Assert.Equal(expected, AccentPalette.Pastel(saturation)[0].Hex);

    [Fact]
    public void Pastel_YellowAtDefaultSaturation() =>
        Assert.Equal("#FFECB3", AccentPalette.Pastel(AccentPalette.DefaultSaturation)[2].Hex);

    [Theory]
    [InlineData(5, 10)]
    [InlineData(95, 90)]
    [InlineData(30, 30)]
    public void ClampSaturation_LimitsRange(int requested, int expected) =>
        Assert.Equal(expected, AccentPalette.ClampSaturation(requested));

    [Theory]
    [InlineData("#25A0C3", true)]
    [InlineData(" #25a0c3 ", true)]
    [InlineData("#12XZ", false)]
    [InlineData("25A0C3", false)]
    [InlineData(null, false)]
    public void IsValidHex(string? text, bool expected) => Assert.Equal(expected, AccentPalette.IsValidHex(text));

    [Theory]
    [InlineData("#FACC15", "#1D2330")]
    [InlineData("#A2C4E7", "#1D2330")]
    [InlineData("#4F46E5", "#F4F7FC")]
    [InlineData("#1F9D55", "#F4F7FC")]
    public void ForegroundFor_PicksReadableText(string accent, string expected) =>
        Assert.Equal(expected, AccentPalette.ForegroundFor(accent));

    [Theory]
    [InlineData("#FACC15", LightSurface, true)]
    [InlineData("#F2F2F2", LightSurface, true)]
    [InlineData("#4F46E5", LightSurface, false)]
    [InlineData("#FACC15", DarkSurface, false)]
    [InlineData("#0D0D0D", DarkSurface, true)]
    public void NeedsOutline_BelowMinimumContrast(string accent, string surface, bool expected) =>
        Assert.Equal(expected, AccentPalette.NeedsOutline(accent, surface));

    [Fact]
    public void OutlineColor_YellowOnLight_IsSaturatedGold()
    {
        var outline = AccentPalette.OutlineColor("#FACC15", LightSurface, isDarkTheme: false);

        Assert.Equal("#D5AD12", outline);
        Assert.True(AccentPalette.ContrastRatio(outline!, LightSurface) >= AccentPalette.OutlineContrastRatio);
    }

    [Theory]
    [InlineData("#F2F2F2", LightSurface, false)]
    [InlineData("#0D0D0D", DarkSurface, true)]
    public void OutlineColor_NeutralColors_UseTextColor(string accent, string surface, bool dark) =>
        Assert.Null(AccentPalette.OutlineColor(accent, surface, dark));

    [Fact]
    public void OutlineColor_DarkTheme_LightensColor()
    {
        var outline = AccentPalette.OutlineColor("#2B1D5C", DarkSurface, isDarkTheme: true);

        Assert.NotNull(outline);
        Assert.True(AccentPalette.RelativeLuminance(outline) > AccentPalette.RelativeLuminance("#2B1D5C"));
    }

    [Fact]
    public void AddCustomColor_SkipsFixedAndDuplicateColorsAndKeepsTen()
    {
        IReadOnlyList<string> colors = [];
        colors = AccentPalette.AddCustomColor(colors, "#2F6FDF", 30);
        Assert.Empty(colors);

        colors = AccentPalette.AddCustomColor(colors, "#336699", 30);
        colors = AccentPalette.AddCustomColor(colors, "#336699", 30);
        Assert.Equal(["#336699"], colors);

        for (var i = 0; i < 12; i++)
        {
            colors = AccentPalette.AddCustomColor(colors, $"#0000{i:X2}", 30);
        }

        Assert.Equal(AccentPalette.MaxCustomColors, colors.Count);
        Assert.DoesNotContain("#336699", colors);
        Assert.Equal("#00000B", colors[^1]);
    }

    [Theory]
    [InlineData("#4F46E5", "#4F46E5")]
    [InlineData("#F2F2F2", DrawingColors.FallbackCommentBorder)]
    [InlineData("#FFECB3", DrawingColors.FallbackCommentBorder)]
    public void CommentBorder_FallsBackToGrayOnPaper(string comment, string expected) =>
        Assert.Equal(expected, DrawingColors.CommentBorder(comment));
}

/// <summary>
/// Tests der Hinterlegung von Suchtreffern: lesbar bei allen Farben der Farbwahl, sichtbar auch bei hellen Farben.
/// </summary>
public sealed class SearchHighlightTests
{
    public static TheoryData<string> AllPaletteColors()
    {
        var data = new TheoryData<string>();
        foreach (var hex in AccentPalette.Pastel(AccentPalette.DefaultSaturation).Concat(AccentPalette.Pastel(AccentPalette.MaxSaturation))
                     .Concat(AccentPalette.Strong).Concat(AccentPalette.Neutral).Select(swatch => swatch.Hex).Append(AccentPalette.DefaultAccentDark))
        {
            data.Add(hex);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllPaletteColors))]
    public void Highlight_KeepsDarkTextReadable(string accent)
    {
        var (opacity, _) = DrawingColors.SearchHighlight(accent);
        var fill = AccentPalette.Mix(DrawingColors.Paper, accent, opacity);

        Assert.True(AccentPalette.ContrastRatio(fill, "#000000") >= 10, $"{accent}: Kontrast {AccentPalette.ContrastRatio(fill, "#000000"):0.0}");
        Assert.InRange(opacity, DrawingColors.MinSearchHighlightOpacity, DrawingColors.MaxSearchHighlightOpacity);
    }

    [Theory]
    [MemberData(nameof(AllPaletteColors))]
    public void Highlight_IsVisibleOnPaper_ByFillOrOutline(string accent)
    {
        var (opacity, outline) = DrawingColors.SearchHighlight(accent);
        var fill = AccentPalette.Mix(DrawingColors.Paper, accent, opacity);

        Assert.True(AccentPalette.ContrastRatio(fill, DrawingColors.Paper) >= DrawingColors.MinSearchHighlightVisibility || outline is not null);
    }

    [Fact]
    public void Highlight_DarkAccent_IsLighter() =>
        Assert.True(DrawingColors.SearchHighlight("#0D0D0D").Opacity < DrawingColors.SearchHighlight("#2F6FDF").Opacity);

    [Theory]
    [InlineData("#F2F2F2")]
    [InlineData("#FFF9E6")]
    public void Highlight_PaleAccent_GetsOutline(string accent) => Assert.NotNull(DrawingColors.SearchHighlight(accent).OutlineHex);

    [Fact]
    public void Highlight_ClearYellow_IsVisibleWithoutOutline() => Assert.Null(DrawingColors.SearchHighlight("#FACC15").OutlineHex);

    [Fact]
    public void Highlight_Indigo_NoOutline() => Assert.Null(DrawingColors.SearchHighlight(AccentPalette.DefaultAccent).OutlineHex);
}
