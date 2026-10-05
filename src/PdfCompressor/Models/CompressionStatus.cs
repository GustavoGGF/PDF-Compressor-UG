namespace PdfCompressor.Models;

/// <summary>
/// Estados de encerramento do processo de compressão.
/// </summary>
public enum CompressionStatus
{
    /// <summary>
    /// Compressão atingiu um tamanho menor ou igual ao alvo solicitado.
    /// </summary>
    TargetMet,

    /// <summary>
    /// Compressão concluída, mas nenhuma tentativa atingiu o alvo; o melhor resultado foi preservado.
    /// </summary>
    BestEffortAboveTarget,

    /// <summary>
    /// Nenhuma tentativa produziu um arquivo estritamente menor que o original.
    /// Nenhum arquivo novo foi gerado.
    /// </summary>
    NoReduction,

    /// <summary>
    /// Operação cancelada explicitamente pelo usuário.
    /// </summary>
    Cancelled,

    /// <summary>
    /// O binário do Ghostscript não foi localizado no sistema.
    /// </summary>
    ToolUnavailable,

    /// <summary>
    /// O arquivo de entrada é inválido, inexistente, protegido por senha ou inacessível.
    /// </summary>
    InvalidInput,

    /// <summary>
    /// Falha irrecuperável do motor Ghostscript durante a execução.
    /// </summary>
    EngineFailed
}
