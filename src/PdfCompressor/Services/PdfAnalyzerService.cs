using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Analisa a integridade e metadados preliminares do arquivo PDF de forma segura,
/// não destrutiva e sem iniciar o motor de compressão quando houver pré-condições impeditivas.
/// </summary>
public sealed class PdfAnalyzerService : IPdfAnalyzerService
{
    private readonly IDiagnosticLogger _logger;

    /// <summary>
    /// Hook interno para testes de estabilidade (simulação de modificação concorrente durante a análise).
    /// </summary>
    internal Func<string, Task>? OnBeforeInspectionHook { get; set; }

    public PdfAnalyzerService(IDiagnosticLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PdfInfo> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Validação de pré-condições de caminho e existência
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return LogAndReturn(PdfInfo.Failed(string.Empty, "Caminho do arquivo não especificado."));
        }

        if (Directory.Exists(filePath))
        {
            return LogAndReturn(PdfInfo.Failed(filePath, "O caminho informado é um diretório, não um arquivo."));
        }

        if (!File.Exists(filePath))
        {
            return LogAndReturn(PdfInfo.Failed(filePath, "Arquivo não encontrado."));
        }

        string extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return LogAndReturn(PdfInfo.Failed(filePath, "Extensão de arquivo inválida. Apenas arquivos .pdf são suportados."));
        }

        // 2. Validação de tamanho e estabilidade preliminar
        FileInfo fileInfo;
        long initialSize;
        try
        {
            fileInfo = new FileInfo(filePath);
            initialSize = fileInfo.Length;
        }
        catch (Exception ex)
        {
            return LogAndReturn(PdfInfo.Failed(filePath, $"Erro ao verificar metadados do arquivo: {ex.Message}"));
        }

        if (initialSize == 0)
        {
            return LogAndReturn(PdfInfo.Failed(filePath, "O arquivo PDF está vazio (0 bytes).", 0));
        }

        // Hook de teste para alteração concorrente antes da abertura do stream
        if (OnBeforeInspectionHook is not null)
        {
            await OnBeforeInspectionHook(filePath);
        }

        // 3. Abertura segura e somente leitura com FileShare.Read
        FileStream stream;
        try
        {
            stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        }
        catch (UnauthorizedAccessException)
        {
            return LogAndReturn(PdfInfo.Failed(filePath, "Acesso negado ao arquivo ou permissão insuficiente de leitura.", initialSize));
        }
        catch (IOException)
        {
            return LogAndReturn(PdfInfo.Failed(filePath, "Arquivo em uso por outro aplicativo ou bloqueado pelo sistema.", initialSize));
        }
        catch (Exception ex)
        {
            return LogAndReturn(PdfInfo.Failed(filePath, $"Erro ao acessar o arquivo: {ex.Message}", initialSize));
        }

        await using (stream.ConfigureAwait(false))
        {
            // 4. Verificação de estabilidade (tamanho mudou entre FileInfo e abertura do stream)
            fileInfo.Refresh();
            if (fileInfo.Length != initialSize || stream.Length != initialSize)
            {
                return LogAndReturn(PdfInfo.Failed(filePath, "O arquivo está sendo modificado ou seu tamanho não está estável.", initialSize));
            }

            // 5. Inspeção do conteúdo estrutural
            PdfInspectionResult inspection;
            try
            {
                inspection = await PdfContentInspector.InspectAsync(stream, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return LogAndReturn(PdfInfo.Failed(filePath, $"Falha inesperada durante a inspeção estrutural do PDF: {ex.Message}", initialSize));
            }

            // Cabeçalho ou rodapé inválidos/corrompidos
            if (!inspection.HasValidHeader || !inspection.HasValidTrailer)
            {
                return LogAndReturn(PdfInfo.Failed(
                    filePath,
                    inspection.FailureReason ?? "Arquivo não é um PDF válido ou está corrompido.",
                    initialSize
                ));
            }

            // Arquivo protegido por senha (DEC-06, TC-09)
            if (inspection.IsEncrypted)
            {
                return LogAndReturn(PdfInfo.Failed(
                    filePath,
                    PdfInfo.PasswordProtectedMessage,
                    initialSize,
                    isEncrypted: true
                ));
            }

            // Alerta de assinatura detectada (DEC-07, TC-06)
            if (inspection.HasLikelySignature)
            {
                return LogAndReturn(PdfInfo.Warning(
                    filePath,
                    initialSize,
                    inspection.PageCount,
                    hasLikelySignature: true,
                    PdfInfo.DefaultSignatureWarningMessage
                ));
            }

            // Sucesso pleno
            return LogAndReturn(PdfInfo.Success(filePath, initialSize, inspection.PageCount));
        }
    }

    private PdfInfo LogAndReturn(PdfInfo info)
    {
        _logger.LogAnalysisSummary(info);
        return info;
    }
}
