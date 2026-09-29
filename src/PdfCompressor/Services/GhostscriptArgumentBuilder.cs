using System.Globalization;
using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Constrói a lista estruturada de argumentos para invocação segura do Ghostscript sem interpolação de shell.
/// Garante isolamento estrito conforme diretrizes de segurança (DEC-05 e DEC-08).
/// </summary>
public static class GhostscriptArgumentBuilder
{
    /// <summary>
    /// Gera a lista de argumentos para a invocação do binário do Ghostscript.
    /// </summary>
    public static IReadOnlyList<string> BuildArguments(GhostscriptExecutionParams parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        if (string.IsNullOrWhiteSpace(parameters.ExecutablePath))
        {
            throw new ArgumentException("O caminho do executável do Ghostscript não pode ser vazio.", nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(parameters.InputPdfPath))
        {
            throw new ArgumentException("O caminho do arquivo de entrada não pode ser vazio.", nameof(parameters));
        }

        if (string.IsNullOrWhiteSpace(parameters.OutputPdfPath))
        {
            throw new ArgumentException("O caminho do arquivo de saída não pode ser vazio.", nameof(parameters));
        }

        if (parameters.Dpi <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parameters), parameters.Dpi, "O valor de DPI deve ser estritamente positivo.");
        }

        var fullInput = Path.GetFullPath(parameters.InputPdfPath);
        var fullOutput = Path.GetFullPath(parameters.OutputPdfPath);

        if (string.Equals(fullInput, fullOutput, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("O arquivo de entrada não pode ser o mesmo de saída para evitar perda ou corrupção de dados.");
        }

        var compatibilityString = FormatCompatibilityLevel(parameters.CompatibilityLevel);

        return
        [
            "-dNOPAUSE",
            "-dBATCH",
            "-dQUIET",
            "-sDEVICE=pdfwrite",
            $"-dCompatibilityLevel={compatibilityString}",
            "-dDownsampleColorImages=true",
            $"-dColorImageResolution={parameters.Dpi}",
            "-dDownsampleGrayImages=true",
            $"-dGrayImageResolution={parameters.Dpi}",
            "-dDownsampleMonoImages=true",
            $"-dMonoImageResolution={parameters.Dpi}",
            "-dColorImageDownsampleType=/Bicubic",
            "-dGrayImageDownsampleType=/Bicubic",
            "-dMonoImageDownsampleType=/Bicubic",
            "-dAutoRotatePages=/None",
            $"-sOutputFile={parameters.OutputPdfPath}",
            parameters.InputPdfPath
        ];
    }

    private static string FormatCompatibilityLevel(int level)
    {
        // Aceita representações inteiras como 14 -> "1.4", 17 -> "1.7"
        if (level is >= 10 and <= 20)
        {
            var decimalVal = level / 10.0;
            return decimalVal.ToString("0.0", CultureInfo.InvariantCulture);
        }

        return "1.7";
    }
}
