namespace PdfCompressor.Models;

/// <summary>
/// Predefinições de compressão selecionadas pelo usuário ou automáticas.
/// </summary>
public enum CompressionPreset
{
    /// <summary>
    /// Busca descendente estrita entre DPIs (300 -> 250 -> 200 -> 150 -> 100 -> 72 DPI).
    /// </summary>
    Automatic,

    /// <summary>
    /// 300 DPI - Alta qualidade de impressão.
    /// </summary>
    HighQuality,

    /// <summary>
    /// 150 DPI - Qualidade intermediária para leitura em telas e arquivamento.
    /// </summary>
    MediumQuality,

    /// <summary>
    /// 72 DPI - Compressão agressiva para redução máxima de tamanho.
    /// </summary>
    StrongCompression
}
