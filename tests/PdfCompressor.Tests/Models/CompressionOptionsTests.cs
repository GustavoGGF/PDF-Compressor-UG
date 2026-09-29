using PdfCompressor.Models;
using Xunit;

namespace PdfCompressor.Tests.Models;

public sealed class CompressionOptionsTests
{
    [Fact]
    public void Validate_ValidManualPreset_DoesNotThrow()
    {
        var options = new CompressionOptions(
            SourceFilePath: "/tmp/source.pdf",
            TargetDirectory: "/tmp/output",
            Preset: CompressionPreset.HighQuality
        );

        var exception = Record.Exception(() => options.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_ValidAutomaticPresetWithTarget_DoesNotThrow()
    {
        var options = new CompressionOptions(
            SourceFilePath: "/tmp/source.pdf",
            TargetDirectory: "/tmp/output",
            Preset: CompressionPreset.Automatic,
            TargetSizeBytes: 5_000_000
        );

        var exception = Record.Exception(() => options.Validate());
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_AutomaticPresetWithoutTarget_ThrowsArgumentException()
    {
        var options = new CompressionOptions(
            SourceFilePath: "/tmp/source.pdf",
            TargetDirectory: "/tmp/output",
            Preset: CompressionPreset.Automatic,
            TargetSizeBytes: null
        );

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TargetSizeBytes", ex.ParamName);
    }

    [Fact]
    public void Validate_AutomaticPresetWithZeroTarget_ThrowsArgumentException()
    {
        var options = new CompressionOptions(
            SourceFilePath: "/tmp/source.pdf",
            TargetDirectory: "/tmp/output",
            Preset: CompressionPreset.Automatic,
            TargetSizeBytes: 0
        );

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TargetSizeBytes", ex.ParamName);
    }

    [Fact]
    public void Validate_EmptySourceFilePath_ThrowsArgumentException()
    {
        var options = new CompressionOptions(
            SourceFilePath: "  ",
            TargetDirectory: "/tmp/output",
            Preset: CompressionPreset.StrongCompression
        );

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("SourceFilePath", ex.ParamName);
    }

    [Fact]
    public void Validate_EmptyTargetDirectory_ThrowsArgumentException()
    {
        var options = new CompressionOptions(
            SourceFilePath: "/tmp/source.pdf",
            TargetDirectory: "",
            Preset: CompressionPreset.StrongCompression
        );

        var ex = Assert.Throws<ArgumentException>(() => options.Validate());
        Assert.Equal("TargetDirectory", ex.ParamName);
    }
}
