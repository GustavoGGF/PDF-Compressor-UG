namespace PdfCompressor.Services;

/// <summary>
/// Gerencia arquivos, resolução de nomes não conflitantes, diretórios temporários e promoção segura de saída.
/// </summary>
public interface IFileManagerService
{
    /// <summary>
    /// Gera um caminho de arquivo de saída não conflitante (ex.: _compactado.pdf, _compactado_1.pdf).
    /// Garante que o caminho gerado nunca colida com o arquivo de origem nem sobrescreva arquivos existentes.
    /// </summary>
    /// <param name="sourceFilePath">Caminho completo do arquivo de entrada original.</param>
    /// <param name="targetDirectory">Diretório de destino pretendido (se nulo, usa a pasta do arquivo de origem).</param>
    /// <returns>Caminho absoluto de saída seguro contra colisões.</returns>
    string GenerateSafeOutputFilePath(string sourceFilePath, string? targetDirectory = null);

    /// <summary>
    /// Cria um diretório temporário isolado e exclusivo para a tentativa ou sessão de compressão.
    /// </summary>
    /// <returns>Caminho absoluto do diretório temporário criado.</returns>
    string CreateIsolatedTempDirectory();

    /// <summary>
    /// Remove de forma segura o diretório e seus arquivos, sem propagar exceções bloqueantes para o chamador.
    /// Registra advertência de diagnóstico caso a remoção não possa ser concluída.
    /// </summary>
    /// <param name="directoryPath">Caminho do diretório temporário a ser excluído.</param>
    /// <returns>True se o diretório foi excluído com êxito ou não existia; false caso contrário.</returns>
    bool SafeDeleteDirectory(string directoryPath);

    /// <summary>
    /// Remove de forma segura um arquivo temporário, sem propagar exceções bloqueantes para o chamador.
    /// Registra advertência de diagnóstico caso a remoção não possa ser concluída.
    /// </summary>
    /// <param name="filePath">Caminho do arquivo temporário a ser excluído.</param>
    /// <returns>True se o arquivo foi excluído com êxito ou não existia; false caso contrário.</returns>
    bool SafeDeleteFile(string filePath);

    /// <summary>
    /// Promove de forma segura e atômica um arquivo temporário candidato para o caminho final de destino.
    /// Utiliza arquivo intermediário de staging para evitar arquivos parciais ou corrompidos em caso de falha de gravação ou bloqueio.
    /// </summary>
    /// <param name="sourceTempPath">Caminho do arquivo de origem que será copiado de forma segura; normalmente um intermediário ou o PDF original preservado.</param>
    /// <param name="destinationPath">Caminho absoluto do arquivo final de destino.</param>
    /// <param name="errorMessage">Mensagem de erro amigável e orientada à ação caso a promoção falhe.</param>
    /// <returns>True se a promoção foi concluída com sucesso; false caso contrário.</returns>
    bool TryPromoteFile(string sourceTempPath, string destinationPath, out string? errorMessage);
}
