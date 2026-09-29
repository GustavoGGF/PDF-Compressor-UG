using System.Security.Cryptography;
using PdfCompressor.Models;
using PdfCompressor.Services;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class CompressionEngineOutputAndCleanupTests : IDisposable
{
    private readonly string _testRoot;
    private readonly FakeLocator _locator;
    private readonly FakeRunner _runner;
    private readonly FakeAnalyzer _analyzer;
    private readonly TrackingFileManager _fileManager;
    private readonly MemoryLogger _logger;
    private readonly CompressionEngine _sut;

    public CompressionEngineOutputAndCleanupTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "PdfCompressor_OutputTests_" + Guid.NewGuid().ToString("N"));
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

    private string CreateFakePdfFile(string fileName, int sizeBytes)
    {
        string filePath = Path.Combine(_testRoot, fileName);
        var bytes = new byte[sizeBytes];
        var header = "%PDF-1.7\n"u8.ToArray();
        Array.Copy(header, bytes, Math.Min(header.Length, bytes.Length));
        File.WriteAllBytes(filePath, bytes);
        return filePath;
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        byte[] hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    [Fact]
    public async Task CompressAsync_WhenPromotionFails_ReturnsEngineFailedCleansTempAndSetsOutputNull()
    {
        string input = CreateFakePdfFile("doc.pdf", 5_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 5_000_000, 3);
        _runner.SimulateDpi(300, 2_000_000);

        _fileManager.FailPromotion = true;
        _fileManager.FailPromotionErrorMessage = "Acesso negado ao destino final.";

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.HighQuality);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.EngineFailed, result.Status);
        Assert.Null(result.OutputFilePath);
        Assert.Contains("Acesso negado ao destino final", result.Message);

        // Garante que todos os diretórios temporários criados foram excluídos
        Assert.NotEmpty(_fileManager.CreatedDirectories);
        Assert.Equal(_fileManager.CreatedDirectories.Count, _fileManager.DeletedDirectories.Count);
    }

    [Fact]
    public async Task CompressAsync_WhenBestEffortPromotionFails_ReturnsEngineFailedAndCleansTemp()
    {
        string input = CreateFakePdfFile("grande.pdf", 10_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 10_000_000, 5);

        // Todas as tentativas acima do limite de 1 MB
        _runner.SimulateDpi(300, 4_000_000);
        _runner.SimulateDpi(250, 3_500_000);
        _runner.SimulateDpi(200, 3_000_000);
        _runner.SimulateDpi(150, 2_500_000);
        _runner.SimulateDpi(100, 2_000_000);
        _runner.SimulateDpi(72, 1_500_000);

        _fileManager.FailPromotion = true;
        _fileManager.FailPromotionErrorMessage = "Arquivo bloqueado no destino.";

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 1_000_000);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.EngineFailed, result.Status);
        Assert.Null(result.OutputFilePath);
        Assert.Contains("Arquivo bloqueado no destino", result.Message);
        Assert.Equal(_fileManager.CreatedDirectories.Count, _fileManager.DeletedDirectories.Count);
    }

    [Fact]
    public async Task CompressAsync_WhenInputEqualsOutput_ReturnsInvalidInputWithPreservationMessage()
    {
        string input = CreateFakePdfFile("mesmo_nome.pdf", 2_000_000);
        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 2_000_000, 2);

        // Força a colisão configurando gerador fake para retornar exatamente o arquivo de entrada
        _fileManager.CustomGenerateOutputFilePath = (src, _) => src;

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.HighQuality);
        var result = await _sut.CompressAsync(options);

        Assert.Equal(CompressionStatus.InvalidInput, result.Status);
        Assert.Null(result.OutputFilePath);
        Assert.Contains("preservar o original", result.Message);
        Assert.Empty(_runner.ExecutionCalls);
    }

    [Theory]
    [InlineData("TargetMet")]
    [InlineData("BestEffortAboveTarget")]
    [InlineData("Cancelled")]
    [InlineData("EngineFailed")]
    public async Task CompressAsync_InAllTerminalStates_PreservesOriginalHashAndCleansSession(string scenario)
    {
        string input = CreateFakePdfFile($"preserve_{scenario}.pdf", 3_000_000);
        string initialHash = ComputeSha256(input);
        long initialLength = new FileInfo(input).Length;

        _analyzer.ConfiguredInfo = PdfInfo.Success(input, 3_000_000, 2);

        using var cts = new CancellationTokenSource();

        switch (scenario)
        {
            case "TargetMet":
                _runner.SimulateDpi(300, 1_000_000);
                break;
            case "BestEffortAboveTarget":
                foreach (int dpi in CompressionPresetPolicy.GetDpiSequence(CompressionPreset.Automatic))
                {
                    _runner.SimulateDpi(dpi, 2_000_000);
                }
                break;
            case "Cancelled":
                cts.Cancel();
                break;
            case "EngineFailed":
                foreach (int dpi in CompressionPresetPolicy.GetDpiSequence(CompressionPreset.Automatic))
                {
                    _runner.SimulateFailure(dpi, GhostscriptFailureReason.NonZeroExitCode, "Exit code 1");
                }
                break;
        }

        var options = new CompressionOptions(input, _testRoot, CompressionPreset.Automatic, TargetSizeBytes: 1_500_000);
        var result = await _sut.CompressAsync(options, progress: null, cancellationToken: cts.Token);

        // 1. Integridade estrita da entrada
        string finalHash = ComputeSha256(input);
        long finalLength = new FileInfo(input).Length;
        Assert.Equal(initialHash, finalHash);
        Assert.Equal(initialLength, finalLength);

        // 2. Limpeza estrita de diretórios de sessão intermediários
        if (_fileManager.CreatedDirectories.Count > 0)
        {
            Assert.Equal(_fileManager.CreatedDirectories.Count, _fileManager.DeletedDirectories.Count);
            foreach (var dir in _fileManager.CreatedDirectories)
            {
                Assert.False(Directory.Exists(dir), $"Diretório intermediário abandonado: {dir}");
            }
        }

        // 3. Destino final só possui arquivo válido se o status for TargetMet ou BestEffort
        if (result.Status == CompressionStatus.TargetMet || result.Status == CompressionStatus.BestEffortAboveTarget)
        {
            Assert.NotNull(result.OutputFilePath);
            Assert.True(File.Exists(result.OutputFilePath));
            Assert.True(new FileInfo(result.OutputFilePath).Length > 0);
        }
        else
        {
            Assert.Null(result.OutputFilePath);
        }
    }
}
