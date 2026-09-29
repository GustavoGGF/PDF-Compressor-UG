using System.Runtime.InteropServices;
using PdfCompressor.Infrastructure;
using PdfCompressor.Models;
using PdfCompressor.Services;
using PdfCompressor.Tests.Fixtures;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class CompressionEngineIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _outputDir;

    public CompressionEngineIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PdfCompressorIntegrationTests", Guid.NewGuid().ToString("N"));
        _outputDir = Path.Combine(_testDir, "output");
        Directory.CreateDirectory(_testDir);
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, recursive: true);
            }
        }
        catch
        {
            // Limpeza de testes
        }
    }

    [Fact]
    public async Task CompressAsync_WithRealGhostscriptOnLinux_CompressesSyntheticPdfSuccessfully()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !File.Exists("/usr/bin/gs"))
        {
            // Skip em ambientes sem Ghostscript Linux instalado
            return;
        }

        string sourcePdf = Path.Combine(_testDir, "multipage_synthetic.pdf");
        File.WriteAllBytes(sourcePdf, PdfTestFixtures.CreateMultiPagePdf(3));
        long originalSize = new FileInfo(sourcePdf).Length;

        var fileManager = new FileManagerService(Path.Combine(_testDir, "attempts"));
        var logger = new DiagnosticLogger(Path.Combine(_testDir, "test.log"));
        var analyzer = new PdfAnalyzerService(logger);
        var locator = new DirectPathLocator("/usr/bin/gs");
        var starter = new DefaultGhostscriptProcessStarter();
        var runner = new GhostscriptProcessRunner(fileManager, logger, starter);

        var engine = new CompressionEngine(locator, runner, fileManager, analyzer, logger);

        var options = new CompressionOptions(
            SourceFilePath: sourcePdf,
            TargetDirectory: _outputDir,
            Preset: CompressionPreset.Automatic,
            TargetSizeBytes: 10_000_000 // Alvo generoso para teste funcional end-to-end
        );

        var progressUpdates = new List<CompressionProgressUpdate>();
        var progress = new Progress<CompressionProgressUpdate>(progressUpdates.Add);

        var result = await engine.CompressAsync(options, progress);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.NotNull(result.OutputFilePath);
        Assert.True(File.Exists(result.OutputFilePath));
        Assert.True(result.FinalSizeBytes > 0);
        Assert.NotNull(result.FinalDpi);
        Assert.NotEmpty(result.Attempts);
        Assert.True(result.Attempts[0].Succeeded);

        // Verifica que o original está intacto
        Assert.True(File.Exists(sourcePdf));
        Assert.Equal(originalSize, new FileInfo(sourcePdf).Length);

        // Verifica que o arquivo gerado tem cabeçalho %PDF-
        byte[] outputBytes = File.ReadAllBytes(result.OutputFilePath);
        Assert.Equal("%PDF-"u8.ToArray(), outputBytes[..5]);

        // Verifica progresso monotônico
        Assert.NotEmpty(progressUpdates);
        Assert.Equal(100, progressUpdates[^1].PercentageEstimate);
    }

    [Fact]
    public async Task CompressAsync_WithRealGhostscriptOnLinux_ManualPresetHighQuality_GeneratesValidPdf()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || !File.Exists("/usr/bin/gs"))
        {
            return;
        }

        string sourcePdf = Path.Combine(_testDir, "single_page.pdf");
        File.WriteAllBytes(sourcePdf, PdfTestFixtures.CreateOnePagePdf());

        var fileManager = new FileManagerService(Path.Combine(_testDir, "attempts"));
        var logger = new DiagnosticLogger(Path.Combine(_testDir, "test.log"));
        var analyzer = new PdfAnalyzerService(logger);
        var locator = new DirectPathLocator("/usr/bin/gs");
        var runner = new GhostscriptProcessRunner(fileManager, logger);

        var engine = new CompressionEngine(locator, runner, fileManager, analyzer, logger);

        var options = new CompressionOptions(
            SourceFilePath: sourcePdf,
            TargetDirectory: _outputDir,
            Preset: CompressionPreset.HighQuality
        );

        var result = await engine.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.Equal(300, result.FinalDpi);
        Assert.NotNull(result.OutputFilePath);
        Assert.True(File.Exists(result.OutputFilePath));
        Assert.Single(result.Attempts);
        Assert.True(result.Attempts[0].Succeeded);
    }

    private sealed class DirectPathLocator : IGhostscriptLocator
    {
        private readonly string _executablePath;

        public DirectPathLocator(string executablePath)
        {
            _executablePath = executablePath;
        }

        public bool IsAvailable() => File.Exists(_executablePath);
        public string? FindExecutablePath(string? customPath = null) => customPath ?? _executablePath;
        public string? GetInstalledVersion(string executablePath) => "10.02.1";
    }
}
