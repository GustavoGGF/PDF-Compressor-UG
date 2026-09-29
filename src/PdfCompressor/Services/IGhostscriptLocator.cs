namespace PdfCompressor.Services;

/// <summary>
/// Localiza a instalação do Ghostscript no sistema operacional.
/// </summary>
public interface IGhostscriptLocator
{
    /// <summary>
    /// Verifica se o binário gswin64c.exe foi encontrado em algum dos caminhos conhecidos ou no PATH.
    /// </summary>
    bool IsAvailable();

    /// <summary>
    /// Localiza o caminho completo do executável, permitindo um caminho customizado prioritário.
    /// </summary>
    string? FindExecutablePath(string? customPath = null);

    /// <summary>
    /// Obtém a versão instalada a partir do executável especificado.
    /// </summary>
    string? GetInstalledVersion(string executablePath);
}
