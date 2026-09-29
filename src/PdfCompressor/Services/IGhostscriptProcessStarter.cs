namespace PdfCompressor.Services;

/// <summary>
/// Dados brutos resultantes da execução de um processo pelo sistema operacional.
/// </summary>
public sealed record ProcessRunOutput(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration
);

/// <summary>
/// Abstração de baixo nível para inicialização e controle assíncrono de processos do sistema operacional.
/// Garante que chamadas sejam isoladas, sem uso de shell e totalmente canceláveis.
/// </summary>
public interface IGhostscriptProcessStarter
{
    /// <summary>
    /// Inicia o processo especificado com os argumentos estruturados informados,
    /// capturando saída e aguardando finalização com suporte a cancelamento.
    /// </summary>
    Task<ProcessRunOutput> StartAndRunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    );
}
