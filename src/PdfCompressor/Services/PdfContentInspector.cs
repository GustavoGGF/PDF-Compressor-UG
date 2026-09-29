using System.Text;
using System.Text.RegularExpressions;

namespace PdfCompressor.Services;

/// <summary>
/// Resultado da inspeção de baixo nível da estrutura do PDF.
/// </summary>
internal sealed record PdfInspectionResult(
    bool HasValidHeader,
    bool HasValidTrailer,
    bool IsEncrypted,
    bool HasLikelySignature,
    int? PageCount,
    string? FailureReason = null
);

/// <summary>
/// Inspeciona estruturas de baixo nível de PDFs (cabeçalho, trailer, criptografia, assinaturas e páginas)
/// sem alterar o stream e sem dependências pesadas de terceiros.
/// </summary>
internal static partial class PdfContentInspector
{
    private const int HeaderBufferSize = 1024;
    private const int TrailerBufferSize = 4096;
    private const int ChunkBufferSize = 64 * 1024; // 64 KB
    private const int OverlapSize = 1024; // 1 KB para evitar corte de tokens

    // Heurísticas compiladas e rápidas
    [GeneratedRegex(@"%PDF-[0-9]\.[0-9]", RegexOptions.Compiled)]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"%%EOF", RegexOptions.Compiled)]
    private static partial Regex EofRegex();

    [GeneratedRegex(@"/Encrypt\b", RegexOptions.Compiled)]
    private static partial Regex EncryptRegex();

    [GeneratedRegex(@"/ByteRange\b", RegexOptions.Compiled)]
    private static partial Regex ByteRangeRegex();

    [GeneratedRegex(@"/(?:Type|FT)\s*/Sig\b", RegexOptions.Compiled)]
    private static partial Regex SigTypeRegex();

    [GeneratedRegex(@"/Contents\s*<[0-9a-fA-F\s]+>", RegexOptions.Compiled)]
    private static partial Regex HexContentsRegex();

    [GeneratedRegex(@"<<[^>]*?/Type\s*/Pages\b[^>]*?/Count\s+(?<count>\d+)[^>]*?>>", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex PagesDictWithCountRegex();

    [GeneratedRegex(@"<<[^>]*?/Count\s+(?<count>\d+)[^>]*?/Type\s*/Pages\b[^>]*?>>", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex CountBeforePagesDictRegex();

    /// <summary>
    /// Inspeciona o stream fornecido e extrai os indicadores estruturais do PDF.
    /// </summary>
    public static async Task<PdfInspectionResult> InspectAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanSeek || !stream.CanRead)
        {
            return new PdfInspectionResult(
                HasValidHeader: false,
                HasValidTrailer: false,
                IsEncrypted: false,
                HasLikelySignature: false,
                PageCount: null,
                FailureReason: "O stream do arquivo não permite leitura ou posicionamento."
            );
        }

        long length = stream.Length;
        if (length < 10)
        {
            return new PdfInspectionResult(
                HasValidHeader: false,
                HasValidTrailer: false,
                IsEncrypted: false,
                HasLikelySignature: false,
                PageCount: null,
                FailureReason: "O arquivo é excessivamente curto para ser um PDF válido."
            );
        }

        // 1. Validação do Cabeçalho (%PDF-1.x ou %PDF-2.x nos primeiros 1024 bytes)
        stream.Seek(0, SeekOrigin.Begin);
        int headerBytesToRead = (int)Math.Min(length, HeaderBufferSize);
        byte[] headerBuffer = new byte[headerBytesToRead];
        await stream.ReadExactlyAsync(headerBuffer, 0, headerBytesToRead, cancellationToken);
        string headerText = Encoding.ASCII.GetString(headerBuffer);

        if (!HeaderRegex().IsMatch(headerText))
        {
            return new PdfInspectionResult(
                HasValidHeader: false,
                HasValidTrailer: false,
                IsEncrypted: false,
                HasLikelySignature: false,
                PageCount: null,
                FailureReason: "Arquivo não é um PDF válido ou o cabeçalho está corrompido."
            );
        }

        // 2. Validação do Rodapé (%%EOF nos últimos 4096 bytes)
        int trailerBytesToRead = (int)Math.Min(length, TrailerBufferSize);
        long trailerOffset = length - trailerBytesToRead;
        stream.Seek(trailerOffset, SeekOrigin.Begin);
        byte[] trailerBuffer = new byte[trailerBytesToRead];
        await stream.ReadExactlyAsync(trailerBuffer, 0, trailerBytesToRead, cancellationToken);
        string trailerText = Encoding.ASCII.GetString(trailerBuffer);

        if (!EofRegex().IsMatch(trailerText))
        {
            return new PdfInspectionResult(
                HasValidHeader: true,
                HasValidTrailer: false,
                IsEncrypted: false,
                HasLikelySignature: false,
                PageCount: null,
                FailureReason: "Arquivo PDF corrompido ou truncado (marcador %%EOF ausente)."
            );
        }

        // 3. Varredura estrutural em blocos para Criptografia, Assinatura e Contagem de Páginas
        bool isEncrypted = false;
        bool hasByteRange = false;
        bool hasSigMarker = false;
        int? maxPageCount = null;

        // Verifica primeiro o trailerText, pois /Encrypt frequentemente fica no dicionário trailer
        if (EncryptRegex().IsMatch(trailerText))
        {
            isEncrypted = true;
        }

        // Leitura em blocos sobrepostos para garantir detecção sem carregar todo o arquivo na memória
        stream.Seek(0, SeekOrigin.Begin);
        byte[] buffer = new byte[ChunkBufferSize];
        int bytesRead;
        long currentPosition = 0;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, ChunkBufferSize), cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Decodifica como Latin1 para preservar 1 byte por caractere sem corromper posições
            string chunkText = Encoding.Latin1.GetString(buffer, 0, bytesRead);

            // Criptografia
            if (!isEncrypted && EncryptRegex().IsMatch(chunkText))
            {
                isEncrypted = true;
            }

            // Assinatura digital heurística
            if (!hasByteRange && ByteRangeRegex().IsMatch(chunkText))
            {
                hasByteRange = true;
            }

            if (!hasSigMarker && (SigTypeRegex().IsMatch(chunkText) || HexContentsRegex().IsMatch(chunkText)))
            {
                hasSigMarker = true;
            }

            // Contagem de páginas: varre nós /Type /Pages
            ExtractPageCountsFromChunk(chunkText, ref maxPageCount);

            currentPosition += bytesRead;
            if (currentPosition >= length)
            {
                break;
            }

            // Recua um pequeno overlap para tokens que cruzem a fronteira do bloco
            if (bytesRead == ChunkBufferSize && stream.Position > OverlapSize)
            {
                stream.Seek(-OverlapSize, SeekOrigin.Current);
                currentPosition -= OverlapSize;
            }
        }

        bool hasLikelySignature = (hasByteRange && hasSigMarker) || (hasByteRange && SigTypeRegex().IsMatch(headerText + trailerText));

        return new PdfInspectionResult(
            HasValidHeader: true,
            HasValidTrailer: true,
            IsEncrypted: isEncrypted,
            HasLikelySignature: hasLikelySignature,
            PageCount: maxPageCount
        );
    }

    private static void ExtractPageCountsFromChunk(string text, ref int? currentMax)
    {
        // 1. Procura << ... /Type /Pages ... /Count N ... >>
        foreach (Match match in PagesDictWithCountRegex().Matches(text))
        {
            if (int.TryParse(match.Groups["count"].Value, out int count) && count > 0)
            {
                currentMax = currentMax.HasValue ? Math.Max(currentMax.Value, count) : count;
            }
        }

        // 2. Procura << ... /Count N ... /Type /Pages ... >>
        foreach (Match match in CountBeforePagesDictRegex().Matches(text))
        {
            if (int.TryParse(match.Groups["count"].Value, out int count) && count > 0)
            {
                currentMax = currentMax.HasValue ? Math.Max(currentMax.Value, count) : count;
            }
        }
    }
}
