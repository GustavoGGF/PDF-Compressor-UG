using System.Globalization;
using PdfCompressor.Models;

namespace PdfCompressor.UI;

/// <summary>
/// Validador desacoplado para a entrada de tamanho-alvo de compressão.
/// Suporta separadores decimais de vírgula e ponto de forma invariante de cultura.
/// </summary>
public static class TargetSizeValidator
{
    /// <summary>
    /// Mensagem exibida quando o campo está vazio ou contém apenas espaços em branco no modo automático.
    /// </summary>
    public const string TargetRequiredMessage = "O tamanho-alvo é obrigatório no modo automático.";

    /// <summary>
    /// Mensagem exibida quando o texto digitado não representa um número válido.
    /// </summary>
    public const string InvalidNumberMessage = "Informe um valor numérico válido para o tamanho-alvo.";

    /// <summary>
    /// Mensagem exibida quando o valor informado é zero ou negativo.
    /// </summary>
    public const string MustBeGreaterThanZeroMessage = "O tamanho-alvo deve ser maior que zero.";

    /// <summary>
    /// Mensagem exibida quando a conversão para bytes resulta em zero bytes.
    /// </summary>
    public const string ResultingBytesZeroMessage = "O tamanho-alvo resultante em bytes deve ser maior que zero.";

    /// <summary>
    /// Valida o texto digitado para o tamanho-alvo considerando o preset selecionado e a unidade.
    /// </summary>
    /// <param name="preset">Predefinição de compressão selecionada.</param>
    /// <param name="input">Texto informado pelo usuário no campo de tamanho-alvo.</param>
    /// <param name="unit">Unidade selecionada (MB ou KB).</param>
    /// <param name="targetSizeBytes">Bytes calculados quando válido; null caso contrário ou em presets manuais.</param>
    /// <param name="errorMessage">Mensagem de erro amigável ao usuário quando inválido; null quando válido.</param>
    /// <returns>True se a entrada for válida para o preset selecionado; false caso contrário.</returns>
    public static bool TryValidate(
        CompressionPreset preset,
        string? input,
        TargetSizeUnit unit,
        out long? targetSizeBytes,
        out string? errorMessage)
    {
        targetSizeBytes = null;
        errorMessage = null;

        // Se o preset não for automático, o tamanho-alvo não é obrigatório nem utilizado
        if (preset != CompressionPreset.Automatic)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(input))
        {
            errorMessage = TargetRequiredMessage;
            return false;
        }

        // Normalização de separador decimal: aceita vírgula ou ponto independentemente da cultura do sistema
        string normalized = input.Trim().Replace(',', '.');

        // Impede múltiplos pontos/vírgulas
        if (normalized.Count(c => c == '.') > 1)
        {
            errorMessage = InvalidNumberMessage;
            return false;
        }

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedValue) ||
            double.IsNaN(parsedValue) || double.IsInfinity(parsedValue))
        {
            errorMessage = InvalidNumberMessage;
            return false;
        }

        if (parsedValue <= 0.0)
        {
            errorMessage = MustBeGreaterThanZeroMessage;
            return false;
        }

        long bytes = TargetSizeUnitExtensions.ToBytes(parsedValue, unit);
        if (bytes <= 0)
        {
            errorMessage = ResultingBytesZeroMessage;
            return false;
        }

        targetSizeBytes = bytes;
        return true;
    }
}
