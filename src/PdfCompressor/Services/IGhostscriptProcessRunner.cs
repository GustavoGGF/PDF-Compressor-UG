using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Executa o processo gswin64c.exe de forma isolada, não interativa e cancelável.
/// </summary>
public interface IGhostscriptProcessRunner
{
    /// <summary>
    /// Executa o Ghostscript com os parâmetros estruturados fornecidos e suporte a cancelamento imediato.
    /// </summary>
    Task<GhostscriptExecutionResult> RunAsync(
        GhostscriptExecutionParams parameters,
        CancellationToken cancellationToken
    );
}
