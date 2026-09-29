using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Registro seguro de diagnósticos sem conteúdo de documentos conforme DEC-10.
/// </summary>
public interface IDiagnosticLogger
{
    /// <summary>
    /// Registra mensagem informativa.
    /// </summary>
    void LogInfo(string message);

    /// <summary>
    /// Registra aviso sobre condições anômalas não fatais.
    /// </summary>
    void LogWarning(string message);

    /// <summary>
    /// Registra erro de execução e detalhes de exceção quando aplicável.
    /// </summary>
    void LogError(string message, Exception? ex = null);

    /// <summary>
    /// Registra o resumo da compressão sem dados sensíveis.
    /// </summary>
    void LogCompressionSummary(CompressionResult result);

    /// <summary>
    /// Registra o resumo da análise preliminar do PDF sem dados sensíveis.
    /// </summary>
    void LogAnalysisSummary(PdfInfo info);

    /// <summary>
    /// Registra operação de limpeza de arquivo ou diretório temporário para auditoria.
    /// </summary>
    /// <param name="targetPath">Caminho do arquivo ou diretório alvo da limpeza.</param>
    /// <param name="succeeded">Indica se a remoção física foi realizada com sucesso.</param>
    /// <param name="details">Detalhes complementares ou motivo do erro quando aplicável.</param>
    void LogCleanup(string targetPath, bool succeeded, string? details = null);
}
