namespace PdfCompressor.Models;

/// <summary>
/// Classificação detalhada das causas de falha na execução do Ghostscript.
/// </summary>
public enum GhostscriptFailureReason
{
    /// <summary>
    /// Execução bem-sucedida, sem falhas.
    /// </summary>
    None = 0,

    /// <summary>
    /// O executável do Ghostscript não foi encontrado no caminho especificado ou no sistema.
    /// </summary>
    ExecutableNotFound,

    /// <summary>
    /// Falha ao iniciar o processo do sistema operacional.
    /// </summary>
    StartFailure,

    /// <summary>
    /// O processo foi executado, mas retornou um código de saída diferente de zero.
    /// </summary>
    NonZeroExitCode,

    /// <summary>
    /// O processo encerrou com código 0, mas o arquivo de saída esperado não foi criado.
    /// </summary>
    OutputMissing,

    /// <summary>
    /// O arquivo de saída foi gerado, mas possui 0 bytes ou cabeçalho PDF corrompido/inválido.
    /// </summary>
    InvalidOutput,

    /// <summary>
    /// Violação de permissão de acesso a arquivos ou falha de I/O em disco.
    /// </summary>
    PermissionOrDiskDenied,

    /// <summary>
    /// A execução foi cancelada explicitamente pelo usuário via CancellationToken.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Parâmetros de entrada inválidos (ex.: entrada inexistente, input igual a output).
    /// </summary>
    InvalidInput
}
