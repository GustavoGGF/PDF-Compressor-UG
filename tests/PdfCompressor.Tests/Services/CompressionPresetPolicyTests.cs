using PdfCompressor.Models;
using PdfCompressor.Services;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class CompressionPresetPolicyTests
{
    [Fact]
    public void GetDpiSequence_Automatic_ReturnsDescendingSixLevels()
    {
        int[] sequence = CompressionPresetPolicy.GetDpiSequence(CompressionPreset.Automatic);

        Assert.Equal([300, 250, 200, 150, 100, 72], sequence);
    }

    [Theory]
    [InlineData(CompressionPreset.HighQuality, 300)]
    [InlineData(CompressionPreset.MediumQuality, 150)]
    [InlineData(CompressionPreset.StrongCompression, 72)]
    public void GetDpiSequence_ManualPresets_ReturnSingleElementArray(CompressionPreset preset, int expectedDpi)
    {
        int[] sequence = CompressionPresetPolicy.GetDpiSequence(preset);

        Assert.Single(sequence);
        Assert.Equal(expectedDpi, sequence[0]);
    }

    [Fact]
    public void GetDpiSequence_InvalidPreset_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CompressionPresetPolicy.GetDpiSequence((CompressionPreset)999)
        );
    }

    [Theory]
    [InlineData(500_000L, 1_000_000L, true)]
    [InlineData(1_000_000L, 1_000_000L, true)]
    [InlineData(1_000_001L, 1_000_000L, false)]
    [InlineData(5_000_000L, null, true)]
    public void IsEligible_EvaluatesCorrectly(long candidateSize, long? targetSize, bool expected)
    {
        bool result = CompressionPresetPolicy.IsEligible(candidateSize, targetSize);
        Assert.Equal(expected, result);
    }
}
