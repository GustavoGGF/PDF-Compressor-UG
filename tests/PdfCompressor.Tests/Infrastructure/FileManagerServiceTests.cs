using PdfCompressor.Infrastructure;
using PdfCompressor.Services;
using PdfCompressor.Tests.Services;
using Xunit;

namespace PdfCompressor.Tests.Infrastructure;

public sealed class FileManagerServiceTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly MemoryLogger _logger;
    private readonly FileManagerService _fileManager;

    public FileManagerServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "PdfCompressor_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
        _logger = new MemoryLogger();
        _fileManager = new FileManagerService(_logger, _testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch
        {
            // Limpeza de melhor esforço dos testes
        }
    }

    [Fact]
    public void GenerateSafeOutputFilePath_WhenTargetFileDoesNotExist_AppendsCompactado()
    {
        string source = Path.Combine(_testTempDir, "documento.pdf");
        string result = _fileManager.GenerateSafeOutputFilePath(source);

        Assert.Equal(Path.Combine(_testTempDir, "documento_compactado.pdf"), result);
    }

    [Fact]
    public void GenerateSafeOutputFilePath_CollisionMatrix_ZeroOneAndMultipleExisting()
    {
        string source = Path.Combine(_testTempDir, "relatorio.pdf");

        // Cenário 0: nenhum arquivo existente
        string result0 = _fileManager.GenerateSafeOutputFilePath(source);
        Assert.Equal(Path.Combine(_testTempDir, "relatorio_compactado.pdf"), result0);

        // Cenário 1: arquivo _compactado já existe
        File.WriteAllText(result0, "mock content 0");
        string result1 = _fileManager.GenerateSafeOutputFilePath(source);
        Assert.Equal(Path.Combine(_testTempDir, "relatorio_compactado_1.pdf"), result1);

        // Cenário 2: arquivo _compactado_1 já existe
        File.WriteAllText(result1, "mock content 1");
        string result2 = _fileManager.GenerateSafeOutputFilePath(source);
        Assert.Equal(Path.Combine(_testTempDir, "relatorio_compactado_2.pdf"), result2);

        // Cenário 3: arquivos intermediários existentes
        File.WriteAllText(result2, "mock content 2");
        File.WriteAllText(Path.Combine(_testTempDir, "relatorio_compactado_3.pdf"), "mock content 3");
        string result4 = _fileManager.GenerateSafeOutputFilePath(source);
        Assert.Equal(Path.Combine(_testTempDir, "relatorio_compactado_4.pdf"), result4);
    }

    [Fact]
    public void GenerateSafeOutputFilePath_WhenSourceAlreadyHasCompactado_NeverCollidesWithSource()
    {
        string source = Path.Combine(_testTempDir, "processo_compactado.pdf");
        File.WriteAllText(source, "source content");

        string result = _fileManager.GenerateSafeOutputFilePath(source);

        Assert.NotEqual(source, result);
        Assert.EndsWith(".pdf", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateSafeOutputFilePath_WithSpacesAccentsAndLongNames()
    {
        string specialName = "Processo Judicial de Ação Ordinária nº 1234 — com espaços e acentuação.pdf";
        string source = Path.Combine(_testTempDir, specialName);

        string result = _fileManager.GenerateSafeOutputFilePath(source);

        string expectedName = "Processo Judicial de Ação Ordinária nº 1234 — com espaços e acentuação_compactado.pdf";
        Assert.Equal(Path.Combine(_testTempDir, expectedName), result);
    }

    [Fact]
    public void GenerateSafeOutputFilePath_RespectsCustomTargetDirectory()
    {
        string customDir = Path.Combine(_testTempDir, "pasta_personalizada");
        Directory.CreateDirectory(customDir);
        string source = Path.Combine(_testTempDir, "peticao.pdf");

        string result = _fileManager.GenerateSafeOutputFilePath(source, customDir);
        Assert.Equal(Path.Combine(customDir, "peticao_compactado.pdf"), result);
    }

    [Fact]
    public void CreateIsolatedTempDirectory_CreatesUniqueDirectory()
    {
        string dir1 = _fileManager.CreateIsolatedTempDirectory();
        string dir2 = _fileManager.CreateIsolatedTempDirectory();

        Assert.True(Directory.Exists(dir1));
        Assert.True(Directory.Exists(dir2));
        Assert.NotEqual(dir1, dir2);

        Assert.True(_fileManager.SafeDeleteDirectory(dir1));
        Assert.True(_fileManager.SafeDeleteDirectory(dir2));

        Assert.False(Directory.Exists(dir1));
        Assert.False(Directory.Exists(dir2));
    }

    [Fact]
    public void SafeDeleteDirectory_NonExistentOrNullDirectory_ReturnsTrueWithoutThrowing()
    {
        Assert.True(_fileManager.SafeDeleteDirectory(""));
        Assert.True(_fileManager.SafeDeleteDirectory(Path.Combine(_testTempDir, "non_existent_folder_xyz")));
    }

    [Fact]
    public void SafeDeleteFile_ExistingFile_DeletesSuccessfullyAndReturnsTrue()
    {
        string testFile = Path.Combine(_testTempDir, "to_delete.tmp");
        File.WriteAllText(testFile, "test data");
        Assert.True(File.Exists(testFile));

        bool deleted = _fileManager.SafeDeleteFile(testFile);
        Assert.True(deleted);
        Assert.False(File.Exists(testFile));
        Assert.Contains(_logger.CleanupLogs, c => c.Target.EndsWith("to_delete.tmp", StringComparison.OrdinalIgnoreCase) && c.Succeeded);
    }

    [Fact]
    public void SafeDeleteFile_NonExistentOrNull_ReturnsTrueWithoutThrowing()
    {
        Assert.True(_fileManager.SafeDeleteFile(""));
        Assert.True(_fileManager.SafeDeleteFile(Path.Combine(_testTempDir, "non_existent_file.tmp")));
    }

    [Fact]
    public void TryPromoteFile_WhenSourceExistsAndValid_PromotesAtomically()
    {
        string sourceTemp = Path.Combine(_testTempDir, "attempt_winner.pdf");
        File.WriteAllText(sourceTemp, "VALID PDF CANDIDATE BYTES");

        string targetDir = Path.Combine(_testTempDir, "final_output_folder");
        string destPath = Path.Combine(targetDir, "documento_compactado.pdf");

        bool success = _fileManager.TryPromoteFile(sourceTemp, destPath, out string? errorMessage);

        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.True(File.Exists(destPath));
        Assert.Equal("VALID PDF CANDIDATE BYTES", File.ReadAllText(destPath));

        // Nenhum resíduo de staging temporário deve existir na pasta de destino
        string[] files = Directory.GetFiles(targetDir);
        Assert.Single(files);
        Assert.Equal(destPath, files[0]);
    }

    [Fact]
    public void TryPromoteFile_WhenSourceTempMissing_ReturnsFalseWithActionableMessage()
    {
        string missingTemp = Path.Combine(_testTempDir, "missing.tmp");
        string destPath = Path.Combine(_testTempDir, "output.pdf");

        bool success = _fileManager.TryPromoteFile(missingTemp, destPath, out string? errorMessage);

        Assert.False(success);
        Assert.NotNull(errorMessage);
        Assert.Contains("não foi localizado", errorMessage);
        Assert.False(File.Exists(destPath));
    }

    [Fact]
    public void TryPromoteFile_WhenSourceZeroBytes_ReturnsFalseWithActionableMessage()
    {
        string zeroByteTemp = Path.Combine(_testTempDir, "empty.pdf");
        File.WriteAllBytes(zeroByteTemp, []);

        string destPath = Path.Combine(_testTempDir, "output_empty.pdf");

        bool success = _fileManager.TryPromoteFile(zeroByteTemp, destPath, out string? errorMessage);

        Assert.False(success);
        Assert.NotNull(errorMessage);
        Assert.Contains("0 bytes", errorMessage);
        Assert.False(File.Exists(destPath));
    }

    [Fact]
    public void TryPromoteFile_WhenDestinationDirectoryHasNoWritePermission_ReturnsFalseAndCleansStaging()
    {
        string sourceTemp = Path.Combine(_testTempDir, "attempt_winner.pdf");
        File.WriteAllText(sourceTemp, "NEW CONTENT");

        string restrictedDir = Path.Combine(_testTempDir, "restricted_dir");
        Directory.CreateDirectory(restrictedDir);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(restrictedDir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
        }

        string destPath = Path.Combine(restrictedDir, "output.pdf");

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                bool success = _fileManager.TryPromoteFile(sourceTemp, destPath, out string? errorMessage);

                Assert.False(success);
                Assert.NotNull(errorMessage);
                Assert.Contains("permissões", errorMessage, StringComparison.OrdinalIgnoreCase);

                string[] tmpFiles = Directory.GetFiles(restrictedDir, ".*.tmp");
                Assert.Empty(tmpFiles);
            }
        }
        finally
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(restrictedDir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
    }

    [Fact]
    public void TryPromoteFile_WhenDestinationIsLocked_ReturnsFalseOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return; // Bloqueio mandatário com FileShare.None é uma característica do subsistema de arquivos do Windows
        }

        string sourceTemp = Path.Combine(_testTempDir, "attempt_locked.pdf");
        File.WriteAllText(sourceTemp, "NEW CONTENT");

        string destPath = Path.Combine(_testTempDir, "locked_target.pdf");
        File.WriteAllText(destPath, "INITIAL CONTENT");

        using (var lockStream = new FileStream(destPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            bool success = _fileManager.TryPromoteFile(sourceTemp, destPath, out string? errorMessage);

            Assert.False(success);
            Assert.NotNull(errorMessage);
            Assert.Contains("bloqueado", errorMessage, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal("INITIAL CONTENT", File.ReadAllText(destPath));
        string[] tmpFiles = Directory.GetFiles(_testTempDir, ".*.tmp");
        Assert.Empty(tmpFiles);
    }
}
