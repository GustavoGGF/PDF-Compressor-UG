using System.Diagnostics;
using PdfCompressor.Services;

namespace PdfCompressor.Infrastructure;

/// <summary>
/// Implementação segura para abertura de arquivos PDF e pastas no sistema operacional.
/// Valida a existência prévia e protege contra execução de artefatos inadequados ou caminhos inexistentes.
/// </summary>
public sealed class FileLauncherService : IFileLauncherService
{
    private readonly IDiagnosticLogger? _logger;
    private readonly Action<ProcessStartInfo> _processStarter;

    public FileLauncherService(
        IDiagnosticLogger? logger = null,
        Action<ProcessStartInfo>? customProcessStarter = null)
    {
        _logger = logger;
        _processStarter = customProcessStarter ?? (psi => Process.Start(psi));
    }

    /// <inheritdoc />
    public bool OpenPdf(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger?.LogWarning("Tentativa de abrir arquivo PDF com caminho nulo ou vazio.");
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning($"Tentativa de abrir PDF inexistente: '{filePath}'.");
            return false;
        }

        string extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning($"Extensão inválida rejeitada para abertura de PDF: '{filePath}'.");
            return false;
        }

        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                };
            }
            else
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(filePath);
            }

            _processStarter(startInfo);
            _logger?.LogInfo($"Arquivo PDF aberto no visualizador padrão: '{Path.GetFileName(filePath)}'.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Falha ao iniciar visualizador para o arquivo '{Path.GetFileName(filePath)}'.", ex);
            return false;
        }
    }

    /// <inheritdoc />
    public bool OpenFolderContainingFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger?.LogWarning("Tentativa de abrir pasta com caminho nulo ou vazio.");
            return false;
        }

        bool isFile = File.Exists(filePath);
        bool isDir = Directory.Exists(filePath);

        if (!isFile && !isDir)
        {
            _logger?.LogWarning($"Tentativa de abrir pasta para caminho inexistente: '{filePath}'.");
            return false;
        }

        try
        {
            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = false
                };

                if (isFile)
                {
                    // Destaca o arquivo dentro do Windows Explorer
                    startInfo.ArgumentList.Add($"/select,{Path.GetFullPath(filePath)}");
                }
                else
                {
                    startInfo.ArgumentList.Add(Path.GetFullPath(filePath));
                }
            }
            else
            {
                string targetDir = isFile
                    ? Path.GetDirectoryName(filePath) ?? filePath
                    : filePath;

                startInfo = new ProcessStartInfo
                {
                    FileName = "xdg-open",
                    UseShellExecute = false
                };
                startInfo.ArgumentList.Add(targetDir);
            }

            _processStarter(startInfo);
            _logger?.LogInfo($"Pasta contendo o arquivo aberta no explorador: '{Path.GetFileName(filePath)}'.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Falha ao abrir pasta no explorador para '{Path.GetFileName(filePath)}'.", ex);
            return false;
        }
    }
}
