using PdfCompressor.Models;
using Xunit;

namespace PdfCompressor.Tests.Models;

public sealed class CompressionResultTests
{
    [Fact]
    public void ReductionPercentage_CalculatesCorrectlyOnReduction()
    {
        var result = new CompressionResult(
            Status: CompressionStatus.TargetMet,
            SourceFilePath: "test.pdf",
            OutputFilePath: "test_compactado.pdf",
            OriginalSizeBytes: 10_000_000,
            FinalSizeBytes: 4_000_000,
            FinalDpi: 150,
            Attempts: [],
            TotalDuration: TimeSpan.FromSeconds(2)
        );

        Assert.Equal(10.0, result.OriginalSizeMb);
        Assert.Equal(4.0, result.FinalSizeMb);
        Assert.Equal(60.0, result.ReductionPercentage);
    }

    [Fact]
    public void ReductionPercentage_WhenFinalIsLargerThanOriginal_ReturnsZero()
    {
        var result = new CompressionResult(
            Status: CompressionStatus.BestEffortAboveTarget,
            SourceFilePath: "test.pdf",
            OutputFilePath: "test_compactado.pdf",
            OriginalSizeBytes: 1_000_000,
            FinalSizeBytes: 1_200_000,
            FinalDpi: 72,
            Attempts: [],
            TotalDuration: TimeSpan.FromSeconds(1)
        );

        Assert.Equal(0.0, result.ReductionPercentage);
    }

    [Fact]
    public void ReductionPercentage_WhenOriginalIsZero_ReturnsZero()
    {
        var result = new CompressionResult(
            Status: CompressionStatus.InvalidInput,
            SourceFilePath: "zero.pdf",
            OutputFilePath: null,
            OriginalSizeBytes: 0,
            FinalSizeBytes: 0,
            FinalDpi: null,
            Attempts: [],
            TotalDuration: TimeSpan.Zero
        );

        Assert.Equal(0.0, result.ReductionPercentage);
    }

    [Fact]
    public void AttemptResult_OutputSizeMb_CalculatesUsingDecimalMb()
    {
        var attempt = new AttemptResult(
            AttemptIndex: 1,
            Dpi: 300,
            OutputSizeBytes: 2_500_000,
            Duration: TimeSpan.FromSeconds(1.2),
            Succeeded: true
        );

        Assert.Equal(2.5, attempt.OutputSizeMb);
        Assert.True(attempt.Succeeded);
        Assert.Null(attempt.ErrorDetails);
    }
}
