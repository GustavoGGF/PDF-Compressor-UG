namespace PdfCompressor.Models;

/// <summary>
/// Notificação de progresso emitida durante a execução da compressão.
/// </summary>
public sealed record CompressionProgressUpdate(
    int CurrentAttempt,
    int TotalAttempts,
    int DpiTested,
    string StepDescription,
    int PercentageEstimate
);
