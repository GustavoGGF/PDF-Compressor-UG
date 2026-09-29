namespace PdfCompressor.Models;

/// <summary>
/// Classificação de alto nível do resultado da análise do PDF.
/// </summary>
public enum PdfAnalysisStatus
{
    /// <summary>
    /// Arquivo válido e pronto para operações sem avisos impeditivos.
    /// </summary>
    Success,

    /// <summary>
    /// Arquivo válido para compressão, mas contém alertas importantes (ex.: assinatura digital).
    /// </summary>
    Warning,

    /// <summary>
    /// Arquivo inválido, protegido, corrompido, bloqueado ou inacessível.
    /// </summary>
    Failed
}
