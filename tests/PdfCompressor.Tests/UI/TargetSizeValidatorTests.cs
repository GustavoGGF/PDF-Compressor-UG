using System.Globalization;
using PdfCompressor.Models;
using PdfCompressor.UI;
using Xunit;

namespace PdfCompressor.Tests.UI;

public sealed class TargetSizeValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryValidate_AutomaticPreset_EmptyOrWhitespace_FailsWithTargetRequiredError(string? input)
    {
        bool isValid = TargetSizeValidator.TryValidate(CompressionPreset.Automatic, input, TargetSizeUnit.MB, out long? bytes, out string? error);

        Assert.False(isValid);
        Assert.Null(bytes);
        Assert.Equal(TargetSizeValidator.TargetRequiredMessage, error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12x")]
    [InlineData("++1")]
    [InlineData("1.2.3")]
    [InlineData("1,2,3")]
    public void TryValidate_AutomaticPreset_NonNumeric_FailsWithInvalidNumberError(string input)
    {
        bool isValid = TargetSizeValidator.TryValidate(CompressionPreset.Automatic, input, TargetSizeUnit.MB, out long? bytes, out string? error);

        Assert.False(isValid);
        Assert.Null(bytes);
        Assert.Equal(TargetSizeValidator.InvalidNumberMessage, error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("0,0")]
    [InlineData("-1")]
    [InlineData("-0.5")]
    [InlineData("-5,2")]
    public void TryValidate_AutomaticPreset_ZeroOrNegative_FailsWithMustBeGreaterThanZeroError(string input)
    {
        bool isValid = TargetSizeValidator.TryValidate(CompressionPreset.Automatic, input, TargetSizeUnit.MB, out long? bytes, out string? error);

        Assert.False(isValid);
        Assert.Null(bytes);
        Assert.Equal(TargetSizeValidator.MustBeGreaterThanZeroMessage, error);
    }

    [Theory]
    [InlineData("2,5", 2_500_000L)]
    [InlineData("2.5", 2_500_000L)]
    [InlineData("10", 10_000_000L)]
    [InlineData("0,75", 750_000L)]
    public void TryValidate_AutomaticPreset_ValidDecimalInMb_CalculatesBytesCorrectly(string input, long expectedBytes)
    {
        bool isValid = TargetSizeValidator.TryValidate(CompressionPreset.Automatic, input, TargetSizeUnit.MB, out long? bytes, out string? error);

        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal(expectedBytes, bytes);
    }

    [Theory]
    [InlineData("500", 500_000L)]
    [InlineData("500,5", 500_500L)]
    [InlineData("500.5", 500_500L)]
    [InlineData("100", 100_000L)]
    public void TryValidate_AutomaticPreset_ValidDecimalInKb_CalculatesBytesCorrectly(string input, long expectedBytes)
    {
        bool isValid = TargetSizeValidator.TryValidate(CompressionPreset.Automatic, input, TargetSizeUnit.KB, out long? bytes, out string? error);

        Assert.True(isValid);
        Assert.Null(error);
        Assert.Equal(expectedBytes, bytes);
    }

    [Theory]
    [InlineData(CompressionPreset.HighQuality)]
    [InlineData(CompressionPreset.MediumQuality)]
    [InlineData(CompressionPreset.StrongCompression)]
    public void TryValidate_ManualPresets_DoesNotRequireTargetSize(CompressionPreset preset)
    {
        bool isValid = TargetSizeValidator.TryValidate(preset, null, TargetSizeUnit.MB, out long? bytes, out string? error);

        Assert.True(isValid);
        Assert.Null(bytes);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_AcrossDifferentThreadCultures_BehavesIdentically()
    {
        var testCultures = new[] { "pt-BR", "en-US", "de-DE", "fr-FR", "" };
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            foreach (var cultureName in testCultures)
            {
                CultureInfo.CurrentCulture = string.IsNullOrEmpty(cultureName)
                    ? CultureInfo.InvariantCulture
                    : CultureInfo.GetCultureInfo(cultureName);

                // Testa vírgula
                bool commaValid = TargetSizeValidator.TryValidate(CompressionPreset.Automatic, "3,5", TargetSizeUnit.MB, out long? commaBytes, out string? commaErr);
                Assert.True(commaValid);
                Assert.Null(commaErr);
                Assert.Equal(3_500_000L, commaBytes);

                // Testa ponto
                bool dotValid = TargetSizeValidator.TryValidate(CompressionPreset.Automatic, "3.5", TargetSizeUnit.MB, out long? dotBytes, out string? dotErr);
                Assert.True(dotValid);
                Assert.Null(dotErr);
                Assert.Equal(3_500_000L, dotBytes);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TargetSizeUnitExtensions_FormatBytes_FormatsSizesInMbKbAndBytes()
    {
        var ptBr = CultureInfo.GetCultureInfo("pt-BR");

        Assert.Equal("1,50 MB", TargetSizeUnitExtensions.FormatBytes(1_500_000L, ptBr));
        Assert.Equal("500,00 KB", TargetSizeUnitExtensions.FormatBytes(500_000L, ptBr));
        Assert.Equal("950 B", TargetSizeUnitExtensions.FormatBytes(950L, ptBr));
    }
}
