using PdfUtopia.Core.Compression;

namespace PdfUtopia.Core.Tests.Compression;

/// <summary>
/// Tests der Komprimierungsstufen (A57, F49).
/// </summary>
public sealed class CompressionPresetTests
{
    [Fact]
    public void Presets_MatchDecision()
    {
        Assert.Equal(new CompressionPreset(150, 85), CompressionPreset.Quality);
        Assert.Equal(new CompressionPreset(96, 60), CompressionPreset.Economy);
    }

    [Fact]
    public void MaxPixels_A4HeightAt150Dpi()
    {
        Assert.Equal(1755, CompressionPreset.Quality.MaxPixels(842));
    }

    [Fact]
    public void TargetSize_Larger_ScaledKeepingAspectRatio()
    {
        Assert.Equal((1755, 878), CompressionPreset.Quality.TargetSize(4000, 2000, 842));
    }

    [Fact]
    public void TargetSize_Smaller_Unchanged()
    {
        Assert.Equal((800, 600), CompressionPreset.Economy.TargetSize(800, 600, 842));
    }

    [Fact]
    public void TargetSize_ExtremeAspectRatio_AtLeastOnePixel()
    {
        Assert.Equal((1123, 1), CompressionPreset.Economy.TargetSize(100_000, 10, 842));
    }

    [Theory]
    [InlineData(1000, 970, true)]
    [InlineData(1000, 980, false)]
    [InlineData(1000, 1200, false)]
    [InlineData(0, 0, false)]
    public void IsWorthwhile_BelowNinetyEightPercent(long original, long compressed, bool expected)
    {
        Assert.Equal(expected, CompressionPreset.IsWorthwhile(original, compressed));
    }

    [Theory]
    [InlineData(2_400_000, 1_100_000, 54)]
    [InlineData(2_400_000, 590_000, 75)]
    [InlineData(1000, 1200, 0)]
    public void SavingPercent_Rounded(long original, long compressed, int expected)
    {
        Assert.Equal(expected, CompressionPreset.SavingPercent(original, compressed));
    }
}
