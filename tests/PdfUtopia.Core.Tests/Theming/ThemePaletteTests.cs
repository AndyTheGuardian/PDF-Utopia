using PdfUtopia.Core.Theming;

namespace PdfUtopia.Core.Tests.Theming;

public sealed class ThemePaletteTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void For_Neutral_ReturnsBaseColors(bool dark)
    {
        Assert.Same(dark ? ThemePalette.DarkBase : ThemePalette.LightBase, ThemePalette.For(dark, ThemeTint.Neutral));
    }

    [Fact]
    public void Base_IsPreviousPdfUtopiaLook()
    {
        // A56: ohne Farbstich sieht PDF Utopia aus wie vor den Farbprofilen
        Assert.Equal("#F7F8FA", ThemePalette.LightBase.Surface);
        Assert.Equal("#ECEEF2", ThemePalette.LightBase.AppBackground);
        Assert.Equal("#1F1F1F", ThemePalette.DarkBase.Surface);
        Assert.Equal("#181818", ThemePalette.DarkBase.AppBackground);
    }

    [Theory]
    [InlineData("#F7F8FA")]
    [InlineData("#1F1F1F")]
    [InlineData("#2F6FDF")]
    [InlineData("#808080")]
    public void Hsl_RoundTrip_KeepsColor(string hex)
    {
        var (h, s, l) = ThemePalette.ToHsl(hex);

        Assert.Equal(hex, ThemePalette.FromHsl(h, s, l));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void For_Tint_ColorsSurfacesVisibly_KeepsLightness(bool dark)
    {
        var colors = ThemePalette.For(dark, new ThemeTint(Hue: 30, Saturation: 100, Brightness: 0));
        var source = dark ? ThemePalette.DarkBase : ThemePalette.LightBase;

        var (hue, _, lightness) = ThemePalette.ToHsl(colors.AppBackground);
        var (_, _, baseLightness) = ThemePalette.ToHsl(source.AppBackground);
        Assert.InRange(hue, 15, 45); // 8-Bit-Rundung verschiebt den Farbton etwas
        Assert.InRange(ThemePalette.Chroma(colors.AppBackground), ThemePalette.MaxTintChroma - 0.02, ThemePalette.MaxTintChroma + 0.02);
        Assert.InRange(lightness, baseLightness - 0.01, baseLightness + 0.01);
    }

    [Fact]
    public void For_Tint_TextGetsHalfTint()
    {
        var colors = ThemePalette.For(dark: false, new ThemeTint(30, 100, 0));

        Assert.True(ThemePalette.Chroma(colors.Text) < ThemePalette.Chroma(colors.Border));
    }

    [Fact]
    public void For_Brightness_LightensSurfaces_StaysBelowWhite()
    {
        var colors = ThemePalette.For(dark: false, new ThemeTint(0, 0, ThemeTint.MaxBrightness));

        var (_, _, background) = ThemePalette.ToHsl(colors.AppBackground);
        var (_, _, surface) = ThemePalette.ToHsl(colors.Surface);
        var (_, _, baseBackground) = ThemePalette.ToHsl(ThemePalette.LightBase.AppBackground);
        Assert.True(background > baseBackground);
        Assert.True(surface <= ThemePalette.MaxLightness);
        Assert.Equal(ThemePalette.LightBase.Text, colors.Text);
    }

    [Fact]
    public void For_Darker_DarkTheme_StaysAboveBlack()
    {
        var colors = ThemePalette.For(dark: true, new ThemeTint(0, 0, ThemeTint.MinBrightness));

        var (_, _, background) = ThemePalette.ToHsl(colors.AppBackground);
        Assert.True(background >= ThemePalette.MinLightness - 0.003);
    }

    [Fact]
    public void For_Brightness_Range_AllowsMuchLighterDarkTheme()
    {
        // Bereich ±25 Prozentpunkte; das dunkle Theme kann deutlich heller werden, das helle deutlich dunkler.
        var (_, _, dark) = ThemePalette.ToHsl(ThemePalette.For(dark: true, new ThemeTint(0, 0, ThemeTint.MaxBrightness)).AppBackground);
        var (_, _, light) = ThemePalette.ToHsl(ThemePalette.For(dark: false, new ThemeTint(0, 0, ThemeTint.MinBrightness)).Surface);
        var (_, _, darkBase) = ThemePalette.ToHsl(ThemePalette.DarkBase.AppBackground);
        var (_, _, lightBase) = ThemePalette.ToHsl(ThemePalette.LightBase.Surface);

        Assert.Equal(25, ThemeTint.MaxBrightness);
        Assert.InRange(dark, darkBase + 0.24, darkBase + 0.26);
        Assert.InRange(light, lightBase - 0.26, lightBase - 0.24);
    }

    [Fact]
    public void For_FullBrightness_ReachesPureWhiteAndBlack()
    {
        // Der Anwender kann bis 100 % Weiß bzw. Schwarz gehen.
        Assert.Equal("#FFFFFF", ThemePalette.For(dark: false, new ThemeTint(0, 0, ThemeTint.MaxBrightness)).Surface);
        Assert.Equal("#000000", ThemePalette.For(dark: true, new ThemeTint(0, 0, ThemeTint.MinBrightness)).AppBackground);
    }

    [Theory]
    [InlineData(30, 60)]
    [InlineData(210, 100)]
    [InlineData(0, 0)]
    public void TintColor_RoundTrip(int hue, int saturation)
    {
        var (h, s) = ThemePalette.TintFromColor(ThemePalette.TintColor(hue, saturation));

        Assert.InRange(s, saturation - 1, saturation + 1);
        if (saturation > 0)
        {
            Assert.InRange(h, hue - 1, hue + 1);
        }
    }

    [Fact]
    public void Tint_Normalized_ClampsValues()
    {
        Assert.Equal(new ThemeTint(350, 100, 25), new ThemeTint(-10, 150, 30).Normalized());
    }
}
