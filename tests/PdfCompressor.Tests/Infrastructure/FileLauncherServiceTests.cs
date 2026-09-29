using System.Diagnostics;
using PdfCompressor.Infrastructure;
using PdfCompressor.Tests.Services;
using Xunit;

namespace PdfCompressor.Tests.Infrastructure;

public sealed class FileLauncherServiceTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly MemoryLogger _logger;
    private readonly List<ProcessStartInfo> _launchedProcesses = [];

    public FileLauncherServiceTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "PdfCompressor_LauncherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
        _logger = new MemoryLogger();
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
            // Limpeza de testes
        }
    }

    private FileLauncherService CreateService(Action<ProcessStartInfo>? customStarter = null)
    {
        return new FileLauncherService(
            _logger,
            customStarter ?? (psi => _launchedProcesses.Add(psi))
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenPdf_NullOrWhitespace_ReturnsFalseWithoutLaunching(string? invalidPath)
    {
        var service = CreateService();

        bool result = service.OpenPdf(invalidPath!);

        Assert.False(result);
        Assert.Empty(_launchedProcesses);
    }

    [Fact]
    public void OpenPdf_NonExistentFile_ReturnsFalseWithoutLaunching()
    {
        var service = CreateService();
        string nonExistent = Path.Combine(_testTempDir, "missing.pdf");

        bool result = service.OpenPdf(nonExistent);

        Assert.False(result);
        Assert.Empty(_launchedProcesses);
        Assert.Contains(_logger.Entries, e => e.Contains("inexistente"));
    }

    [Fact]
    public void OpenPdf_NonPdfExtension_ReturnsFalseWithoutLaunching()
    {
        var service = CreateService();
        string invalidFile = Path.Combine(_testTempDir, "script.bat");
        File.WriteAllText(invalidFile, "echo malicious");

        bool result = service.OpenPdf(invalidFile);

        Assert.False(result);
        Assert.Empty(_launchedProcesses);
        Assert.Contains(_logger.Entries, e => e.Contains("Extensão inválida"));
    }

    [Fact]
    public void OpenPdf_ValidExistingPdf_LaunchesProcessAndReturnsTrue()
    {
        var service = CreateService();
        string validPdf = Path.Combine(_testTempDir, "documento.pdf");
        File.WriteAllText(validPdf, "%PDF-1.7 mock");

        bool result = service.OpenPdf(validPdf);

        Assert.True(result);
        Assert.Single(_launchedProcesses);

        var psi = _launchedProcesses[0];
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(validPdf, psi.FileName);
            Assert.True(psi.UseShellExecute);
        }
        else
        {
            Assert.Equal("xdg-open", psi.FileName);
            Assert.Contains(validPdf, psi.ArgumentList);
        }
    }

    [Fact]
    public void OpenPdf_WhenLauncherThrowsException_CatchesAndReturnsFalse()
    {
        var service = CreateService(psi => throw new InvalidOperationException("Process launch error"));
        string validPdf = Path.Combine(_testTempDir, "arquivo.pdf");
        File.WriteAllText(validPdf, "%PDF-1.7 mock");

        bool result = service.OpenPdf(validPdf);

        Assert.False(result);
        Assert.Contains(_logger.Entries, e => e.Contains("Falha ao iniciar"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenFolderContainingFile_NullOrWhitespace_ReturnsFalseWithoutLaunching(string? invalidPath)
    {
        var service = CreateService();

        bool result = service.OpenFolderContainingFile(invalidPath!);

        Assert.False(result);
        Assert.Empty(_launchedProcesses);
    }

    [Fact]
    public void OpenFolderContainingFile_NonExistentPath_ReturnsFalseWithoutLaunching()
    {
        var service = CreateService();
        string nonExistent = Path.Combine(_testTempDir, "inexistente", "arquivo.pdf");

        bool result = service.OpenFolderContainingFile(nonExistent);

        Assert.False(result);
        Assert.Empty(_launchedProcesses);
    }

    [Fact]
    public void OpenFolderContainingFile_ExistingFile_LaunchesExplorerOrFileManager()
    {
        var service = CreateService();
        string validFile = Path.Combine(_testTempDir, "relatorio.pdf");
        File.WriteAllText(validFile, "pdf content");

        bool result = service.OpenFolderContainingFile(validFile);

        Assert.True(result);
        Assert.Single(_launchedProcesses);

        var psi = _launchedProcesses[0];
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("explorer.exe", psi.FileName);
            Assert.Contains($"/select,{Path.GetFullPath(validFile)}", psi.ArgumentList);
        }
        else
        {
            Assert.Equal("xdg-open", psi.FileName);
            Assert.Contains(_testTempDir, psi.ArgumentList);
        }
    }

    [Fact]
    public void OpenFolderContainingFile_ExistingDirectory_LaunchesExplorerOrFileManager()
    {
        var service = CreateService();
        string subDir = Path.Combine(_testTempDir, "subpasta");
        Directory.CreateDirectory(subDir);

        bool result = service.OpenFolderContainingFile(subDir);

        Assert.True(result);
        Assert.Single(_launchedProcesses);

        var psi = _launchedProcesses[0];
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("explorer.exe", psi.FileName);
            Assert.Contains(Path.GetFullPath(subDir), psi.ArgumentList);
        }
        else
        {
            Assert.Equal("xdg-open", psi.FileName);
            Assert.Contains(subDir, psi.ArgumentList);
        }
    }
}
