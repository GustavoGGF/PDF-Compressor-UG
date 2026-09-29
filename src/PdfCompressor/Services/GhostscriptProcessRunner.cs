using System.ComponentModel;
using System.Text;
using PdfCompressor.Models;

namespace PdfCompressor.Services;

/// <summary>
/// Executa o Ghostscript de forma isolada, não interativa e cancelável,
/// gerenciando diretório temporário exclusivo e garantindo limpeza total em qualquer desfecho.
/// </summary>
public sealed class GhostscriptProcessRunner : IGhostscriptProcessRunner
{
    private static readonly byte[] ExpectedPdfHeader = "%PDF-"u8.ToArray();

    private readonly IFileManagerService _fileManager;
    private readonly IDiagnosticLogger _logger;
    private readonly IGhostscriptProcessStarter _processStarter;

    public GhostscriptProcessRunner(
        IFileManagerService fileManager,
        IDiagnosticLogger logger,
        IGhostscriptProcessStarter? processStarter = null)
    {
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processStarter = processStarter ?? new DefaultGhostscriptProcessStarter();
    }

    public async Task<GhostscriptExecutionResult> RunAsync(
        GhostscriptExecutionParams parameters,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new GhostscriptExecutionResult(
                Success: false,
                ExitCode: -1,
                ExecutionTime: TimeSpan.Zero,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                FailureReason: GhostscriptFailureReason.Cancelled,
                ErrorMessage: "Operação cancelada antes de iniciar a execução."
            );
        }

        if (parameters == null)
        {
            return new GhostscriptExecutionResult(
                Success: false,
                ExitCode: -1,
                ExecutionTime: TimeSpan.Zero,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                FailureReason: GhostscriptFailureReason.InvalidInput,
                ErrorMessage: "Parâmetros de execução nulos."
            );
        }

        var validationResult = ValidatePreconditions(parameters);
        if (validationResult != null)
        {
            return validationResult;
        }

        var tempDirectory = _fileManager.CreateIsolatedTempDirectory();
        var tempWorkingFile = Path.Combine(tempDirectory, "ghostscript_attempt.pdf");

        try
        {
            var stagingParams = parameters with { OutputPdfPath = tempWorkingFile };
            IReadOnlyList<string> arguments;

            try
            {
                arguments = GhostscriptArgumentBuilder.BuildArguments(stagingParams);
            }
            catch (Exception ex)
            {
                _logger.LogError("Falha ao construir argumentos do Ghostscript.", ex);
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: -1,
                    ExecutionTime: TimeSpan.Zero,
                    StandardOutput: string.Empty,
                    StandardError: ex.Message,
                    FailureReason: GhostscriptFailureReason.InvalidInput,
                    ErrorMessage: $"Argumentos inválidos: {ex.Message}"
                );
            }

            _logger.LogInfo($"Iniciando execução do Ghostscript: DPI={parameters.Dpi}, Nível Compat={parameters.CompatibilityLevel}");

            ProcessRunOutput runOutput;
            try
            {
                runOutput = await _processStarter.StartAndRunAsync(parameters.ExecutablePath, arguments, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Execução do Ghostscript cancelada durante o processamento.");
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: -1,
                    ExecutionTime: TimeSpan.Zero,
                    StandardOutput: string.Empty,
                    StandardError: string.Empty,
                    FailureReason: GhostscriptFailureReason.Cancelled,
                    ErrorMessage: "Execução do Ghostscript cancelada pelo usuário."
                );
            }
            catch (Win32Exception ex)
            {
                _logger.LogError("Falha ao iniciar processo do Ghostscript no sistema operacional.", ex);
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: -1,
                    ExecutionTime: TimeSpan.Zero,
                    StandardOutput: string.Empty,
                    StandardError: ex.Message,
                    FailureReason: GhostscriptFailureReason.StartFailure,
                    ErrorMessage: $"Falha ao iniciar processo do Ghostscript: {ex.Message}"
                );
            }
            catch (Exception ex)
            {
                _logger.LogError("Erro inesperado durante execução do processo do Ghostscript.", ex);
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: -1,
                    ExecutionTime: TimeSpan.Zero,
                    StandardOutput: string.Empty,
                    StandardError: ex.Message,
                    FailureReason: GhostscriptFailureReason.StartFailure,
                    ErrorMessage: $"Erro ao executar o processo: {ex.Message}"
                );
            }

            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Cancelamento requisitado após o término da execução do Ghostscript.");
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: runOutput.ExitCode,
                    ExecutionTime: runOutput.Duration,
                    StandardOutput: runOutput.StandardOutput,
                    StandardError: runOutput.StandardError,
                    FailureReason: GhostscriptFailureReason.Cancelled,
                    ErrorMessage: "Execução cancelada pelo usuário."
                );
            }

            if (runOutput.ExitCode != 0)
            {
                _logger.LogWarning($"Ghostscript finalizou com código {runOutput.ExitCode}. Erro: {runOutput.StandardError}");
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: runOutput.ExitCode,
                    ExecutionTime: runOutput.Duration,
                    StandardOutput: runOutput.StandardOutput,
                    StandardError: runOutput.StandardError,
                    FailureReason: GhostscriptFailureReason.NonZeroExitCode,
                    ErrorMessage: $"O processo retornou código de saída diferente de zero: {runOutput.ExitCode}."
                );
            }

            // Validações do arquivo de saída temporário
            if (!File.Exists(tempWorkingFile))
            {
                _logger.LogError("Processo Ghostscript encerrou com sucesso, mas o arquivo de saída não foi gerado.");
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: runOutput.ExitCode,
                    ExecutionTime: runOutput.Duration,
                    StandardOutput: runOutput.StandardOutput,
                    StandardError: runOutput.StandardError,
                    FailureReason: GhostscriptFailureReason.OutputMissing,
                    ErrorMessage: "O arquivo de saída temporário não foi gerado pelo Ghostscript."
                );
            }

            var fileInfo = new FileInfo(tempWorkingFile);
            if (fileInfo.Length == 0)
            {
                _logger.LogError("Ghostscript gerou um arquivo de saída vazio (0 bytes).");
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: runOutput.ExitCode,
                    ExecutionTime: runOutput.Duration,
                    StandardOutput: runOutput.StandardOutput,
                    StandardError: runOutput.StandardError,
                    FailureReason: GhostscriptFailureReason.InvalidOutput,
                    ErrorMessage: "O arquivo de saída gerado possui 0 bytes."
                );
            }

            if (!IsValidPdfHeader(tempWorkingFile))
            {
                _logger.LogError("Arquivo de saída gerado pelo Ghostscript não contém cabeçalho PDF válido.");
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: runOutput.ExitCode,
                    ExecutionTime: runOutput.Duration,
                    StandardOutput: runOutput.StandardOutput,
                    StandardError: runOutput.StandardError,
                    FailureReason: GhostscriptFailureReason.InvalidOutput,
                    ErrorMessage: "O arquivo gerado pelo Ghostscript não possui cabeçalho PDF válido (%PDF-)."
                );
            }

            // Move ou copia para o destino final com garantia de criação do diretório
            try
            {
                var destinationDir = Path.GetDirectoryName(parameters.OutputPdfPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Copy(tempWorkingFile, parameters.OutputPdfPath, overwrite: true);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                _logger.LogError($"Erro de permissão ou I/O ao gravar arquivo de saída final '{parameters.OutputPdfPath}'.", ex);
                return new GhostscriptExecutionResult(
                    Success: false,
                    ExitCode: runOutput.ExitCode,
                    ExecutionTime: runOutput.Duration,
                    StandardOutput: runOutput.StandardOutput,
                    StandardError: runOutput.StandardError,
                    FailureReason: GhostscriptFailureReason.PermissionOrDiskDenied,
                    ErrorMessage: $"Permissão negada ou falha de disco ao salvar no destino: {ex.Message}"
                );
            }

            _logger.LogInfo($"Ghostscript finalizado com sucesso: DPI={parameters.Dpi}, Saída={fileInfo.Length} bytes, Duração={runOutput.Duration.TotalMilliseconds:F0}ms");

            return new GhostscriptExecutionResult(
                Success: true,
                ExitCode: 0,
                ExecutionTime: runOutput.Duration,
                StandardOutput: runOutput.StandardOutput,
                StandardError: runOutput.StandardError,
                FailureReason: GhostscriptFailureReason.None
            );
        }
        finally
        {
            // Limpeza estrita do diretório temporário da tentativa em todos os caminhos
            _fileManager.SafeDeleteDirectory(tempDirectory);
        }
    }

    private static GhostscriptExecutionResult? ValidatePreconditions(GhostscriptExecutionParams parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters.ExecutablePath) || !File.Exists(parameters.ExecutablePath))
        {
            return new GhostscriptExecutionResult(
                Success: false,
                ExitCode: -1,
                ExecutionTime: TimeSpan.Zero,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                FailureReason: GhostscriptFailureReason.ExecutableNotFound,
                ErrorMessage: $"Executável do Ghostscript não encontrado: '{parameters.ExecutablePath}'."
            );
        }

        if (string.IsNullOrWhiteSpace(parameters.InputPdfPath) || !File.Exists(parameters.InputPdfPath))
        {
            return new GhostscriptExecutionResult(
                Success: false,
                ExitCode: -1,
                ExecutionTime: TimeSpan.Zero,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                FailureReason: GhostscriptFailureReason.InvalidInput,
                ErrorMessage: $"Arquivo de entrada não encontrado: '{parameters.InputPdfPath}'."
            );
        }

        if (string.IsNullOrWhiteSpace(parameters.OutputPdfPath))
        {
            return new GhostscriptExecutionResult(
                Success: false,
                ExitCode: -1,
                ExecutionTime: TimeSpan.Zero,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                FailureReason: GhostscriptFailureReason.InvalidInput,
                ErrorMessage: "Caminho do arquivo de saída não informado."
            );
        }

        var fullInput = Path.GetFullPath(parameters.InputPdfPath);
        var fullOutput = Path.GetFullPath(parameters.OutputPdfPath);

        if (string.Equals(fullInput, fullOutput, StringComparison.OrdinalIgnoreCase))
        {
            return new GhostscriptExecutionResult(
                Success: false,
                ExitCode: -1,
                ExecutionTime: TimeSpan.Zero,
                StandardOutput: string.Empty,
                StandardError: string.Empty,
                FailureReason: GhostscriptFailureReason.InvalidInput,
                ErrorMessage: "O arquivo de entrada não pode ser o mesmo de saída para evitar perda ou corrupção de dados."
            );
        }

        return null;
    }

    private static bool IsValidPdfHeader(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var buffer = new byte[ExpectedPdfHeader.Length];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            return bytesRead == ExpectedPdfHeader.Length && buffer.SequenceEqual(ExpectedPdfHeader);
        }
        catch
        {
            return false;
        }
    }
}
