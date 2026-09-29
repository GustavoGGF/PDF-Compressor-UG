using System.Globalization;
using PdfCompressor.Models;
using PdfCompressor.Services;

namespace PdfCompressor.Infrastructure;

/// <summary>
/// Registrador de diagnósticos seguro em arquivo de texto conforme DEC-10.
/// </summary>
public sealed class DiagnosticLogger : IDiagnosticLogger
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public DiagnosticLogger(string? customLogFilePath = null)
    {
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

                File.AppendAllText(_logFilePath, entry);
            }
            catch
            {
                // Diagnóstico não deve derrubar a aplicação caso o disco ou arquivo de log esteja bloqueado
            }
        }
    }

    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return string.Empty;
        }

        // Remove quebras de linha adicionais no corpo da mensagem para manter uma entrada por linha
        return message.Replace("\r", " ").Replace("\n", " ");
    }
}
