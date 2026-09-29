using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Define as políticas de qualidade, mapeamento de presets e sequências determinísticas de DPI
/// utilizadas pelo motor de compressão conforme DEC-04 e DEC-05.
/// </summary>
public static class CompressionPresetPolicy
{
    private static readonly int[] AutomaticDpiSequence = [300, 250, 200, 150, 100, 72];
    private static readonly int[] HighQualityDpiSequence = [300];
    private static readonly int[] MediumQualityDpiSequence = [150];
    private static readonly int[] StrongCompressionDpiSequence = [72];

    /// <summary>
    /// Retorna a sequência determinística de DPIs associada ao preset informado.
    /// </summary>
    public static int[] GetDpiSequence(CompressionPreset preset) => preset switch
    {
        CompressionPreset.Automatic => AutomaticDpiSequence,
        CompressionPreset.HighQuality => HighQualityDpiSequence,
        CompressionPreset.MediumQuality => MediumQualityDpiSequence,
        CompressionPreset.StrongCompression => StrongCompressionDpiSequence,
        _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Preset de compressão inválido ou não suportado.")
    };

    /// <summary>
    /// Avalia se o tamanho do candidato é elegível conforme a política de tamanho-alvo.
    /// Se nenhum alvo for especificado (presets manuais diretos), qualquer candidato válido é elegível.
    /// </summary>
    public static bool IsEligible(long candidateSizeBytes, long? targetSizeBytes)
    {
        return !targetSizeBytes.HasValue || candidateSizeBytes <= targetSizeBytes.Value;
    }
}
