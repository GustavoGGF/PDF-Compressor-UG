using PdfCompressor.Models;
using PdfCompressor.Services;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class GhostscriptArgumentBuilderTests
{
    [Fact]
    public void BuildArguments_ValidParams_ReturnsStructuredArgumentsWithoutShellInterpolation()
    {
        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "/usr/bin/gs",
            InputPdfPath: "/tmp/input.pdf",
            OutputPdfPath: "/tmp/output.pdf",
            Dpi: 150,
            CompatibilityLevel: 17
        );

        var args = GhostscriptArgumentBuilder.BuildArguments(parameters);

        Assert.Contains("-dNOPAUSE", args);
        Assert.Contains("-dBATCH", args);
        Assert.Contains("-dQUIET", args);
        Assert.Contains("-sDEVICE=pdfwrite", args);
        Assert.Contains("-dCompatibilityLevel=1.7", args);
        Assert.Contains("-dDownsampleColorImages=true", args);
        Assert.Contains("-dColorImageResolution=150", args);
        Assert.Contains("-dDownsampleGrayImages=true", args);
        Assert.Contains("-dGrayImageResolution=150", args);
        Assert.Contains("-dDownsampleMonoImages=true", args);
        Assert.Contains("-dMonoImageResolution=150", args);
        Assert.Contains("-dColorImageDownsampleType=/Bicubic", args);
        Assert.Contains("-dGrayImageDownsampleType=/Bicubic", args);
        Assert.Contains("-dMonoImageDownsampleType=/Bicubic", args);
        Assert.Contains("-dAutoRotatePages=/None", args);
        Assert.Contains("-sOutputFile=/tmp/output.pdf", args);
        Assert.Equal("/tmp/input.pdf", args[^1]);
    }

    [Theory]
    [InlineData(72, "72")]
    [InlineData(100, "100")]
    [InlineData(150, "150")]
    [InlineData(200, "200")]
    [InlineData(250, "250")]
    [InlineData(300, "300")]
    public void BuildArguments_DifferentDpi_SetsColorGrayMonoResolutionsCorrectly(int dpi, string expectedDpiStr)
    {
        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "gswin64c.exe",
            InputPdfPath: "in.pdf",
            OutputPdfPath: "out.pdf",
            Dpi: dpi
        );

        var args = GhostscriptArgumentBuilder.BuildArguments(parameters);

        Assert.Contains($"-dColorImageResolution={expectedDpiStr}", args);
        Assert.Contains($"-dGrayImageResolution={expectedDpiStr}", args);
        Assert.Contains($"-dMonoImageResolution={expectedDpiStr}", args);
    }

    [Theory]
    [InlineData(14, "1.4")]
    [InlineData(15, "1.5")]
    [InlineData(16, "1.6")]
    [InlineData(17, "1.7")]
    public void BuildArguments_CompatibilityLevel_FormatsCorrectly(int level, string expectedStr)
    {
        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "gswin64c.exe",
            InputPdfPath: "in.pdf",
            OutputPdfPath: "out.pdf",
            Dpi: 150,
            CompatibilityLevel: level
        );

        var args = GhostscriptArgumentBuilder.BuildArguments(parameters);

        Assert.Contains($"-dCompatibilityLevel={expectedStr}", args);
    }

    [Fact]
    public void BuildArguments_PathsWithSpaces_PreservesVerbatimInArguments()
    {
        var inPath = @"C:\Program Files\Test Data\Input File With Spaces.pdf";
        var outPath = @"C:\Program Files\Test Data\Output File With Spaces.pdf";

        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: @"C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe",
            InputPdfPath: inPath,
            OutputPdfPath: outPath,
            Dpi: 200
        );

        var args = GhostscriptArgumentBuilder.BuildArguments(parameters);

        Assert.Contains($"-sOutputFile={outPath}", args);
        Assert.Equal(inPath, args[^1]);
    }

    [Fact]
    public void BuildArguments_PathsWithUnicodeAndAccents_PreservesVerbatim()
    {
        var inPath = @"C:\Usuário Teste\Ação Judicial 2026\peça de petição.pdf";
        var outPath = @"C:\Usuário Teste\Ação Judicial 2026\peça_compactada.pdf";

        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "gswin64c.exe",
            InputPdfPath: inPath,
            OutputPdfPath: outPath,
            Dpi: 150
        );

        var args = GhostscriptArgumentBuilder.BuildArguments(parameters);

        Assert.Contains($"-sOutputFile={outPath}", args);
        Assert.Equal(inPath, args[^1]);
    }

    [Fact]
    public void BuildArguments_PathsWithSpecialCharacters_PreservesVerbatim()
    {
        var inPath = @"/data/docs/#project-[2026]/in.pdf";
        var outPath = @"/data/docs/#project-[2026]/out.pdf";

        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "/bin/gs",
            InputPdfPath: inPath,
            OutputPdfPath: outPath,
            Dpi: 150
        );

        var args = GhostscriptArgumentBuilder.BuildArguments(parameters);

        Assert.Contains($"-sOutputFile={outPath}", args);
        Assert.Equal(inPath, args[^1]);
    }

    [Fact]
    public void BuildArguments_InputEqualsOutput_ThrowsInvalidOperationException()
    {
        var path = "/tmp/sample.pdf";
        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "gs",
            InputPdfPath: path,
            OutputPdfPath: path,
            Dpi: 150
        );

        var ex = Assert.Throws<InvalidOperationException>(() => GhostscriptArgumentBuilder.BuildArguments(parameters));
        Assert.Contains("mesmo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void BuildArguments_InvalidDpi_ThrowsArgumentOutOfRangeException(int invalidDpi)
    {
        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "gs",
            InputPdfPath: "in.pdf",
            OutputPdfPath: "out.pdf",
            Dpi: invalidDpi
        );

        Assert.Throws<ArgumentOutOfRangeException>(() => GhostscriptArgumentBuilder.BuildArguments(parameters));
    }

    [Theory]
    [InlineData("", "out.pdf")]
    [InlineData("   ", "out.pdf")]
    [InlineData("in.pdf", "")]
    [InlineData("in.pdf", "   ")]
    public void BuildArguments_WhitespacePaths_ThrowsArgumentException(string inPath, string outPath)
    {
        var parameters = new GhostscriptExecutionParams(
            ExecutablePath: "gs",
            InputPdfPath: inPath,
            OutputPdfPath: outPath,
            Dpi: 150
        );

        Assert.Throws<ArgumentException>(() => GhostscriptArgumentBuilder.BuildArguments(parameters));
    }
}
