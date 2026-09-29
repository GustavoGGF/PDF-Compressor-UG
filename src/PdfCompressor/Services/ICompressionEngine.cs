using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Motor de compressão responsável pela orquestração das tentativas e seleção da qualidade.
/// </summary>
public interface ICompressionEngine
{
    /// <summary>
    /// Executa o fluxo de compressão conforme as opções configuradas, notificando o progresso e respeitando cancelamento.
    /// </summary>
    Task<CompressionResult> CompressAsync(
        CompressionOptions options,
        IProgress<CompressionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default
    );
}
