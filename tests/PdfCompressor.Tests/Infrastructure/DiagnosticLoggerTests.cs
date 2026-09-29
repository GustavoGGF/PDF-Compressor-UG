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
            OutputFilePath: "/secret/path/to/sensivel_compactado.pdf",
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
        // Garante que apenas o nome base é registrado
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

    [Fact]
    public void LogAnalysisSummary_LogsMetricsWithoutDocumentContentsOrSensitivePaths()
    {
        var info = new PdfInfo(
            FilePath: "/secret/folder/confidencial.pdf",
            FileSizeBytes: 1_234_567,
            PageCount: 3,
            HasLikelySignature: true,
            IsEncrypted: false,
            IsValid: true,
            WarningMessage: PdfInfo.DefaultSignatureWarningMessage
        );

        _logger.LogAnalysisSummary(info);

        string content = File.ReadAllText(_testLogFilePath);
        Assert.Contains("File=confidencial.pdf", content);
        Assert.DoesNotContain("/secret/folder/", content);
        Assert.Contains("Status=Warning", content);
        Assert.Contains("Pages=3", content);
        Assert.Contains("HasSignature=True", content);
        Assert.Contains("Encrypted=False", content);
        Assert.Contains(PdfInfo.DefaultSignatureWarningMessage, content);
    }

    [Fact]
    public void LogCleanup_LogsTargetAndStatusSanitized()
    {
        _logger.LogCleanup("/secret/dir/attempt_123", succeeded: true);
        _logger.LogCleanup("/secret/dir/locked_file.tmp", succeeded: false, details: "Sharing violation");

        string content = File.ReadAllText(_testLogFilePath);
        Assert.Contains("[INFO] Cleanup: Target=attempt_123, Succeeded=True, Details=OK", content);
        Assert.Contains("[WARN] Cleanup: Target=locked_file.tmp, Succeeded=False, Details=Sharing violation", content);
        Assert.DoesNotContain("/secret/dir/", content);
    }

    [Fact]
    public void SanitizeMessage_RedactsPdfStreamsAndEliminatesLineBreaks()
    {
        string rawMessage = "Diagnostic stream:\r\n%PDF-1.7\r\n4 0 obj\r\nendobj";
        _logger.LogInfo(rawMessage);

        string content = File.ReadAllText(_testLogFilePath);
        Assert.DoesNotContain("%PDF-", content);
        Assert.Contains("[PDF_STREAM_REDACTED]", content);

        // Cada linha do arquivo de log deve ter exatamente uma entrada completa
        string[] lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }

    [Fact]
    public void LogRotation_WhenFileSizeExceedsLimit_RotatesLogFiles()
    {
        string rotatingLogPath = Path.Combine(_testLogDirectory, "rotating.log");
        // Limite muito baixo (100 bytes) e até 2 arquivos de histórico
        var rotatingLogger = new DiagnosticLogger(rotatingLogPath, maxFileSizeBytes: 100, maxArchivedFiles: 2);

        for (int i = 0; i < 10; i++)
        {
            rotatingLogger.LogInfo($"Mensagem de teste {i} com tamanho considerável para forçar rotação.");
        }

        Assert.True(File.Exists(rotatingLogPath));
        Assert.True(File.Exists($"{rotatingLogPath}.1"));
    }
}
