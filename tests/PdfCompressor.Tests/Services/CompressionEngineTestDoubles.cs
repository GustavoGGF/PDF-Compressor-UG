using PdfCompressor.Models;
using PdfCompressor.Services;

namespace PdfCompressor.Tests.Services;

internal sealed class FakeLocator : IGhostscriptLocator
{
    public bool Available { get; set; } = true;
    public string? ExecutablePath { get; set; } = "C:\\gs\\bin\\gswin64c.exe";

    public bool IsAvailable() => Available;
    public string? FindExecutablePath(string? customPath = null) => customPath ?? ExecutablePath;
    public string? GetInstalledVersion(string executablePath) => "10.02.1";
}

internal sealed class FakeAnalyzer : IPdfAnalyzerService
{
    public PdfInfo? ConfiguredInfo { get; set; }

    public Task<PdfInfo> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ConfiguredInfo ?? PdfInfo.Success(filePath, 1_000_000, 1));
    }
}

internal sealed class FakeRunner : IGhostscriptProcessRunner
{
    public sealed record Call(GhostscriptExecutionParams Params, int Dpi);

    private readonly Dictionary<int, long> _simulatedSizes = new();
    private readonly Dictionary<int, (GhostscriptFailureReason Reason, string Error)> _simulatedFailures = new();
    public List<Call> ExecutionCalls { get; } = [];

    public void SimulateDpi(int dpi, long outputSize) => _simulatedSizes[dpi] = outputSize;
    public void SimulateFailure(int dpi, GhostscriptFailureReason reason, string error) =>
        _simulatedFailures[dpi] = (reason, error);

    public Task<GhostscriptExecutionResult> RunAsync(GhostscriptExecutionParams parameters, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutionCalls.Add(new Call(parameters, parameters.Dpi));

        if (_simulatedFailures.TryGetValue(parameters.Dpi, out var failure))
        {
            return Task.FromResult(new GhostscriptExecutionResult(
                Success: false,
                ExitCode: failure.Reason == GhostscriptFailureReason.NonZeroExitCode ? 1 : -1,
                ExecutionTime: TimeSpan.FromMilliseconds(50),
                StandardOutput: string.Empty,
                StandardError: failure.Error,
                FailureReason: failure.Reason,
                ErrorMessage: failure.Error
            ));
        }

        long size = _simulatedSizes.TryGetValue(parameters.Dpi, out long s) ? s : 1_000_000;
        string? dir = Path.GetDirectoryName(parameters.OutputPdfPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var bytes = new byte[size];
        var header = "%PDF-1.7\n"u8.ToArray();
        Array.Copy(header, bytes, Math.Min(header.Length, bytes.Length));
        File.WriteAllBytes(parameters.OutputPdfPath, bytes);

        return Task.FromResult(new GhostscriptExecutionResult(
            Success: true,
            ExitCode: 0,
            ExecutionTime: TimeSpan.FromMilliseconds(50),
            StandardOutput: "Simulated GS output",
            StandardError: string.Empty,
            FailureReason: GhostscriptFailureReason.None
        ));
    }
}

internal sealed class TrackingFileManager : IFileManagerService
{
    private readonly string _baseDir;
    public List<string> CreatedDirectories { get; } = [];
    public List<string> DeletedDirectories { get; } = [];

    public TrackingFileManager(string baseDir)
    {
        _baseDir = baseDir;
        Directory.CreateDirectory(_baseDir);
    }

    public Func<string, string?, string>? CustomGenerateOutputFilePath { get; set; }

    public string GenerateSafeOutputFilePath(string sourceFilePath, string? targetDirectory = null)
    {
        if (CustomGenerateOutputFilePath != null)
        {
            return CustomGenerateOutputFilePath(sourceFilePath, targetDirectory);
        }

        string dir = targetDirectory ?? _baseDir;
        string name = Path.GetFileNameWithoutExtension(sourceFilePath);
        return Path.Combine(dir, $"{name}_compressed.pdf");
    }

    public string CreateIsolatedTempDirectory()
    {
        string dir = Path.Combine(_baseDir, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        CreatedDirectories.Add(dir);
        return dir;
    }

    public void SafeDeleteDirectory(string directoryPath)
    {
        DeletedDirectories.Add(directoryPath);
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Limpeza silenciosa
        }
    }

    public void SafeDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Limpeza silenciosa
        }
    }
}

internal sealed class MemoryLogger : IDiagnosticLogger
{
    public List<string> Entries { get; } = [];
    public List<CompressionResult> Summaries { get; } = [];

    public void LogInfo(string message) => Entries.Add($"INFO: {message}");
    public void LogWarning(string message) => Entries.Add($"WARN: {message}");
    public void LogError(string message, Exception? ex = null) => Entries.Add($"ERROR: {message} {ex?.Message}");
    public void LogCompressionSummary(CompressionResult result) => Summaries.Add(result);
    public void LogAnalysisSummary(PdfInfo info) => Entries.Add($"ANALYSIS: {info.FilePath}");
}
