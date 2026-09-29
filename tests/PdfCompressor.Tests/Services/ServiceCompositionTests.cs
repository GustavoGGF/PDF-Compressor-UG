using Microsoft.Extensions.DependencyInjection;
using PdfCompressor.Infrastructure;
using PdfCompressor.Models;
using PdfCompressor.Services;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class ServiceCompositionTests
{
    [Fact]
    public void CreateDefault_ResolvesCoreInfrastructureServices()
    {
        var container = AppServiceContainer.CreateDefault();

        Assert.NotNull(container.FileManager);
        Assert.NotNull(container.Logger);
        Assert.NotNull(container.PdfAnalyzer);
        Assert.IsType<FileManagerService>(container.FileManager);
        Assert.IsType<DiagnosticLogger>(container.Logger);
        Assert.IsType<PdfAnalyzerService>(container.PdfAnalyzer);
    }

    [Fact]
    public async Task CreateWithCustomServices_AllowsInjectingFakes()
    {
        var fakeAnalyzer = new FakePdfAnalyzer();
        var fakeLocator = new FakeGhostscriptLocator();
        var fakeRunner = new FakeGhostscriptProcessRunner();
        var fakeEngine = new FakeCompressionEngine();

        var container = AppServiceContainer.CreateWithCustomServices(services =>
        {
            services.AddSingleton<IPdfAnalyzerService>(fakeAnalyzer);
            services.AddSingleton<IGhostscriptLocator>(fakeLocator);
            services.AddSingleton<IGhostscriptProcessRunner>(fakeRunner);
            services.AddSingleton<ICompressionEngine>(fakeEngine);
        });

        Assert.Same(fakeAnalyzer, container.PdfAnalyzer);
        Assert.Same(fakeLocator, container.GhostscriptLocator);
        Assert.Same(fakeRunner, container.ProcessRunner);
        Assert.Same(fakeEngine, container.CompressionEngine);

        using var cts = new CancellationTokenSource();
        var info = await container.PdfAnalyzer!.AnalyzeAsync("fake.pdf", cts.Token);
        Assert.Equal(1_000_000, info.FileSizeBytes);
        Assert.Equal(1.0, info.FileSizeMb);

        Assert.True(container.GhostscriptLocator!.IsAvailable());
        Assert.Equal("10.02.1", container.GhostscriptLocator.GetInstalledVersion("gs"));

        var execResult = await container.ProcessRunner!.RunAsync(
            new GhostscriptExecutionParams("gs", "in.pdf", "out.pdf", 150),
            cts.Token
        );
        Assert.True(execResult.Success);
        Assert.Equal(0, execResult.ExitCode);

        var compressResult = await container.CompressionEngine!.CompressAsync(
            new CompressionOptions("in.pdf", "/out", CompressionPreset.HighQuality),
            null,
            cts.Token
        );
        Assert.Equal(CompressionStatus.TargetMet, compressResult.Status);
    }

    private sealed class FakePdfAnalyzer : IPdfAnalyzerService
    {
        public Task<PdfInfo> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new PdfInfo(
                FilePath: filePath,
                FileSizeBytes: 1_000_000,
                PageCount: 5,
                HasLikelySignature: false,
                IsEncrypted: false,
                IsValid: true
            ));
        }
    }

    private sealed class FakeGhostscriptLocator : IGhostscriptLocator
    {
        public bool IsAvailable() => true;
        public string? FindExecutablePath(string? customPath = null) => customPath ?? "C:\\tools\\gswin64c.exe";
        public string? GetInstalledVersion(string executablePath) => "10.02.1";
    }

    private sealed class FakeGhostscriptProcessRunner : IGhostscriptProcessRunner
    {
        public Task<GhostscriptExecutionResult> RunAsync(GhostscriptExecutionParams parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new GhostscriptExecutionResult(
                Success: true,
                ExitCode: 0,
                ExecutionTime: TimeSpan.FromMilliseconds(100),
                StandardOutput: "GPL Ghostscript 10.02.1\nPage 1\n",
                StandardError: string.Empty
            ));
        }
    }

    private sealed class FakeCompressionEngine : ICompressionEngine
    {
        public Task<CompressionResult> CompressAsync(
            CompressionOptions options,
            IProgress<CompressionProgressUpdate>? progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new CompressionProgressUpdate(1, 1, 300, "Concluído", 100));

            return Task.FromResult(new CompressionResult(
                Status: CompressionStatus.TargetMet,
                SourceFilePath: options.SourceFilePath,
                OutputFilePath: Path.Combine(options.TargetDirectory, "compressed.pdf"),
                OriginalSizeBytes: 1_000_000,
                FinalSizeBytes: 500_000,
                FinalDpi: 300,
                Attempts: [
                    new AttemptResult(1, 300, 500_000, TimeSpan.FromMilliseconds(100), true)
                ],
                TotalDuration: TimeSpan.FromMilliseconds(100),
                Message: "Sucesso simulado"
            ));
        }
    }
}
