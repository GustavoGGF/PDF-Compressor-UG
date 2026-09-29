using System.Globalization;

namespace PdfCompressor.UI;

/// <summary>
/// Unidade de medida selecionável para especificação do tamanho-alvo de compressão.
/// Adota o padrão decimal do SI conforme DEC-01 (1 MB = 1.000.000 bytes; 1 KB = 1.000 bytes).
/// </summary>
public enum TargetSizeUnit
{
    /// <summary>
    /// Megabytes decimais (1 MB = 1.000.000 bytes).
    /// </summary>
    MB,

    /// <summary>
    /// Kilobytes decimais (1 KB = 1.000 bytes).
    /// </summary>
    KB
}

/// <summary>
/// Métodos auxiliares para conversão e formatação com <see cref="TargetSizeUnit"/>.
/// </summary>
public static class TargetSizeUnitExtensions
{
    /// <summary>
    /// Multiplicador decimal em bytes para a unidade especificada.
    /// </summary>
    public static long GetBytesMultiplier(this TargetSizeUnit unit) =>
        unit switch
        {
            TargetSizeUnit.MB => 1_000_000L,
            TargetSizeUnit.KB => 1_000L,
            _ => 1_000_000L
        };

    /// <summary>
    /// Converte um valor numérico na unidade informada para bytes inteiros.
    /// </summary>
    public static long ToBytes(double value, TargetSizeUnit unit)
    {
        if (value <= 0)
        {
            return 0;
        }

        long multiplier = unit.GetBytesMultiplier();
        return (long)Math.Round(value * multiplier);
    }

    /// <summary>
    /// Formata uma quantidade de bytes em representação decimal legível em MB ou KB.
    /// </summary>
    public static string FormatBytes(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.GetCultureInfo("pt-BR");

        if (bytes >= 1_000_000L)
        {
            double mb = bytes / 1_000_000.0;
            return $"{mb.ToString("0.00", culture)} MB";
        }

        if (bytes >= 1_000L)
        {
            double kb = bytes / 1_000.0;
            return $"{kb.ToString("0.00", culture)} KB";
        }

        return $"{bytes} B";
    }
}
