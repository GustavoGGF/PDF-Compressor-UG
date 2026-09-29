using PdfCompressor.Infrastructure;
using PdfCompressor.Models;
using Xunit;

namespace PdfCompressor.Tests.Infrastructure;

public sealed class DiagnosticLoggerTests : IDisposable
{
    private readonly string _testLogDirectory;
    private readonly string _testLogFilePath;
    private readonly DiagnosticLogger _logger;

    public DiagnosticLoggerTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), "PdfCompressor_LogTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testLogDirectory);
        _testLogFilePath = Path.Combine(_testLogDirectory, "test_log.log");
        _logger = new DiagnosticLogger(_testLogFilePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testLogDirectory))
            {
                Directory.Delete(_testLogDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void LogInfo_WritesInfoEntryWithUtcTimestamp()
    {
        _logger.LogInfo("Operação de teste iniciada");

        Assert.True(File.Exists(_testLogFilePath));
        string content = File.ReadAllText(_testLogFilePath);
        Assert.Contains("[INFO] Operação de teste iniciada", content);
        Assert.Contains("UTC", content);
    }

    [Fact]
    public void LogError_WithException_AppendsExceptionTypeAndMessage()
    {
        var exception = new InvalidOperationException("Falha simulada");
        _logger.LogError("Erro durante processamento", exception);

        string content = File.ReadAllText(_testLogFilePath);
        Assert.Contains("[ERROR] Erro durante processamento | Exception: InvalidOperationException: Falha simulada", content);
    }

    [Fact]
    public void LogCompressionSummary_LogsMetricsWithoutDocumentContents()
    {
        var result = new CompressionResult(
            Status: CompressionStatus.TargetMet,
            SourceFilePath: "/secret/path/to/sensivel.pdf",
            OutputFilePath: "/secret/path/to/sensivel_compressed.pdf",
            OriginalSizeBytes: 5_000_000,
            FinalSizeBytes: 2_000_000,
            FinalDpi: 200,
            Attempts: [
                new AttemptResult(1, 300, 4_000_000, TimeSpan.FromMilliseconds(500), true),
                new AttemptResult(2, 200, 2_000_000, TimeSpan.FromMilliseconds(450), true)
            ],
            TotalDuration: TimeSpan.FromMilliseconds(950)
        );

        _logger.LogCompressionSummary(result);

        string content = File.ReadAllText(_testLogFilePath);
        // Garante que o nome base é registrado
        Assert.Contains("File=sensivel.pdf", content);
        // Garante que o caminho sensível não é vazado no resumo
        Assert.DoesNotContain("/secret/path/to/", content);
        // Garante que métricas e status estão no formato padronizado
        Assert.Contains("Status=TargetMet", content);
        Assert.Contains("OriginalBytes=5000000 (5.00 MB)", content);
        Assert.Contains("FinalBytes=2000000 (2.00 MB)", content);
        Assert.Contains("Reduction=60.00%", content);
        Assert.Contains("FinalDpi=200", content);
        Assert.Contains("Attempts=2", content);
    }
}
