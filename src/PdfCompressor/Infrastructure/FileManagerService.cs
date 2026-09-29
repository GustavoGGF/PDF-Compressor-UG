using System.IO;
using PdfCompressor.Services;

namespace PdfCompressor.Infrastructure;

/// <summary>
/// Implementação segura para manipulação de arquivos, nomes não conflitantes,
/// isolamento de diretórios temporários e promoção atômica para o destino final.
/// </summary>
public sealed class FileManagerService : IFileManagerService
{
    private const string DefaultSuffix = "_compactado";
    private readonly string _baseTempDirectory;
    private readonly IDiagnosticLogger? _logger;

    public FileManagerService(
        IDiagnosticLogger? logger = null,
        string? customTempDirectory = null)
    {
        _logger = logger;
        _baseTempDirectory = !string.IsNullOrWhiteSpace(customTempDirectory)
            ? customTempDirectory
            : Path.Combine(Path.GetTempPath(), "PdfCompressor", "attempts");
    }

    public FileManagerService(string customTempDirectory)
        : this(null, customTempDirectory)
    {
    }

    /// <inheritdoc />
    public string GenerateSafeOutputFilePath(string sourceFilePath, string? targetDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            throw new ArgumentException("O caminho do arquivo de origem não pode ser nulo ou vazio.", nameof(sourceFilePath));
        }

        string fullSource = Path.GetFullPath(sourceFilePath);
        string directory = !string.IsNullOrWhiteSpace(targetDirectory)
            ? targetDirectory
            : Path.GetDirectoryName(fullSource) ?? Directory.GetCurrentDirectory();

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(sourceFilePath);
        string extension = Path.GetExtension(sourceFilePath);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".pdf";
        }

        string candidate = Path.Combine(directory, $"{fileNameWithoutExtension}{DefaultSuffix}{extension}");
        string fullCandidate = Path.GetFullPath(candidate);

        // Se o arquivo candidato não existir e for estritamente diferente da entrada original, podemos utilizá-lo
        if (!string.Equals(fullCandidate, fullSource, StringComparison.OrdinalIgnoreCase) && !File.Exists(candidate))
        {
            return candidate;
        }

        int counter = 1;
        while (true)
        {
            candidate = Path.Combine(directory, $"{fileNameWithoutExtension}{DefaultSuffix}_{counter}{extension}");
            fullCandidate = Path.GetFullPath(candidate);

            if (!string.Equals(fullCandidate, fullSource, StringComparison.OrdinalIgnoreCase) && !File.Exists(candidate))
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
    public bool SafeDeleteDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return true;
        }

        if (!Directory.Exists(directoryPath))
        {
            return true;
        }

        int maxRetries = 2;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);
                _logger?.LogCleanup(directoryPath, succeeded: true);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    // Pequena pausa para liberação de handles de processo no SO (ex.: Windows)
                    Thread.Sleep(50);
                    continue;
                }

                _logger?.LogWarning($"Falha na limpeza do diretório temporário '{directoryPath}': {ex.Message}");
                _logger?.LogCleanup(directoryPath, succeeded: false, details: ex.Message);
                return false;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool SafeDeleteFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return true;
        }

        if (!File.Exists(filePath))
        {
            return true;
        }

        int maxRetries = 2;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                File.Delete(filePath);
                _logger?.LogCleanup(filePath, succeeded: true);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    Thread.Sleep(50);
                    continue;
                }

                _logger?.LogWarning($"Falha na limpeza do arquivo temporário '{filePath}': {ex.Message}");
                _logger?.LogCleanup(filePath, succeeded: false, details: ex.Message);
                return false;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool TryPromoteFile(string sourceTempPath, string destinationPath, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(sourceTempPath) || !File.Exists(sourceTempPath))
        {
            errorMessage = "O arquivo temporário de origem não foi localizado ou está indisponível.";
            _logger?.LogError($"Falha de promoção: arquivo temporário não encontrado '{sourceTempPath}'.");
            return false;
        }

        var sourceInfo = new FileInfo(sourceTempPath);
        if (sourceInfo.Length == 0)
        {
            errorMessage = "O arquivo intermediário gerado possui 0 bytes e não pode ser promovido para o destino.";
            _logger?.LogError($"Falha de promoção: arquivo temporário com 0 bytes '{sourceTempPath}'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            errorMessage = "O caminho do arquivo de destino não foi informado.";
            return false;
        }

        string fullSource = Path.GetFullPath(sourceTempPath);
        string fullDestination = Path.GetFullPath(destinationPath);
        if (string.Equals(fullSource, fullDestination, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "O caminho de destino não pode coincidir com o arquivo intermediário.";
            return false;
        }

        string? destinationDirectory = Path.GetDirectoryName(fullDestination);
        string stagingPath = string.Empty;

        try
        {
            if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
            {
                Directory.CreateDirectory(destinationDirectory);
            }

            // Arquivo temporário de staging no mesmo volume para permitir movimentação atômica
            string stagingFileName = $".{Path.GetFileName(fullDestination)}.{Guid.NewGuid():N}.tmp";
            stagingPath = Path.Combine(destinationDirectory ?? Directory.GetCurrentDirectory(), stagingFileName);

            File.Copy(fullSource, stagingPath, overwrite: true);

            // Operação atômica de renomeação / substituição no sistema de arquivos
            File.Move(stagingPath, fullDestination, overwrite: true);

            errorMessage = null;
            _logger?.LogInfo($"Arquivo promovido com sucesso para o destino final: '{Path.GetFileName(fullDestination)}'.");
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            SafeDeleteFile(stagingPath);
            errorMessage = "Acesso negado ao diretório ou arquivo de destino. Verifique se você possui permissões de gravação na pasta.";
            _logger?.LogError($"Permissão negada ao promover arquivo para '{fullDestination}'.", ex);
            return false;
        }
        catch (IOException ex)
        {
            SafeDeleteFile(stagingPath);

            // Código HResult 0x80070020 (ERROR_SHARING_VIOLATION) ou 0x80070021 (ERROR_LOCK_VIOLATION)
            int hResult = ex.HResult & 0xFFFF;
            if (hResult == 32 || hResult == 33 || ex.Message.Contains("lock", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("used by another process", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "O arquivo de destino está aberto em outro programa ou bloqueado. Feche o leitor de PDF e tente novamente.";
            }
            else
            {
                errorMessage = $"Falha de gravação no arquivo de destino: {ex.Message}";
            }

            _logger?.LogError($"Erro de E/S ao promover arquivo para '{fullDestination}'.", ex);
            return false;
        }
        catch (Exception ex)
        {
            SafeDeleteFile(stagingPath);
            errorMessage = $"Falha ao gravar arquivo no destino final: {ex.Message}";
            _logger?.LogError($"Falha inesperada ao promover arquivo para '{fullDestination}'.", ex);
            return false;
        }
    }
}
