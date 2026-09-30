using System.Security.Cryptography;
using System.Text;
using PdfCompressor.Infrastructure;
using PdfCompressor.Models;
using PdfCompressor.Services;
using PdfCompressor.Tests.Fixtures;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class PdfAnalyzerServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _logFilePath;
    private readonly DiagnosticLogger _logger;
    private readonly PdfAnalyzerService _analyzer;

    public PdfAnalyzerServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PdfAnalyzerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
        _logFilePath = Path.Combine(_tempDirectory, "test_analyzer.log");
        _logger = new DiagnosticLogger(_logFilePath);
        _analyzer = new PdfAnalyzerService(_logger);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private string WriteTempPdf(string fileName, byte[] content)
    {
        string path = Path.Combine(_tempDirectory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    [Fact]
    public async Task AnalyzeAsync_ValidOnePagePdf_ReturnsSuccessWithOnePage()
    {
        string path = WriteTempPdf("single.pdf", PdfTestFixtures.CreateOnePagePdf());

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.True(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Success, result.Status);
        Assert.Equal(1, result.PageCount);
        Assert.False(result.HasLikelySignature);
        Assert.False(result.IsEncrypted);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.WarningMessage);
        Assert.True(result.FileSizeBytes > 0);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidMultiPagePdf_ReturnsSuccessWithExactPageCount()
    {
        string path = WriteTempPdf("multi3.pdf", PdfTestFixtures.CreateMultiPagePdf(3));

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.True(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Success, result.Status);
        Assert.Equal(3, result.PageCount);
    }

    [Fact]
    public async Task AnalyzeAsync_NonExistentFile_ReturnsFailedWithActionableMessage()
    {
        string path = Path.Combine(_tempDirectory, "inexistente.pdf");

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Equal("Arquivo não encontrado.", result.ErrorMessage);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public async Task AnalyzeAsync_DirectoryPath_ReturnsFailed()
    {
        var result = await _analyzer.AnalyzeAsync(_tempDirectory);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Contains("diretório", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("documento.txt")]
    [InlineData("documento.docx")]
    [InlineData("documento")]
    public async Task AnalyzeAsync_InvalidExtension_ReturnsFailed(string fileName)
    {
        string path = Path.Combine(_tempDirectory, fileName);
        await File.WriteAllTextAsync(path, "conteudo");

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Contains("Extensão de arquivo inválida", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_EmptyOrNullPath_ReturnsFailed()
    {
        var resultNull = await _analyzer.AnalyzeAsync(string.Empty);
        var resultWhitespace = await _analyzer.AnalyzeAsync("   ");

        Assert.False(resultNull.IsValid);
        Assert.False(resultWhitespace.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, resultNull.Status);
        Assert.Equal(PdfAnalysisStatus.Failed, resultWhitespace.Status);
    }

    [Fact]
    public async Task AnalyzeAsync_ZeroByteFile_ReturnsFailed()
    {
        string path = Path.Combine(_tempDirectory, "vazio.pdf");
        await File.WriteAllBytesAsync(path, []);

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Contains("vazio", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_CorruptHeader_ReturnsFailed()
    {
        string path = WriteTempPdf("corrupt_header.pdf", PdfTestFixtures.CreateCorruptHeaderFile());

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Contains("não é um PDF válido", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_TruncatedPdf_ReturnsFailed()
    {
        string path = WriteTempPdf("truncated.pdf", PdfTestFixtures.CreateTruncatedPdf());

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Contains("truncado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidPdfWithTrailingNullPadding_ReturnsSuccess()
    {
        byte[] validPdf = PdfTestFixtures.CreateOnePagePdf();
        byte[] paddedPdf = new byte[validPdf.Length + 10000];
        Buffer.BlockCopy(validPdf, 0, paddedPdf, 0, validPdf.Length);
        // trailing 10000 bytes are zeroes (0x00)

        string path = WriteTempPdf("padded.pdf", paddedPdf);

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.True(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Success, result.Status);
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public async Task AnalyzeAsync_RealImagensPdf_ReturnsSuccess()
    {
        string realPdfPath = Path.Combine(AppContext.BaseDirectory, "../../../../imagens.pdf");
        if (!File.Exists(realPdfPath))
        {
            return; // Executado apenas quando o arquivo estiver no repositório
        }

        var result = await _analyzer.AnalyzeAsync(realPdfPath);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(PdfAnalysisStatus.Success, result.Status);
        Assert.NotNull(result.PageCount);
    }

    [Fact]
    public async Task AnalyzeAsync_SignatureMarkedPdf_ReturnsWarningWithSignatureAlert()
    {
        // TC-06, DEC-07: Detecção de marcadores /ByteRange e /Sig
        string path = WriteTempPdf("signed.pdf", PdfTestFixtures.CreateSignedPdf());

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.True(result.IsValid);
        Assert.True(result.HasLikelySignature);
        Assert.Equal(PdfAnalysisStatus.Warning, result.Status);
        Assert.Equal(PdfInfo.DefaultSignatureWarningMessage, result.WarningMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_PdfWithoutSignature_DoesNotTriggerWarning()
    {
        string path = WriteTempPdf("plain.pdf", PdfTestFixtures.CreateOnePagePdf());

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.True(result.IsValid);
        Assert.False(result.HasLikelySignature);
        Assert.Equal(PdfAnalysisStatus.Success, result.Status);
        Assert.Null(result.WarningMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_EncryptedPdf_ReturnsFailedWithPasswordProtectionMessage()
    {
        // TC-09, DEC-06: Detecção de proteção /Encrypt
        string path = WriteTempPdf("encrypted.pdf", PdfTestFixtures.CreateEncryptedPdf());

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.True(result.IsEncrypted);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Equal(PdfInfo.PasswordProtectedMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeAsync_FileLockedByAnotherProcess_ReturnsFailedWithActionableMessage()
    {
        // TC-11: Arquivo aberto com lock exclusivo
        string path = WriteTempPdf("locked.pdf", PdfTestFixtures.CreateOnePagePdf());

        await using var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Contains("em uso", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_SizeChangedDuringAnalysis_ReturnsFailedForInstability()
    {
        string path = WriteTempPdf("unstable.pdf", PdfTestFixtures.CreateOnePagePdf());

        _analyzer.OnBeforeInspectionHook = async filePath =>
        {
            // Simula processo concorrente alterando o tamanho do arquivo
            await File.AppendAllTextAsync(filePath, "\n% extra bytes\n");
        };

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.False(result.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
        Assert.Contains("estável", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnalyzeAsync_GuaranteesOriginalFileHashAndSizeAreUnaltered()
    {
        string path = WriteTempPdf("immutable.pdf", PdfTestFixtures.CreateOnePagePdf("Original intocado"));
        byte[] bytesBefore = await File.ReadAllBytesAsync(path);
        string hashBefore = Convert.ToHexString(SHA256.HashData(bytesBefore));
        long lengthBefore = new FileInfo(path).Length;

        var result = await _analyzer.AnalyzeAsync(path);

        byte[] bytesAfter = await File.ReadAllBytesAsync(path);
        string hashAfter = Convert.ToHexString(SHA256.HashData(bytesAfter));
        long lengthAfter = new FileInfo(path).Length;

        Assert.True(result.IsValid);
        Assert.Equal(hashBefore, hashAfter);
        Assert.Equal(lengthBefore, lengthAfter);
    }

    [Fact]
    public async Task AnalyzeAsync_ConfidentialContentNotLogged()
    {
        const string secretWord = "TOP_SECRET_DOCUMENT_BODY_XYZ_123";
        string path = WriteTempPdf("confidencial.pdf", PdfTestFixtures.CreateOnePagePdf(secretWord));

        var result = await _analyzer.AnalyzeAsync(path);
        Assert.True(result.IsValid);

        string logText = await File.ReadAllTextAsync(_logFilePath);
        Assert.Contains("File=confidencial.pdf", logText);
        Assert.DoesNotContain(secretWord, logText);
    }

    [Fact]
    public async Task AnalyzeAsync_UnresolvablePageCount_ReturnsNullWithoutInventingValue()
    {
        string path = WriteTempPdf("unresolvable.pdf", PdfTestFixtures.CreateUnresolvablePageCountPdf());

        var result = await _analyzer.AnalyzeAsync(path);

        Assert.True(result.IsValid);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public async Task AnalyzeAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        string path = WriteTempPdf("cancellable.pdf", PdfTestFixtures.CreateOnePagePdf());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _analyzer.AnalyzeAsync(path, cts.Token)
        );
    }

    [Fact]
    public async Task AnalyzeAsync_ReadOnlyPermissionDenied_ReturnsFailedWhenSupported()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string path = WriteTempPdf("no_permission.pdf", PdfTestFixtures.CreateOnePagePdf());
        File.SetUnixFileMode(path, UnixFileMode.None);

        try
        {
            var result = await _analyzer.AnalyzeAsync(path);
            Assert.False(result.IsValid);
            Assert.Equal(PdfAnalysisStatus.Failed, result.Status);
            Assert.Contains("Acesso negado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Restaura permissões para permitir exclusão no Dispose
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_PhysicalSampleFixtures_BehaveAsSpecified()
    {
        string sampleDir = Path.Combine(_tempDirectory, "SampleFixtures");
        SamplePdfFiles.EnsureFixturesCreated(sampleDir);

        // fix-01 (texto simples, 3 páginas)
        var res01 = await _analyzer.AnalyzeAsync(Path.Combine(sampleDir, "fix-01-simple-text.pdf"));
        Assert.True(res01.IsValid);
        Assert.Equal(3, res01.PageCount);
        Assert.Equal(PdfAnalysisStatus.Success, res01.Status);

        // fix-02 (imagens não comprimidas, 1 página)
        var res02 = await _analyzer.AnalyzeAsync(Path.Combine(sampleDir, "fix-02-highres-images.pdf"));
        Assert.True(res02.IsValid);
        Assert.Equal(1, res02.PageCount);
        Assert.Equal(PdfAnalysisStatus.Success, res02.Status);

        // fix-03 (pequeno, 1 página)
        var res03 = await _analyzer.AnalyzeAsync(Path.Combine(sampleDir, "fix-03-already-small.pdf"));
        Assert.True(res03.IsValid);
        Assert.Equal(1, res03.PageCount);

        // fix-04 (assinatura marcada)
        var res04 = await _analyzer.AnalyzeAsync(Path.Combine(sampleDir, "fix-04-signature-marked.pdf"));
        Assert.True(res04.IsValid);
        Assert.True(res04.HasLikelySignature);
        Assert.Equal(PdfAnalysisStatus.Warning, res04.Status);

        // fix-05 (protegido por senha)
        var res05 = await _analyzer.AnalyzeAsync(Path.Combine(sampleDir, "fix-05-password-protected.pdf"));
        Assert.False(res05.IsValid);
        Assert.True(res05.IsEncrypted);
        Assert.Equal(PdfAnalysisStatus.Failed, res05.Status);

        // fix-06 (cabeçalho corrompido)
        var res06 = await _analyzer.AnalyzeAsync(Path.Combine(sampleDir, "fix-06-corrupt-header.pdf"));
        Assert.False(res06.IsValid);
        Assert.Equal(PdfAnalysisStatus.Failed, res06.Status);

        // fix-08 (vetorial denso de 5 páginas)
        var res08 = await _analyzer.AnalyzeAsync(Path.Combine(sampleDir, "fix-08-unreachable-target.pdf"));
        Assert.True(res08.IsValid);
        Assert.Equal(5, res08.PageCount);
        Assert.Equal(PdfAnalysisStatus.Success, res08.Status);
    }
}
