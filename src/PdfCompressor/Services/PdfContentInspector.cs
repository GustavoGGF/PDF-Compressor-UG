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
    private const int TrailerBufferSize = 64 * 1024; // 64 KB para acomodar metadados e streams no trailer
    private const int ChunkBufferSize = 64 * 1024; // 64 KB
    private const int OverlapSize = 1024; // 1 KB para evitar corte de tokens

    // Heurísticas compiladas e rápidas
    [GeneratedRegex(@"%PDF-[0-9]\.[0-9]")]
    private static partial Regex HeaderRegex();

    [GeneratedRegex(@"%%EOF")]
    private static partial Regex EofRegex();

    [GeneratedRegex(@"/Encrypt\b")]
    private static partial Regex EncryptRegex();

    [GeneratedRegex(@"/ByteRange\b")]
    private static partial Regex ByteRangeRegex();

    [GeneratedRegex(@"/(?:Type|FT)\s*/Sig\b")]
    private static partial Regex SigTypeRegex();

    [GeneratedRegex(@"/Contents\s*<[0-9a-fA-F\s]+>")]
    private static partial Regex HexContentsRegex();

    [GeneratedRegex(@"<<[^>]*?/Type\s*/Pages\b[^>]*?/Count\s+(?<count>\d+)[^>]*?>>", RegexOptions.Singleline)]
    private static partial Regex PagesDictWithCountRegex();

    [GeneratedRegex(@"<<[^>]*?/Count\s+(?<count>\d+)[^>]*?/Type\s*/Pages\b[^>]*?>>", RegexOptions.Singleline)]
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
        var headerText = Encoding.ASCII.GetString(headerBuffer);

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

        // 2. Validação do Rodapé (procura %%EOF considerando possíveis bytes nulos ou padding pós-EOF)
        string? trailerText = await FindTrailerTextAsync(stream, length, cancellationToken).ConfigureAwait(false);

        if (trailerText is null || !EofRegex().IsMatch(trailerText))
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
        char[] charBuffer = new char[ChunkBufferSize];
        int bytesRead;
        long currentPosition = 0;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, ChunkBufferSize), cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProcessChunk(buffer, bytesRead, charBuffer, ref isEncrypted, ref hasByteRange, ref hasSigMarker, ref maxPageCount);

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

    private static void ProcessChunk(byte[] buffer, int bytesRead, char[] charBuffer, ref bool isEncrypted, ref bool hasByteRange, ref bool hasSigMarker, ref int? maxPageCount)
    {
        int charsDecoded = Encoding.Latin1.GetChars(buffer.AsSpan(0, bytesRead), charBuffer.AsSpan());
        var chunkSpan = charBuffer.AsSpan(0, charsDecoded);

        if (!isEncrypted && EncryptRegex().IsMatch(chunkSpan))
        {
            isEncrypted = true;
        }

        if (!hasByteRange && ByteRangeRegex().IsMatch(chunkSpan))
        {
            hasByteRange = true;
        }

        if (!hasSigMarker && (SigTypeRegex().IsMatch(chunkSpan) || HexContentsRegex().IsMatch(chunkSpan)))
        {
            hasSigMarker = true;
        }

        ExtractPageCountsFromChunk(chunkSpan, ref maxPageCount);
    }

    private static void ExtractPageCountsFromChunk(ReadOnlySpan<char> text, ref int? currentMax)
    {
        // 1. Procura << ... /Type /Pages ... /Count N ... >>
        foreach (var match in PagesDictWithCountRegex().EnumerateMatches(text))
        {
            var matchSpan = text.Slice(match.Index, match.Length);
            ExtractAllCounts(matchSpan, ref currentMax);
        }

        // 2. Procura << ... /Count N ... /Type /Pages ... >>
        foreach (var match in CountBeforePagesDictRegex().EnumerateMatches(text))
        {
            var matchSpan = text.Slice(match.Index, match.Length);
            ExtractAllCounts(matchSpan, ref currentMax);
        }
    }

    private static void ExtractAllCounts(ReadOnlySpan<char> matchSpan, ref int? currentMax)
    {
        int offset = 0;
        while (offset < matchSpan.Length)
        {
            var span = matchSpan.Slice(offset);
            int index = span.IndexOf("/Count", StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            var afterCount = span.Slice(index + 6); // length of "/Count"
            bool extracted = ExtractNumberFromSpan(afterCount, ref currentMax);

            // Advance offset
            offset += index + 6;
            // Also need to advance past the number, but skipping just 6 is safe enough for another loop iteration
        }
    }

    private static bool ExtractNumberFromSpan(ReadOnlySpan<char> span, ref int? currentMax)
    {
        // Skip whitespace
        int i = 0;
        while (i < span.Length && char.IsWhiteSpace(span[i]))
        {
            i++;
        }

        // Parse number
        int number = 0;
        bool hasDigits = false;
        while (i < span.Length && char.IsAsciiDigit(span[i]))
        {
            number = number * 10 + (span[i] - '0');
            hasDigits = true;
            i++;
        }

        if (hasDigits && number > 0)
        {
            currentMax = currentMax.HasValue ? Math.Max(currentMax.Value, number) : number;
            return true;
        }

        return false;
    }

    private static async Task<string?> FindTrailerTextAsync(Stream stream, long fileLength, CancellationToken cancellationToken)
    {
        // Tenta usar ArrayPool para não alocar arrays grandes de forma desnecessária
        var pool = System.Buffers.ArrayPool<byte>.Shared;

        // 1. Tenta ler uma janela inicial do final (64 KB)
        int initialWindow = (int)Math.Min(fileLength, TrailerBufferSize);
        stream.Seek(fileLength - initialWindow, SeekOrigin.Begin);
        byte[] buffer = pool.Rent(initialWindow);
        try
        {
            await stream.ReadExactlyAsync(buffer, 0, initialWindow, cancellationToken).ConfigureAwait(false);

            // 2. Se encontrar %%EOF no final, retorna o texto do trailer
            string trailerText = Encoding.Latin1.GetString(buffer, 0, initialWindow);
            if (EofRegex().IsMatch(trailerText))
            {
                return trailerText;
            }

            // 3. Verifica se o final do arquivo é composto apenas por bytes nulos ou espaços em branco (padding)
            // Percorremos de trás para frente até encontrar o último byte não-nulo e não-espaço.
            int nonPaddingEndIndex = -1;
            for (int i = initialWindow - 1; i >= 0; i--)
            {
                byte b = buffer[i];
                if (b != 0 && b != (byte)' ' && b != (byte)'\r' && b != (byte)'\n' && b != (byte)'\t')
                {
                    nonPaddingEndIndex = i;
                    break;
                }
            }

            long actualContentEnd;
            if (nonPaddingEndIndex >= 0)
            {
                actualContentEnd = (fileLength - initialWindow) + nonPaddingEndIndex + 1;
            }
            else
            {
                // Toda a janela inicial de 64 KB é composta de zeros/espaços (ex: arquivos com preenchimento em blocos).
                // Varre de trás para frente em blocos de 64 KB até achar bytes válidos de conteúdo.
                actualContentEnd = fileLength - initialWindow;
                byte[] scanBuffer = pool.Rent(ChunkBufferSize);
                try
                {
                    while (actualContentEnd > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(actualContentEnd, ChunkBufferSize);
                        long offset = actualContentEnd - toRead;
                        stream.Seek(offset, SeekOrigin.Begin);
                        await stream.ReadExactlyAsync(scanBuffer, 0, toRead, cancellationToken).ConfigureAwait(false);

                        int foundIndex = -1;
                        for (int i = toRead - 1; i >= 0; i--)
                        {
                            byte b = scanBuffer[i];
                            if (b != 0 && b != (byte)' ' && b != (byte)'\r' && b != (byte)'\n' && b != (byte)'\t')
                            {
                                foundIndex = i;
                                break;
                            }
                        }

                        if (foundIndex >= 0)
                        {
                            actualContentEnd = offset + foundIndex + 1;
                            break;
                        }

                        actualContentEnd = offset;
                    }
                }
                finally
                {
                    pool.Return(scanBuffer);
                }
            }

            if (actualContentEnd <= 0)
            {
                return null;
            }

            // Lê a janela do trailer posicionada logo antes do final do conteúdo real
            int actualTrailerBytes = (int)Math.Min(actualContentEnd, TrailerBufferSize);
            stream.Seek(actualContentEnd - actualTrailerBytes, SeekOrigin.Begin);
            byte[] finalTrailerBuffer = pool.Rent(actualTrailerBytes);
            try
            {
                await stream.ReadExactlyAsync(finalTrailerBuffer, 0, actualTrailerBytes, cancellationToken).ConfigureAwait(false);
                return Encoding.Latin1.GetString(finalTrailerBuffer, 0, actualTrailerBytes);
            }
            finally
            {
                pool.Return(finalTrailerBuffer);
            }
        }
        finally
        {
            pool.Return(buffer);
        }
    }
}
