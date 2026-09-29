using System.ComponentModel;
using System.Text;
using PdfCompressor.Infrastructure;
using PdfCompressor.Models;
using PdfCompressor.Services;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class GhostscriptProcessRunnerTests : IDisposable
{
    private readonly string _testWorkingDir;
    private readonly IFileManagerService _fileManager;
    private readonly TestDiagnosticLogger _logger;

    public GhostscriptProcessRunnerTests()
    {
        _testWorkingDir = Path.Combine(Path.GetTempPath(), "PdfCompressor_RunnerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWorkingDir);
        _fileManager = new FileManagerService();
        _logger = new TestDiagnosticLogger();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWorkingDir))
            {
                Directory.Delete(_testWorkingDir, recursive: true);
            }
        }
        catch { }
    }

    private string CreateFakeValidPdf(string fileName, string content = "Sample valid PDF content")
    {
        var filePath = Path.Combine(_testWorkingDir, fileName);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var pdfBytes = Encoding.ASCII.GetBytes($"%PDF-1.7\n1 0 obj\n<< /Type /Catalog >>\nendobj\n%%EOF\n{content}");
        File.WriteAllBytes(filePath, pdfBytes);
        return filePath;
    }

    [Fact]
    public async Task RunAsync_SuccessfulExecution_CreatesOutputAndCleansTempDir()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) =>
        {
            // Extract -sOutputFile=...
            var outArg = args.First(a => a.StartsWith("-sOutputFile=", StringComparison.Ordinal));
            var tempOutFile = outArg["-sOutputFile=".Length..];
            File.WriteAllBytes(tempOutFile, Encoding.ASCII.GetBytes("%PDF-1.7\nCompressed content\n%%EOF"));
            return Task.FromResult(new ProcessRunOutput(0, "GPL Ghostscript 10.02.1\nPage 1\n", "", TimeSpan.FromMilliseconds(50)));
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(GhostscriptFailureReason.None, result.FailureReason);
        Assert.True(File.Exists(outputPath));
        Assert.StartsWith("%PDF-1.7", Encoding.ASCII.GetString(File.ReadAllBytes(outputPath)));
    }

    [Fact]
    public async Task RunAsync_NonZeroExitCode_ReturnsNonZeroStatus_DoesNotCreateOutput_CleansTempDir()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) =>
        {
            return Task.FromResult(new ProcessRunOutput(1, "", "Error: /undefined in .setdistillerkeys", TimeSpan.FromMilliseconds(40)));
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output_non_zero.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(GhostscriptFailureReason.NonZeroExitCode, result.FailureReason);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_ProcessStartFailure_ReturnsStartFailure_CleansTempDir()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) =>
        {
            throw new Win32Exception(2, "O sistema não pode encontrar o arquivo especificado.");
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output_fail_start.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.StartFailure, result.FailureReason);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_OutputMissingAfterZeroExit_ReturnsOutputMissing()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) =>
        {
            // Exits with 0 but produces no file
            return Task.FromResult(new ProcessRunOutput(0, "Finished without output", "", TimeSpan.FromMilliseconds(50)));
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output_missing.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.OutputMissing, result.FailureReason);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_ZeroBytesOutput_ReturnsInvalidOutput()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) =>
        {
            var outArg = args.First(a => a.StartsWith("-sOutputFile=", StringComparison.Ordinal));
            var tempOutFile = outArg["-sOutputFile=".Length..];
            File.WriteAllBytes(tempOutFile, Array.Empty<byte>());
            return Task.FromResult(new ProcessRunOutput(0, "", "", TimeSpan.FromMilliseconds(50)));
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output_zero.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.InvalidOutput, result.FailureReason);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_InvalidPdfHeader_ReturnsInvalidOutput()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) =>
        {
            var outArg = args.First(a => a.StartsWith("-sOutputFile=", StringComparison.Ordinal));
            var tempOutFile = outArg["-sOutputFile=".Length..];
            File.WriteAllBytes(tempOutFile, Encoding.ASCII.GetBytes("NOT A PDF HEADER"));
            return Task.FromResult(new ProcessRunOutput(0, "", "", TimeSpan.FromMilliseconds(50)));
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output_corrupt.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.InvalidOutput, result.FailureReason);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_InputPathEqualsOutputPath_ReturnsInvalidInput()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) => Task.FromResult(new ProcessRunOutput(0, "", "", TimeSpan.Zero)));
        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("same_path.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, inputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.InvalidInput, result.FailureReason);
    }

    [Fact]
    public async Task RunAsync_InputPathDoesNotExist_ReturnsInvalidInput()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) => Task.FromResult(new ProcessRunOutput(0, "", "", TimeSpan.Zero)));
        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var nonExistentInput = Path.Combine(_testWorkingDir, "does_not_exist.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, nonExistentInput, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.InvalidInput, result.FailureReason);
    }

    [Fact]
    public async Task RunAsync_ExecutableDoesNotExist_ReturnsExecutableNotFound()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) => Task.FromResult(new ProcessRunOutput(0, "", "", TimeSpan.Zero)));
        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output.pdf");
        var nonExistentExe = Path.Combine(_testWorkingDir, "no_gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(nonExistentExe, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.ExecutableNotFound, result.FailureReason);
    }

    [Fact]
    public async Task RunAsync_CancelledViaToken_ReturnsCancelled_CleansTempDir()
    {
        using var cts = new CancellationTokenSource();

        var fakeStarter = new FakeProcessStarter(async (exe, args) =>
        {
            // Simulate long execution interrupted by cancellation
            cts.Cancel();
            await Task.Delay(500, cts.Token);
            return new ProcessRunOutput(0, "", "", TimeSpan.FromMilliseconds(500));
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "output_cancelled.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, cts.Token);

        Assert.False(result.Success);
        Assert.Equal(GhostscriptFailureReason.Cancelled, result.FailureReason);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RunAsync_LogsDiagnosticInformationWithoutPdfContent()
    {
        var fakeStarter = new FakeProcessStarter((exe, args) =>
        {
            var outArg = args.First(a => a.StartsWith("-sOutputFile=", StringComparison.Ordinal));
            var tempOutFile = outArg["-sOutputFile=".Length..];
            File.WriteAllBytes(tempOutFile, Encoding.ASCII.GetBytes("%PDF-1.7\nSECRET_INTERNAL_CONTENT\n%%EOF"));
            return Task.FromResult(new ProcessRunOutput(0, "GPL Ghostscript 10.02.1\nPage 1\n", "", TimeSpan.FromMilliseconds(30)));
        });

        var runner = new GhostscriptProcessRunner(_fileManager, _logger, fakeStarter);
        var inputPath = CreateFakeValidPdf("input_secret.pdf", "SECRET_INTERNAL_CONTENT");
        var outputPath = Path.Combine(_testWorkingDir, "output_secret.pdf");
        var exePath = CreateFakeValidPdf("gswin64c.exe");

        var parameters = new GhostscriptExecutionParams(exePath, inputPath, outputPath, 200);
        await runner.RunAsync(parameters, CancellationToken.None);

        // Verify logs contain DPI and success info, but NO document text
        Assert.NotEmpty(_logger.Logs);
        Assert.All(_logger.Logs, log => Assert.DoesNotContain("SECRET_INTERNAL_CONTENT", log));
        Assert.Contains(_logger.Logs, log => log.Contains("200") && log.Contains("sucesso"));
    }

    [Fact]
    public async Task RunAsync_AuxiliaryLinuxGs_IfAvailable_ExecutesRealCompression()
    {
        var gsPath = "/usr/bin/gs";
        if (!File.Exists(gsPath))
        {
            return; // Skip on systems without gs
        }

        // Test with real DefaultGhostscriptProcessStarter and real gs
        var starter = new DefaultGhostscriptProcessStarter();
        var runner = new GhostscriptProcessRunner(_fileManager, _logger, starter);

        var inputPath = Path.Combine(_testWorkingDir, "real_in.pdf");
        var outputPath = Path.Combine(_testWorkingDir, "real_out.pdf");

        // Create a minimal synthetic 1-page PDF
        var syntheticPdf = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000052 00000 n \n0000000101 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n178\n%%EOF\n";
        File.WriteAllBytes(inputPath, Encoding.ASCII.GetBytes(syntheticPdf));

        var parameters = new GhostscriptExecutionParams(gsPath, inputPath, outputPath, 150);
        var result = await runner.RunAsync(parameters, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(outputPath));
        var outBytes = File.ReadAllBytes(outputPath);
        Assert.True(outBytes.Length > 0);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(outBytes));
    }

    private sealed class FakeProcessStarter : IGhostscriptProcessStarter
    {
        private readonly Func<string, IReadOnlyList<string>, Task<ProcessRunOutput>> _executeHandler;

        public FakeProcessStarter(Func<string, IReadOnlyList<string>, Task<ProcessRunOutput>> executeHandler)
        {
            _executeHandler = executeHandler;
        }

        public Task<ProcessRunOutput> StartAndRunAsync(string executablePath, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _executeHandler(executablePath, arguments);
        }
    }

    private sealed class TestDiagnosticLogger : IDiagnosticLogger
    {
        public List<string> Logs { get; } = [];

        public void LogInfo(string message) => Logs.Add($"[INFO] {message}");
        public void LogWarning(string message) => Logs.Add($"[WARN] {message}");
        public void LogError(string message, Exception? ex = null) => Logs.Add($"[ERROR] {message} {ex?.Message}");
        public void LogCompressionSummary(CompressionResult result) => Logs.Add($"[SUMMARY] {result.Status}");
        public void LogAnalysisSummary(PdfInfo info) => Logs.Add($"[ANALYSIS] {info.FilePath}");
    }
}
