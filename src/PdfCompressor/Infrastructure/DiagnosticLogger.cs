using System.Globalization;
using PdfCompressor.Models;
using PdfCompressor.Services;

namespace PdfCompressor.Infrastructure;

/// <summary>
/// Registrador de diagnósticos seguro em arquivo de texto conforme DEC-10.
/// Garante linhas estáveis, ausência de conteúdo de documentos ou senhas,
/// e política de retenção com rotação de arquivos de log.
/// </summary>
public sealed class DiagnosticLogger : IDiagnosticLogger
{
    public const long DefaultMaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    public const int DefaultMaxArchivedFiles = 3;

    private readonly string _logFilePath;
    private readonly long _maxFileSizeBytes;
    private readonly int _maxArchivedFiles;
    private readonly object _lock = new();

    public DiagnosticLogger(
        string? customLogFilePath = null,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        int maxArchivedFiles = DefaultMaxArchivedFiles)
    {
        _maxFileSizeBytes = maxFileSizeBytes > 0 ? maxFileSizeBytes : DefaultMaxFileSizeBytes;
        _maxArchivedFiles = maxArchivedFiles >= 0 ? maxArchivedFiles : DefaultMaxArchivedFiles;

        if (!string.IsNullOrWhiteSpace(customLogFilePath))
        {
            _logFilePath = customLogFilePath;
        }
        else
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Path.GetTempPath();
            }
            string logDir = Path.Combine(localAppData, "PdfCompressor", "logs");
            _logFilePath = Path.Combine(logDir, "pdfcompressor.log");
        }
    }

    public string LogFilePath => _logFilePath;

    /// <inheritdoc />
    public void LogInfo(string message) => WriteEntry("INFO", message);

    /// <inheritdoc />
    public void LogWarning(string message) => WriteEntry("WARN", message);

    /// <inheritdoc />
    public void LogError(string message, Exception? ex = null)
    {
        string detail = ex is null ? message : $"{message} | Exception: {ex.GetType().Name}: {ex.Message}";
        WriteEntry("ERROR", detail);
    }

    /// <inheritdoc />
    public void LogCompressionSummary(CompressionResult result)
    {
        string safeFileName = Path.GetFileName(result.SourceFilePath);
        string message = string.Format(
            CultureInfo.InvariantCulture,
            "CompressionSummary: File={0}, Status={1}, OriginalBytes={2} ({3:F2} MB), FinalBytes={4} ({5:F2} MB), Reduction={6:F2}%, FinalDpi={7}, Attempts={8}, DurationMs={9:F0}",
            safeFileName,
            result.Status,
            result.OriginalSizeBytes,
            result.OriginalSizeMb,
            result.FinalSizeBytes,
            result.FinalSizeMb,
            result.ReductionPercentage,
            result.FinalDpi?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
            result.Attempts.Count,
            result.TotalDuration.TotalMilliseconds
        );
        WriteEntry("INFO", message);
    }

    /// <inheritdoc />
    public void LogAnalysisSummary(PdfInfo info)
    {
        string safeFileName = string.IsNullOrWhiteSpace(info.FilePath) ? "unknown" : Path.GetFileName(info.FilePath);
        string message = string.Format(
            CultureInfo.InvariantCulture,
            "AnalysisSummary: File={0}, Valid={1}, Status={2}, SizeBytes={3} ({4:F2} MB), Pages={5}, HasSignature={6}, Encrypted={7}, Error={8}, Warning={9}",
            safeFileName,
            info.IsValid,
            info.Status,
            info.FileSizeBytes,
            info.FileSizeMb,
            info.PageCount?.ToString(CultureInfo.InvariantCulture) ?? "Unknown",
            info.HasLikelySignature,
            info.IsEncrypted,
            info.ErrorMessage ?? "None",
            info.WarningMessage ?? "None"
        );
        WriteEntry("INFO", message);
    }

    /// <inheritdoc />
    public void LogCleanup(string targetPath, bool succeeded, string? details = null)
    {
        string safeTarget = string.IsNullOrWhiteSpace(targetPath) ? "unknown" : Path.GetFileName(targetPath);
        string message = string.Format(
            CultureInfo.InvariantCulture,
            "Cleanup: Target={0}, Succeeded={1}, Details={2}",
            safeTarget,
            succeeded,
            details ?? "OK"
        );
        WriteEntry(succeeded ? "INFO" : "WARN", message);
    }

    private void WriteEntry(string level, string message)
    {
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        string sanitizedMessage = SanitizeMessage(message);
        string entry = $"[{timestamp} UTC] [{level}] {sanitizedMessage}{Environment.NewLine}";

        lock (_lock)
        {
            try
            {
                string? directory = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                RotateLogsIfNeeded();
                File.AppendAllText(_logFilePath, entry);
            }
            catch
            {
                // Diagnóstico não deve interromper a operação da aplicação se o disco estiver indisponível
            }
        }
    }

    private void RotateLogsIfNeeded()
    {
        if (!File.Exists(_logFilePath))
        {
            return;
        }

        try
        {
            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Length < _maxFileSizeBytes)
            {
                return;
            }

            for (int i = _maxArchivedFiles - 1; i >= 1; i--)
            {
                string sourceArchive = $"{_logFilePath}.{i}";
                string targetArchive = $"{_logFilePath}.{i + 1}";

                if (File.Exists(sourceArchive))
                {
                    if (File.Exists(targetArchive))
                    {
                        File.Delete(targetArchive);
                    }
                    File.Move(sourceArchive, targetArchive);
                }
            }

            string firstArchive = $"{_logFilePath}.1";
            if (File.Exists(firstArchive))
            {
                File.Delete(firstArchive);
            }

            File.Move(_logFilePath, firstArchive);
        }
        catch
        {
            // Falha na rotação não deve abortar o log principal
        }
    }

    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        // Remove quebras de linha para manter strictly uma linha por entrada
        string sanitized = message.Replace("\r", " ").Replace("\n", " ");

        // Proteção contra inclusão acidental de cabeçalhos brutos ou bytes PDF (%PDF-)
        if (sanitized.Contains("%PDF-", StringComparison.OrdinalIgnoreCase))
        {
            sanitized = sanitized.Replace("%PDF-", "[PDF_STREAM_REDACTED]", StringComparison.OrdinalIgnoreCase);
        }

        return sanitized;
    }
}
