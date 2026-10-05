using System.Diagnostics;
using System.Text;
using PdfCompressor.Models;
using PdfCompressor.Services;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class CompressionEngineTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FakeLocator _locator;
    private readonly FakeRunner _runner;
    private readonly FakeAnalyzer _analyzer;
    private readonly TrackingFileManager _fileManager;
    private readonly MemoryLogger _logger;
    private readonly CompressionEngine _sut;

    public CompressionEngineTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "PdfCompressorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        _locator = new FakeLocator();
        _runner = new FakeRunner();
        _analyzer = new FakeAnalyzer();
        _fileManager = new TrackingFileManager(Path.Combine(_testRoot, "attempts"));
        _logger = new MemoryLogger();

        _sut = new CompressionEngine(_locator, _runner, _fileManager, _analyzer, _logger);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Limpeza de testes
        }
    }

    private string CreateFakePdfFile(string fileName = "sample.pdf", int sizeBytes = 10_000)
    {
        string filePath = Path.Combine(_testRoot, fileName);
        var bytes = new byte[sizeBytes];
        var header = "%PDF-1.7\n"u8.ToArray();
        Array.Copy(header, bytes, Math.Min(header.Length, bytes.Length));
        File.WriteAllBytes(filePath, bytes);
        return filePath;
    }

    [Fact]
    public async Task CompressAsync_WhenOptionsNull_ReturnsInvalidInput()
    {
        var result = await _sut.CompressAsync(null!);

        Assert.Equal(CompressionStatus.InvalidInput, result.Status);
        Assert.Null(result.OutputFilePath);
    }

    [Fact]
    public async Task CompressAsync_WhenAutomaticModeAndTargetSizeBytesNullOrZero_ReturnsInvalidInput()
    {
        string input = CreateFakePdfFile();
        var optionsZero = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 0);

        var resultZero = await _sut.CompressAsync(optionsZero);
        Assert.Equal(CompressionStatus.InvalidInput, resultZero.Status);

        var optionsNegative = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: -100);
        var resultNegative = await _sut.CompressAsync(optionsNegative);
        Assert.Equal(CompressionStatus.InvalidInput, resultNegative.Status);
    }

    [Fact]
    public async Task CompressAsync_WhenCancelledBeforeStart_ReturnsCancelled()
    {
        string input = CreateFakePdfFile();
        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 5_000_000);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await _sut.CompressAsync(options, null, cts.Token);

        Assert.Equal(CompressionStatus.Cancelled, result.Status);
        Assert.Empty(_runner.ExecutionCalls);
    }

    [Fact]
    public async Task CompressAsync_WhenSourceFileIsInvalid_ReturnsInvalidInput()
    {
        string input = CreateFakePdfFile();
        _analyzer.ConfiguredInfo = PdfInfo.Failed(input, "Arquivo corrompido");

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 5_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.InvalidInput, result.Status);
        Assert.Contains("Arquivo corrompido", result.Message);
        Assert.Empty(_runner.ExecutionCalls);
    }

    [Fact]
    public async Task CompressAsync_WhenSourceFileIsPasswordProtected_ReturnsInvalidInputWithSpecificMessage()
    {
        string input = CreateFakePdfFile();
        _analyzer.ConfiguredInfo = PdfInfo.Failed(input, PdfInfo.PasswordProtectedMessage, isEncrypted: true);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 5_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.InvalidInput, result.Status);
        Assert.Equal(PdfInfo.PasswordProtectedMessage, result.Message);
        Assert.Empty(_runner.ExecutionCalls);
    }

    [Fact]
    public async Task CompressAsync_WhenGhostscriptUnavailable_ReturnsToolUnavailable()
    {
        string input = CreateFakePdfFile();
        _locator.Available = false;
        _locator.ExecutablePath = null;

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 5_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.ToolUnavailable, result.Status);
        Assert.Empty(_runner.ExecutionCalls);
    }

    [Fact]
    public async Task CompressAsync_WhenOutputCollidesWithInput_ReturnsInvalidInput()
    {
        string input = CreateFakePdfFile("sample.pdf");
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000, 1);
        _runner.SimulateDpi(300, 5_000);
        _fileManager.CustomGenerateOutputFilePath = (source, _) => source;

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.HighQuality);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.InvalidInput, result.Status);
        Assert.Contains("imutabilidade", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(_runner.ExecutionCalls);
    }

    [Fact]
    public async Task CompressAsync_Automatic_StopsAtFirstEligibleCandidate_SelectingHighestQuality()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 10);

        _runner.SimulateDpi(300, 8_000_000);
        _runner.SimulateDpi(250, 6_000_000);
        _runner.SimulateDpi(200, 3_800_000);
        _runner.SimulateDpi(150, 2_000_000);
        _runner.SimulateDpi(100, 1_000_000);
        _runner.SimulateDpi(72, 500_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.Equal(200, result.FinalDpi);
        Assert.Equal(3_800_000, result.FinalSizeBytes);
        Assert.NotNull(result.OutputFilePath);
        Assert.True(File.Exists(result.OutputFilePath));

        Assert.Equal(3, _runner.ExecutionCalls.Count);
        Assert.Equal(300, _runner.ExecutionCalls[0].Dpi);
        Assert.Equal(250, _runner.ExecutionCalls[1].Dpi);
        Assert.Equal(200, _runner.ExecutionCalls[2].Dpi);

        Assert.DoesNotContain(_runner.ExecutionCalls, call => call.Dpi is 150 or 100 or 72);
        Assert.Equal(3, result.Attempts.Count);
    }

    [Fact]
    public async Task CompressAsync_Automatic_WhenFirstAttempt300DpiMeetsTarget_StopsImmediately()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 12_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 12_000_000, 5);

        _runner.SimulateDpi(300, 7_000_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 8_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.Equal(300, result.FinalDpi);
        Assert.Equal(7_000_000, result.FinalSizeBytes);
        Assert.Single(_runner.ExecutionCalls);
        Assert.Single(result.Attempts);
    }

    [Fact]
    public async Task CompressAsync_Automatic_ExactTargetMatch_ReturnsTargetMet()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 4);

        _runner.SimulateDpi(300, 5_000_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 5_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.Equal(5_000_000, result.FinalSizeBytes);
    }

    [Fact]
    public async Task CompressAsync_Automatic_OriginalFileAlreadyBelowTarget_ReturnsTargetMetWithoutCreatingOutput()
    {
        string input = CreateFakePdfFile("small.pdf", sizeBytes: 2_000_000);
        byte[] originalBytes = File.ReadAllBytes(input);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 2_000_000, 2);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 5_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.Equal(0, result.FinalSizeBytes);
        Assert.Null(result.FinalDpi);
        Assert.Empty(result.Attempts);
        Assert.Empty(_runner.ExecutionCalls);
        Assert.Contains("já atende", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(input));
        Assert.Equal(2_000_000, new FileInfo(input).Length);
        Assert.Null(result.OutputFilePath);
        Assert.DoesNotContain(Directory.EnumerateFiles(_testRoot), path => path.EndsWith("_compactado.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(originalBytes, File.ReadAllBytes(input));
    }

    [Fact]
    public async Task CompressAsync_Automatic_DoesNotCreateOutputWhenCandidatesAreLargerThanOriginal()
    {
        string input = CreateFakePdfFile("text.pdf", sizeBytes: 900_000);
        byte[] originalBytes = File.ReadAllBytes(input);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 900_000, 10);
        _runner.SimulateDpi(300, 1_800_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 800_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.NoReduction, result.Status);
        Assert.Equal(0, result.FinalSizeBytes);
        Assert.Null(result.FinalDpi);
        Assert.Equal(6, result.Attempts.Count);
        Assert.Null(result.OutputFilePath);
        Assert.DoesNotContain(Directory.EnumerateFiles(_testRoot), path => path.EndsWith("_compactado.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(originalBytes, File.ReadAllBytes(input));
    }

    [Fact]
    public async Task CompressAsync_Automatic_WhenTargetMissedAndCandidatesAreLarger_ReturnsNoReduction()
    {
        string input = CreateFakePdfFile("already-smallest.pdf", sizeBytes: 3_000_000);
        byte[] originalBytes = File.ReadAllBytes(input);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 3_000_000, 10);
        _runner.SimulateDpi(300, 3_500_000);
        _runner.SimulateDpi(250, 3_200_000);
        _runner.SimulateDpi(200, 3_100_000);
        _runner.SimulateDpi(150, 3_050_000);
        _runner.SimulateDpi(100, 3_025_000);
        _runner.SimulateDpi(72, 3_010_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 1_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.NoReduction, result.Status);
        Assert.Equal(0, result.FinalSizeBytes);
        Assert.Null(result.FinalDpi);
        Assert.Equal(6, result.Attempts.Count);
        Assert.Contains("Nenhuma tentativa reduziu", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.OutputFilePath);
        Assert.DoesNotContain(Directory.EnumerateFiles(_testRoot), path => path.EndsWith("_compactado.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(originalBytes, File.ReadAllBytes(input));
    }

    [Fact]
    public async Task CompressAsync_Automatic_WhenNoAttemptMeetsTarget_DoesNotCreateOutput()
    {
        string input = CreateFakePdfFile("dense.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 8);

        _runner.SimulateDpi(300, 8_000_000);
        _runner.SimulateDpi(250, 6_000_000);
        _runner.SimulateDpi(200, 5_000_000);
        _runner.SimulateDpi(150, 4_000_000);
        _runner.SimulateDpi(100, 3_000_000);
        _runner.SimulateDpi(72, 2_400_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 1_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.NoReduction, result.Status);
        Assert.Null(result.FinalDpi);
        Assert.Equal(0, result.FinalSizeBytes);
        Assert.Null(result.OutputFilePath);
        Assert.DoesNotContain(Directory.EnumerateFiles(_testRoot), path => path.EndsWith("_compactado.pdf", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(6, _runner.ExecutionCalls.Count);
        Assert.Equal(6, result.Attempts.Count);
        Assert.Contains("Nenhuma tentativa atingiu o tamanho-alvo", result.Message);
    }

    [Fact]
    public async Task CompressAsync_Automatic_WhenIntermediateAttemptFails_ContinuesToNextQuality()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 5);

        _runner.SimulateFailure(300, GhostscriptFailureReason.NonZeroExitCode, "Erro interno GS");
        _runner.SimulateDpi(250, 3_500_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.Equal(250, result.FinalDpi);
        Assert.Equal(3_500_000, result.FinalSizeBytes);
        Assert.Equal(2, result.Attempts.Count);
        Assert.False(result.Attempts[0].Succeeded);
        Assert.True(result.Attempts[1].Succeeded);
    }

    [Fact]
    public async Task CompressAsync_Automatic_WhenAllAttemptsFail_ReturnsEngineFailed()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 3);

        foreach (int dpi in new[] { 300, 250, 200, 150, 100, 72 })
        {
            _runner.SimulateFailure(dpi, GhostscriptFailureReason.NonZeroExitCode, "Falha fatal");
        }

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.EngineFailed, result.Status);
        Assert.Null(result.OutputFilePath);
        Assert.Equal(6, result.Attempts.Count);
        Assert.All(result.Attempts, a => Assert.False(a.Succeeded));
    }

    [Fact]
    public async Task CompressAsync_WhenCancelledDuringAttempt_ReturnsCancelled()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 5);
        _runner.SimulateFailure(300, GhostscriptFailureReason.Cancelled, "Cancelado");

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.Cancelled, result.Status);
        Assert.Null(result.OutputFilePath);
    }

    [Fact]
    public async Task CompressAsync_WhenExecutableNotFoundInRun_ReturnsToolUnavailable()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 5);
        _runner.SimulateFailure(300, GhostscriptFailureReason.ExecutableNotFound, "Arquivo não encontrado");

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.ToolUnavailable, result.Status);
    }

    [Fact]
    public async Task CompressAsync_WhenDiskOrPermissionDenied_ReturnsEngineFailed()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 5);
        _runner.SimulateFailure(300, GhostscriptFailureReason.PermissionOrDiskDenied, "Disco cheio");

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.EngineFailed, result.Status);
        Assert.Contains("Disco cheio", result.Message);
    }

    [Fact]
    public async Task CompressAsync_ReportsMonotonicProgress()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 5);

        _runner.SimulateDpi(300, 8_000_000);
        _runner.SimulateDpi(250, 6_000_000);
        _runner.SimulateDpi(200, 3_500_000);

        var progressUpdates = new List<CompressionProgressUpdate>();
        var progress = new Progress<CompressionProgressUpdate>(progressUpdates.Add);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        var result = await _sut.CompressAsync(options, progress);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.NotEmpty(progressUpdates);

        for (int i = 1; i < progressUpdates.Count; i++)
        {
            Assert.True(
                progressUpdates[i].PercentageEstimate >= progressUpdates[i - 1].PercentageEstimate,
                $"Progresso não monotônico: {progressUpdates[i - 1].PercentageEstimate}% -> {progressUpdates[i].PercentageEstimate}%"
            );
        }

        Assert.Equal(100, progressUpdates[^1].PercentageEstimate);
    }

    [Fact]
    public async Task CompressAsync_CleansUpAllSessionTempDirectories()
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 5);

        _runner.SimulateDpi(300, 8_000_000);
        _runner.SimulateDpi(250, 3_500_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 4_000_000);
        await _sut.CompressAsync(options);

        Assert.NotEmpty(_fileManager.CreatedDirectories);
        Assert.Equal(_fileManager.CreatedDirectories.Count, _fileManager.DeletedDirectories.Count);
        foreach (var dir in _fileManager.CreatedDirectories)
        {
            Assert.False(Directory.Exists(dir), $"Diretório temporário não foi excluído: {dir}");
        }
    }

    [Theory]
    [InlineData(CompressionPreset.HighQuality, 300)]
    [InlineData(CompressionPreset.MediumQuality, 150)]
    [InlineData(CompressionPreset.StrongCompression, 72)]
    public async Task CompressAsync_ManualPresets_RunSingleAttemptAtConfiguredDpi(CompressionPreset preset, int expectedDpi)
    {
        string input = CreateFakePdfFile("input.pdf", sizeBytes: 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 4);

        _runner.SimulateDpi(expectedDpi, 4_500_000);

        var options = new CompressionOptions(input, _testRoot, preset);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        Assert.Equal(expectedDpi, result.FinalDpi);
        Assert.Single(_runner.ExecutionCalls);
        Assert.Equal(expectedDpi, _runner.ExecutionCalls[0].Dpi);
    }

    [Fact]
    public async Task CompressAsync_NeverModifiesSourceFile()
    {
        string input = CreateFakePdfFile("preserve_original.pdf", sizeBytes: 50_000);
        byte[] originalBytes = File.ReadAllBytes(input);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 50_000, 2);

        _runner.SimulateDpi(300, 30_000);

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.HighQuality);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.TargetMet, result.Status);
        byte[] afterBytes = File.ReadAllBytes(input);
        Assert.Equal(originalBytes, afterBytes);
    }
}
