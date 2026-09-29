using PdfCompressor.Infrastructure;
using Xunit;

namespace PdfCompressor.Tests.Infrastructure;

public sealed class FileManagerServiceTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly FileManagerService _fileManager;

    public FileManagerServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "PdfCompressor_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
        _fileManager = new FileManagerService(_testTempDir);
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
            // Best effort test cleanup
        }
    }

    [Fact]
    public void GenerateSafeOutputFilePath_WhenTargetFileDoesNotExist_AppendsCompressed()
    {
        string source = Path.Combine(_testTempDir, "documento.pdf");
        string result = _fileManager.GenerateSafeOutputFilePath(source);

        Assert.Equal(Path.Combine(_testTempDir, "documento_compressed.pdf"), result);
    }

    [Fact]
    public void GenerateSafeOutputFilePath_WhenTargetFileAlreadyExists_AppendsIncrementedIndex()
    {
        string source = Path.Combine(_testTempDir, "relatorio.pdf");
        string firstOutput = Path.Combine(_testTempDir, "relatorio_compressed.pdf");
        File.WriteAllText(firstOutput, "mock content");

        string result = _fileManager.GenerateSafeOutputFilePath(source);
        Assert.Equal(Path.Combine(_testTempDir, "relatorio_compressed_1.pdf"), result);

        // Se o _1 também existir, gera _2
        File.WriteAllText(result, "mock content 2");
        string nextResult = _fileManager.GenerateSafeOutputFilePath(source);
        Assert.Equal(Path.Combine(_testTempDir, "relatorio_compressed_2.pdf"), nextResult);
    }

    [Fact]
    public void GenerateSafeOutputFilePath_RespectsCustomTargetDirectory()
    {
        string customDir = Path.Combine(_testTempDir, "custom_out");
        Directory.CreateDirectory(customDir);
        string source = Path.Combine(_testTempDir, "peticao.pdf");

        string result = _fileManager.GenerateSafeOutputFilePath(source, customDir);
        Assert.Equal(Path.Combine(customDir, "peticao_compressed.pdf"), result);
    }

    [Fact]
    public void CreateIsolatedTempDirectory_CreatesUniqueDirectory()
    {
        string dir1 = _fileManager.CreateIsolatedTempDirectory();
        string dir2 = _fileManager.CreateIsolatedTempDirectory();

        Assert.True(Directory.Exists(dir1));
        Assert.True(Directory.Exists(dir2));
        Assert.NotEqual(dir1, dir2);

        _fileManager.SafeDeleteDirectory(dir1);
        _fileManager.SafeDeleteDirectory(dir2);

        Assert.False(Directory.Exists(dir1));
        Assert.False(Directory.Exists(dir2));
    }

    [Fact]
    public void SafeDeleteDirectory_NonExistentOrNullDirectory_DoesNotThrow()
    {
        _fileManager.SafeDeleteDirectory("");
        _fileManager.SafeDeleteDirectory(Path.Combine(_testTempDir, "non_existent_folder_xyz"));
    }

    [Fact]
    public void SafeDeleteFile_ExistingFile_DeletesSuccessfully()
    {
        string testFile = Path.Combine(_testTempDir, "to_delete.tmp");
        File.WriteAllText(testFile, "test data");
        Assert.True(File.Exists(testFile));

        _fileManager.SafeDeleteFile(testFile);
        Assert.False(File.Exists(testFile));
    }

    [Fact]
    public void SafeDeleteFile_NonExistentOrNull_DoesNotThrow()
    {
        _fileManager.SafeDeleteFile("");
        _fileManager.SafeDeleteFile(Path.Combine(_testTempDir, "non_existent_file.tmp"));
    }
}
