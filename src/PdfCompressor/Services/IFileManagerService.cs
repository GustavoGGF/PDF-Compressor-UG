namespace PdfCompressor.Services;

/// <summary>
/// Gerencia arquivos, resolução de nomes não conflitantes e diretórios temporários.
/// </summary>
public interface IFileManagerService
{
    /// <summary>
    /// Gera um caminho de arquivo de saída não conflitante (ex.: _compressed, _compressed_1).
    /// </summary>
    string GenerateSafeOutputFilePath(string sourceFilePath, string? targetDirectory = null);

    /// <summary>
    /// Cria um diretório temporário isolado e exclusivo para a tentativa de compressão.
    /// </summary>
    string CreateIsolatedTempDirectory();

    /// <summary>
    /// Remove de forma segura o diretório e seus arquivos, sem propagar exceções bloqueantes.
    /// </summary>
    void SafeDeleteDirectory(string directoryPath);

    /// <summary>
    /// Remove de forma segura um arquivo temporário, sem propagar exceções bloqueantes.
    /// </summary>
    void SafeDeleteFile(string filePath);
}
