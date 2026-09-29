namespace PdfCompressor.Models;

/// <summary>
/// Metadados e diagnóstico preliminar do arquivo PDF analisado.
/// </summary>
public sealed record PdfInfo(
    string FilePath,
    long FileSizeBytes,
    int? PageCount,
    bool HasLikelySignature,
    bool IsEncrypted,
    bool IsValid,
    string? ErrorMessage = null
)
{
    /// <summary>
    /// Tamanho em Megabytes Decimais (1 MB = 1.000.000 bytes) conforme DEC-01.
    /// </summary>
    public double FileSizeMb => FileSizeBytes / 1_000_000.0;
}
