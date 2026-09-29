using PdfCompressor.Services;

namespace PdfCompressor.Infrastructure;

/// <summary>
/// Implementação segura para manipulação de arquivos, nomes não conflitantes e diretórios temporários.
/// </summary>
public sealed class FileManagerService : IFileManagerService
{
    private readonly string _baseTempDirectory;

    public FileManagerService(string? customTempDirectory = null)
    {
        _baseTempDirectory = !string.IsNullOrWhiteSpace(customTempDirectory)
            ? customTempDirectory
            : Path.Combine(Path.GetTempPath(), "PdfCompressor", "attempts");
    }

    /// <inheritdoc />
    public string GenerateSafeOutputFilePath(string sourceFilePath, string? targetDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("O caminho do arquivo de origem não pode ser nulo ou vazio.", nameof(sourceFilePath));
        }

        string directory = !string.IsNullOrWhiteSpace(targetDirectory)
            ? targetDirectory
            : Path.GetDirectoryName(sourceFilePath) ?? Directory.GetCurrentDirectory();

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFilePath);
        string extension = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".pdf";
        }

        string candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_compressed{extension}");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        int counter = 1;
        while (true)
        {
            candidate = Path.Combine(directory, $"{fileNameWithoutExtension}_compressed_{counter}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
            counter++;
        }
    }

    /// <inheritdoc />
    public string CreateIsolatedTempDirectory()
    {
        string uniqueId = Guid.NewGuid().ToString("N");
        string attemptDirectory = Path.Combine(_baseTempDirectory, uniqueId);
        Directory.CreateDirectory(attemptDirectory);
        return attemptDirectory;
    }

    /// <inheritdoc />
    public void SafeDeleteDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Limpeza de melhor esforço: não propaga exceção bloqueante
        }
    }

    /// <inheritdoc />
    public void SafeDeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Limpeza de melhor esforço: não propaga exceção bloqueante
        }
    }
}
