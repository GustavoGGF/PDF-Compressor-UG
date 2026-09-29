using System.Diagnostics;
using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Motor principal de compressão e busca automática por tamanho-alvo.
/// Executa a orquestração de tentativas em ordem descendente de DPI,
/// selecionando a máxima qualidade elegível com garantia de limpeza e semântica explícita.
/// </summary>
public sealed class CompressionEngine : ICompressionEngine
{
    private readonly IGhostscriptLocator _locator;
    private readonly IGhostscriptProcessRunner _processRunner;
    private readonly IFileManagerService _fileManager;
    private readonly IPdfAnalyzerService _pdfAnalyzer;
    private readonly IDiagnosticLogger _logger;

    public CompressionEngine(
        IGhostscriptLocator locator,
        IGhostscriptProcessRunner processRunner,
        IFileManagerService fileManager,
        IPdfAnalyzerService pdfAnalyzer,
        IDiagnosticLogger logger)
    {
        _locator = locator ?? throw new ArgumentNullException(nameof(locator));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _pdfAnalyzer = pdfAnalyzer ?? throw new ArgumentNullException(nameof(pdfAnalyzer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CompressionResult> CompressAsync(
        CompressionOptions options,
        IProgress<CompressionProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();

        if (options == null)
        {
            return CreateErrorResult(
                CompressionStatus.InvalidInput,
                sourcePath: string.Empty,
                originalSize: 0,
                duration: totalStopwatch.Elapsed,
                message: "As opções de compressão não foram fornecidas."
            );
        }

        try
        {
            options.Validate();
        }
        catch (ArgumentException ex)
        {
            return CreateErrorResult(
                CompressionStatus.InvalidInput,
                sourcePath: options.SourceFilePath,
                originalSize: 0,
                duration: totalStopwatch.Elapsed,
                message: ex.Message
            );
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return CreateCancelledResult(options.SourceFilePath, originalSize: 0, attempts: [], duration: totalStopwatch.Elapsed);
        }

        // Análise preliminar do arquivo de entrada
        PdfInfo pdfInfo;
        try
        {
            pdfInfo = await _pdfAnalyzer.AnalyzeAsync(options.SourceFilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return CreateCancelledResult(options.SourceFilePath, originalSize: 0, attempts: [], duration: totalStopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError("Falha inesperada ao analisar arquivo de entrada.", ex);
            return CreateErrorResult(
                CompressionStatus.InvalidInput,
                sourcePath: options.SourceFilePath,
                originalSize: 0,
                duration: totalStopwatch.Elapsed,
                message: $"Falha ao analisar arquivo de entrada: {ex.Message}"
            );
        }

        if (!pdfInfo.IsValid)
        {
            _logger.LogWarning($"Arquivo inválido para compressão '{options.SourceFilePath}': {pdfInfo.ErrorMessage}");
            string errorMessage = pdfInfo.ErrorMessage ?? (pdfInfo.IsEncrypted
                ? PdfInfo.PasswordProtectedMessage
                : "Arquivo de entrada inválido.");

            return CreateErrorResult(
                CompressionStatus.InvalidInput,
                sourcePath: options.SourceFilePath,
                originalSize: pdfInfo.FileSizeBytes,
                duration: totalStopwatch.Elapsed,
                message: errorMessage
            );
        }

        if (pdfInfo.HasLikelySignature)
        {
            _logger.LogWarning($"Assinatura detectada no arquivo '{options.SourceFilePath}'. O processo de compressão poderá invalidá-la.");
        }

        // Validação da disponibilidade do Ghostscript
        string? gsPath = _locator.FindExecutablePath();
        if (string.IsNullOrWhiteSpace(gsPath) || !_locator.IsAvailable())
        {
            _logger.LogWarning("Executável do Ghostscript (gswin64c.exe) não localizado no sistema.");
            return CreateErrorResult(
                CompressionStatus.ToolUnavailable,
                sourcePath: options.SourceFilePath,
                originalSize: pdfInfo.FileSizeBytes,
                duration: totalStopwatch.Elapsed,
                message: "O executável do Ghostscript não foi localizado no sistema."
            );
        }

        // Resolução do arquivo de saída final com garantia de não colisão com a entrada
        string finalOutputPath = options.OverwriteTarget
            ? Path.Combine(options.TargetDirectory, $"{Path.GetFileNameWithoutExtension(options.SourceFilePath)}_compactado{(Path.GetExtension(options.SourceFilePath) is { Length: > 0 } ext ? ext : ".pdf")}")
            : _fileManager.GenerateSafeOutputFilePath(options.SourceFilePath, options.TargetDirectory);

        string fullSource = Path.GetFullPath(options.SourceFilePath);
        string fullOutput = Path.GetFullPath(finalOutputPath);
        if (string.Equals(fullSource, fullOutput, StringComparison.OrdinalIgnoreCase))
        {
            return CreateErrorResult(
                CompressionStatus.InvalidInput,
                sourcePath: options.SourceFilePath,
                originalSize: pdfInfo.FileSizeBytes,
                duration: totalStopwatch.Elapsed,
                message: "O caminho de saída não pode ser idêntico ao arquivo de entrada para garantir sua imutabilidade. Escolha outro nome ou pasta de destino para preservar o original."
            );
        }

        int[] dpiSequence = CompressionPresetPolicy.GetDpiSequence(options.Preset);
        int totalAttempts = dpiSequence.Length;
        var attempts = new List<AttemptResult>();

        string sessionTempDir = _fileManager.CreateIsolatedTempDirectory();
        (string FilePath, int Dpi, long SizeBytes)? eligibleCandidate = null;
        (string FilePath, int Dpi, long SizeBytes)? smallestAboveTargetCandidate = null;

        try
        {
            for (int i = 0; i < totalAttempts; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return CreateCancelledResult(options.SourceFilePath, pdfInfo.FileSizeBytes, attempts, totalStopwatch.Elapsed);
                }

                int attemptIndex = i + 1;
                int dpi = dpiSequence[i];

                int progressPercentage = (int)Math.Round((i / (double)totalAttempts) * 100.0);
                progress?.Report(new CompressionProgressUpdate(
                    CurrentAttempt: attemptIndex,
                    TotalAttempts: totalAttempts,
                    DpiTested: dpi,
                    StepDescription: $"Testando compressão a {dpi} DPI (tentativa {attemptIndex} de {totalAttempts})...",
                    PercentageEstimate: progressPercentage
                ));

                string attemptWorkingFile = Path.Combine(sessionTempDir, $"attempt_{attemptIndex}_{dpi}dpi.pdf");
                var executionParams = new GhostscriptExecutionParams(
                    ExecutablePath: gsPath,
                    InputPdfPath: options.SourceFilePath,
                    OutputPdfPath: attemptWorkingFile,
                    Dpi: dpi,
                    CompatibilityLevel: 17
                );

                GhostscriptExecutionResult execResult;
                try
                {
                    execResult = await _processRunner.RunAsync(executionParams, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return CreateCancelledResult(options.SourceFilePath, pdfInfo.FileSizeBytes, attempts, totalStopwatch.Elapsed);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Erro ao executar Ghostscript na tentativa {attemptIndex} ({dpi} DPI).", ex);
                    execResult = new GhostscriptExecutionResult(
                        Success: false,
                        ExitCode: -1,
                        ExecutionTime: TimeSpan.Zero,
                        StandardOutput: string.Empty,
                        StandardError: ex.Message,
                        FailureReason: GhostscriptFailureReason.StartFailure,
                        ErrorMessage: ex.Message
                    );
                }

                if (execResult.FailureReason == GhostscriptFailureReason.Cancelled || cancellationToken.IsCancellationRequested)
                {
                    return CreateCancelledResult(options.SourceFilePath, pdfInfo.FileSizeBytes, attempts, totalStopwatch.Elapsed);
                }

                if (execResult.FailureReason == GhostscriptFailureReason.ExecutableNotFound)
                {
                    return CreateErrorResult(
                        CompressionStatus.ToolUnavailable,
                        sourcePath: options.SourceFilePath,
                        originalSize: pdfInfo.FileSizeBytes,
                        duration: totalStopwatch.Elapsed,
                        attempts: attempts,
                        message: "O executável do Ghostscript não foi localizado."
                    );
                }

                if (execResult.FailureReason == GhostscriptFailureReason.PermissionOrDiskDenied)
                {
                    return CreateErrorResult(
                        CompressionStatus.EngineFailed,
                        sourcePath: options.SourceFilePath,
                        originalSize: pdfInfo.FileSizeBytes,
                        duration: totalStopwatch.Elapsed,
                        attempts: attempts,
                        message: $"Falha de permissão ou espaço em disco: {execResult.ErrorMessage}"
                    );
                }

                if (!execResult.Success || !File.Exists(attemptWorkingFile))
                {
                    var failedAttempt = new AttemptResult(
                        AttemptIndex: attemptIndex,
                        Dpi: dpi,
                        OutputSizeBytes: 0,
                        Duration: execResult.ExecutionTime,
                        Succeeded: false,
                        ErrorDetails: execResult.ErrorMessage ?? "Falha na geração do arquivo para este nível de qualidade."
                    );
                    attempts.Add(failedAttempt);
                    continue;
                }

                long candidateSize = new FileInfo(attemptWorkingFile).Length;
                var successfulAttempt = new AttemptResult(
                    AttemptIndex: attemptIndex,
                    Dpi: dpi,
                    OutputSizeBytes: candidateSize,
                    Duration: execResult.ExecutionTime,
                    Succeeded: true,
                    ErrorDetails: null
                );
                attempts.Add(successfulAttempt);

                bool isEligible = CompressionPresetPolicy.IsEligible(candidateSize, options.TargetSizeBytes);

                if (isEligible)
                {
                    // Ordem descendente: a primeira tentativa elegível corresponde à máxima qualidade elegível
                    eligibleCandidate = (attemptWorkingFile, dpi, candidateSize);
                    break;
                }
                else
                {
                    if (smallestAboveTargetCandidate == null || candidateSize < smallestAboveTargetCandidate.Value.SizeBytes)
                    {
                        smallestAboveTargetCandidate = (attemptWorkingFile, dpi, candidateSize);
                    }
                }
            }

            totalStopwatch.Stop();

            if (eligibleCandidate != null)
            {
                if (!_fileManager.TryPromoteFile(eligibleCandidate.Value.FilePath, finalOutputPath, out string? copyError))
                {
                    return CreateErrorResult(
                        CompressionStatus.EngineFailed,
                        sourcePath: options.SourceFilePath,
                        originalSize: pdfInfo.FileSizeBytes,
                        duration: totalStopwatch.Elapsed,
                        attempts: attempts,
                        message: copyError ?? "Falha ao gravar arquivo compactado no destino final."
                    );
                }

                progress?.Report(new CompressionProgressUpdate(
                    CurrentAttempt: attempts.Count,
                    TotalAttempts: totalAttempts,
                    DpiTested: eligibleCandidate.Value.Dpi,
                    StepDescription: "Compressão concluída com sucesso.",
                    PercentageEstimate: 100
                ));

                var successResult = new CompressionResult(
                    Status: CompressionStatus.TargetMet,
                    SourceFilePath: options.SourceFilePath,
                    OutputFilePath: finalOutputPath,
                    OriginalSizeBytes: pdfInfo.FileSizeBytes,
                    FinalSizeBytes: eligibleCandidate.Value.SizeBytes,
                    FinalDpi: eligibleCandidate.Value.Dpi,
                    Attempts: attempts,
                    TotalDuration: totalStopwatch.Elapsed,
                    Message: "Arquivo compactado com sucesso respeitando o tamanho-alvo."
                );

                _logger.LogCompressionSummary(successResult);
                return successResult;
            }

            if (smallestAboveTargetCandidate != null)
            {
                if (!_fileManager.TryPromoteFile(smallestAboveTargetCandidate.Value.FilePath, finalOutputPath, out string? copyError))
                {
                    return CreateErrorResult(
                        CompressionStatus.EngineFailed,
                        sourcePath: options.SourceFilePath,
                        originalSize: pdfInfo.FileSizeBytes,
                        duration: totalStopwatch.Elapsed,
                        attempts: attempts,
                        message: copyError ?? "Falha ao gravar arquivo de melhor esforço no destino final."
                    );
                }

                progress?.Report(new CompressionProgressUpdate(
                    CurrentAttempt: attempts.Count,
                    TotalAttempts: totalAttempts,
                    DpiTested: smallestAboveTargetCandidate.Value.Dpi,
                    StepDescription: "Compressão concluída em melhor esforço acima do alvo.",
                    PercentageEstimate: 100
                ));

                double targetMb = options.TargetSizeBytes.HasValue ? options.TargetSizeBytes.Value / 1_000_000.0 : 0.0;
                double reachedMb = smallestAboveTargetCandidate.Value.SizeBytes / 1_000_000.0;
                string effortMessage = $"Nenhuma tentativa atingiu o limite de {targetMb:F2} MB. O menor tamanho obtido foi {reachedMb:F2} MB ({smallestAboveTargetCandidate.Value.Dpi} DPI).";

                var bestEffortResult = new CompressionResult(
                    Status: CompressionStatus.BestEffortAboveTarget,
                    SourceFilePath: options.SourceFilePath,
                    OutputFilePath: finalOutputPath,
                    OriginalSizeBytes: pdfInfo.FileSizeBytes,
                    FinalSizeBytes: smallestAboveTargetCandidate.Value.SizeBytes,
                    FinalDpi: smallestAboveTargetCandidate.Value.Dpi,
                    Attempts: attempts,
                    TotalDuration: totalStopwatch.Elapsed,
                    Message: effortMessage
                );

                _logger.LogCompressionSummary(bestEffortResult);
                return bestEffortResult;
            }

            var failedResult = new CompressionResult(
                Status: CompressionStatus.EngineFailed,
                SourceFilePath: options.SourceFilePath,
                OutputFilePath: null,
                OriginalSizeBytes: pdfInfo.FileSizeBytes,
                FinalSizeBytes: 0,
                FinalDpi: null,
                Attempts: attempts,
                TotalDuration: totalStopwatch.Elapsed,
                Message: "Nenhuma das tentativas de compressão gerou um arquivo PDF válido. O documento pode conter elementos incompatíveis ou estar corrompido."
            );

            _logger.LogCompressionSummary(failedResult);
            return failedResult;
        }
        finally
        {
            // Limpeza estrita de todos os intermediários da sessão com auditoria de diagnóstico
            bool cleaned = _fileManager.SafeDeleteDirectory(sessionTempDir);
            if (!cleaned)
            {
                _logger.LogWarning($"Aviso de limpeza: O diretório temporário de sessão '{sessionTempDir}' não pôde ser completamente excluído.");
            }
        }
    }

    private CompressionResult CreateCancelledResult(
        string sourcePath,
        long originalSize,
        IReadOnlyList<AttemptResult> attempts,
        TimeSpan duration)
    {
        _logger.LogWarning($"Operação de compressão cancelada para '{sourcePath}'.");
        return CreateErrorResult(
            CompressionStatus.Cancelled,
            sourcePath,
            originalSize,
            duration,
            "Operação de compressão cancelada pelo usuário.",
            attempts
        );
    }

    private CompressionResult CreateErrorResult(
        CompressionStatus status,
        string sourcePath,
        long originalSize,
        TimeSpan duration,
        string message,
        IReadOnlyList<AttemptResult>? attempts = null)
    {
        var result = new CompressionResult(
            Status: status,
            SourceFilePath: sourcePath,
            OutputFilePath: null,
            OriginalSizeBytes: originalSize,
            FinalSizeBytes: 0,
            FinalDpi: null,
            Attempts: attempts ?? [],
            TotalDuration: duration,
            Message: message
        );
        _logger.LogCompressionSummary(result);
        return result;
    }
}
