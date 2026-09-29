namespace PdfCompressor.Models;

/// <summary>
/// Parâmetros de baixo nível para invocação do processo Ghostscript.
/// </summary>
public sealed record GhostscriptExecutionParams(
    string ExecutablePath,
    string InputPdfPath,
    string OutputPdfPath,
    int Dpi,
    int CompatibilityLevel = 17
);
