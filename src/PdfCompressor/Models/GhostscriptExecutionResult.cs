namespace PdfCompressor.Models;

/// <summary>
/// Resultado da execução do processo do Ghostscript.
/// </summary>
public sealed record GhostscriptExecutionResult(
    bool Success,
    int ExitCode,
    TimeSpan ExecutionTime,
    string StandardOutput,
    string StandardError
);
