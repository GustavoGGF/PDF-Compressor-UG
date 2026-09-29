namespace PdfCompressor.Models;

/// <summary>
/// Opções configuradas para a rotina de compressão do documento.
/// </summary>
public sealed record CompressionOptions(
    string SourceFilePath,
    string TargetDirectory,
    CompressionPreset Preset,
    long? TargetSizeBytes = null,
    bool OverwriteTarget = false
)
{
    /// <summary>
    /// Valida a coerência dos parâmetros especificados.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceFilePath))
        {
            throw new ArgumentException("O caminho do arquivo de origem não pode ser vazio.", nameof(SourceFilePath));
        }

        if (string.IsNullOrWhiteSpace(TargetDirectory))
        {
            throw new ArgumentException("O diretório de destino não pode ser vazio.", nameof(TargetDirectory));
        }

        if (Preset == CompressionPreset.Automatic)
        {
            if (!TargetSizeBytes.HasValue || TargetSizeBytes.Value <= 0)
            {
                throw new ArgumentException("O tamanho-alvo em bytes é obrigatório e deve ser maior que zero no modo automático.", nameof(TargetSizeBytes));
            }
        }
    }
}
