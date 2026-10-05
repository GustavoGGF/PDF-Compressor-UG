namespace PdfCompressor.Models;

/// <summary>
/// Resultado final consolidado da operação de compressão.
/// </summary>
public sealed record CompressionResult(
    CompressionStatus Status,
    string SourceFilePath,
    string? OutputFilePath,
    long OriginalSizeBytes,
    long FinalSizeBytes,
    int? FinalDpi,
    IReadOnlyList<AttemptResult> Attempts,
    TimeSpan TotalDuration,
    string? Message = null
)
{
    /// <summary>
    /// Tamanho original em Megabytes Decimais (1 MB = 1.000.000 bytes) conforme DEC-01.
    /// </summary>
    public double OriginalSizeMb => OriginalSizeBytes / 1_000_000.0;

    /// <summary>
    /// Tamanho final em Megabytes Decimais (1 MB = 1.000.000 bytes) conforme DEC-01.
    /// </summary>
    public double FinalSizeMb => FinalSizeBytes / 1_000_000.0;

    /// <summary>
    /// Taxa percentual de redução calculada. Retorna 0.0 se não houve redução ou se o tamanho original for zero.
    /// </summary>
    public double ReductionPercentage => !string.IsNullOrEmpty(OutputFilePath) && OriginalSizeBytes > 0
        ? Math.Max(0.0, ((OriginalSizeBytes - FinalSizeBytes) / (double)OriginalSizeBytes) * 100.0)
        : 0.0;
}
