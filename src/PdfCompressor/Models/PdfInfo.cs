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
    string? ErrorMessage = null,
    string? WarningMessage = null
)
{
    /// <summary>
    /// Mensagem padrão exibida quando indicadores de assinatura digital são detectados (DEC-07, TC-06).
    /// </summary>
    public const string DefaultSignatureWarningMessage =
        "⚠ Assinatura detectada: a compressão pode invalidá-la. O arquivo original não será alterado.";

    /// <summary>
    /// Mensagem padrão exibida quando proteção por senha/criptografia é detectada (DEC-06, TC-09).
    /// </summary>
    public const string PasswordProtectedMessage =
        "Este arquivo está protegido por senha. Remova a proteção antes de comprimir.";

    /// <summary>
    /// Tamanho em Megabytes Decimais (1 MB = 1.000.000 bytes) conforme DEC-01.
    /// </summary>
    public double FileSizeMb => FileSizeBytes / 1_000_000.0;

    /// <summary>
    /// Status consolidado da análise (Success, Warning ou Failed).
    /// </summary>
    public PdfAnalysisStatus Status =>
        !IsValid
            ? PdfAnalysisStatus.Failed
            : (HasLikelySignature || !string.IsNullOrWhiteSpace(WarningMessage)
                ? PdfAnalysisStatus.Warning
                : PdfAnalysisStatus.Success);

    /// <summary>
    /// Cria instância de resultado com falha na análise.
    /// </summary>
    public static PdfInfo Failed(string filePath, string errorMessage, long fileSizeBytes = 0, bool isEncrypted = false) =>
        new(filePath, fileSizeBytes, null, false, isEncrypted, false, errorMessage, null);

    /// <summary>
    /// Cria instância de resultado com sucesso pleno na análise.
    /// </summary>
    public static PdfInfo Success(string filePath, long fileSizeBytes, int? pageCount) =>
        new(filePath, fileSizeBytes, pageCount, false, false, true, null, null);

    /// <summary>
    /// Cria instância de resultado com sucesso na validação mas com alerta/aviso (ex.: assinatura digital).
    /// </summary>
    public static PdfInfo Warning(string filePath, long fileSizeBytes, int? pageCount, bool hasLikelySignature, string warningMessage) =>
        new(filePath, fileSizeBytes, pageCount, hasLikelySignature, false, true, null, warningMessage);
}
