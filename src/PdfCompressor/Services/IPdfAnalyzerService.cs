using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Analisa a integridade e metadados básicos do PDF sem modificá-lo.
/// </summary>
public interface IPdfAnalyzerService
{
    /// <summary>
    /// Inspeciona o arquivo PDF e retorna suas informações e diagnóstico preliminar.
    /// </summary>
    Task<PdfInfo> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);
}
