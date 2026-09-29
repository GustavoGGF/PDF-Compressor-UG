namespace PdfCompressor.Models;

/// <summary>
/// Resultado detalhado de uma tentativa de compressão com determinado DPI.
/// </summary>
public sealed record AttemptResult(
    int AttemptIndex,
    int Dpi,
    long OutputSizeBytes,
    TimeSpan Duration,
    bool Succeeded,
    string? ErrorDetails = null
)
{
    /// <summary>
    /// Tamanho de saída em Megabytes Decimais (1 MB = 1.000.000 bytes) conforme DEC-01.
    /// </summary>
    public double OutputSizeMb => OutputSizeBytes / 1_000_000.0;
}
