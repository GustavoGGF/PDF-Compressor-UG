using PdfCompressor.Models;
using PdfCompressor.Services;

namespace PdfCompressor.Tests.UI;

internal sealed class FakePdfAnalyzerService : IPdfAnalyzerService
{
    public PdfInfo? ConfiguredResult { get; set; }
    public TimeSpan Delay { get; set; }
    public string? LastAnalyzedPath { get; private set; }

    public async Task<PdfInfo> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        LastAnalyzedPath = filePath;
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();

        return ConfiguredResult ?? PdfInfo.Success(filePath, 2_000_000, 5);
    }
}

internal sealed class FakeCompressionEngineService : ICompressionEngine
{
    public CompressionResult? ConfiguredResult { get; set; }
    public CompressionOptions? LastOptions { get; private set; }
    public TimeSpan Delay { get; set; }
    public bool ReportSteps { get; set; }

    public async Task<CompressionResult> CompressAsync(
        CompressionOptions options,
        IProgress<CompressionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        LastOptions = options;

        if (ReportSteps && progress != null)
        {
            progress.Report(new CompressionProgressUpdate(1, 6, 300, "Testando 300 DPI", 16));
            progress.Report(new CompressionProgressUpdate(2, 6, 250, "Testando 250 DPI", 33));
        }

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return ConfiguredResult ?? new CompressionResult(
            Status: CompressionStatus.TargetMet,
            SourceFilePath: options.SourceFilePath,
            OutputFilePath: Path.Combine(options.TargetDirectory, "documento_compactado.pdf"),
            OriginalSizeBytes: 2_000_000,
            FinalSizeBytes: 800_000,
            FinalDpi: 250,
            Attempts: [],
            TotalDuration: TimeSpan.FromSeconds(1)
        );
    }
}

internal sealed class FakeFileManagerService : IFileManagerService
{
    public string GenerateSafeOutputFilePath(string sourceFilePath, string? targetDirectory = null) =>
        Path.Combine(targetDirectory ?? Path.GetDirectoryName(sourceFilePath)!, "documento_compactado.pdf");

    public string CreateIsolatedTempDirectory() => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public bool SafeDeleteDirectory(string directoryPath) => true;

    public bool SafeDeleteFile(string filePath) => true;

    public bool TryPromoteFile(string sourceTempPath, string destinationPath, out string? errorMessage)
    {
        errorMessage = null;
        return true;
    }
}

internal sealed class FakeFileLauncherService : IFileLauncherService
{
    public List<string> OpenedPdfs { get; } = [];
    public List<string> OpenedFolders { get; } = [];
    public bool ReturnValue { get; set; } = true;

    public bool OpenPdf(string filePath)
    {
        OpenedPdfs.Add(filePath);
        return ReturnValue;
    }

    public bool OpenFolderContainingFile(string filePath)
    {
        OpenedFolders.Add(filePath);
        return ReturnValue;
    }
}

internal sealed class FakeDiagnosticLogger : IDiagnosticLogger
{
    public List<string> InfoLogs { get; } = [];
    public List<string> WarningLogs { get; } = [];
    public List<string> ErrorLogs { get; } = [];

    public void LogInfo(string message) => InfoLogs.Add(message);
    public void LogWarning(string message) => WarningLogs.Add(message);
    public void LogError(string message, Exception? exception = null) => ErrorLogs.Add(message);
    public void LogCompressionSummary(CompressionResult result) { }
    public void LogAnalysisSummary(PdfInfo info) { }
    public void LogCleanup(string targetPath, bool success, string? details = null) { }
}
