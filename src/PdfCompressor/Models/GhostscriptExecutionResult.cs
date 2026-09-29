namespace PdfCompressor.Models;

/// <summary>
/// Resultado da execução do processo do Ghostscript com classificação de status.
/// </summary>
public sealed record GhostscriptExecutionResult(
    bool Success,
    int ExitCode,
    TimeSpan ExecutionTime,
    string StandardOutput,
    string StandardError,
    GhostscriptFailureReason FailureReason = GhostscriptFailureReason.None,
    string? ErrorMessage = null
);
