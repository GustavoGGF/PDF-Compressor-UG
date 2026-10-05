using System.Globalization;
using PdfCompressor.Infrastructure;
using PdfCompressor.Models;
using PdfCompressor.Services;

namespace PdfCompressor.UI;

/// <summary>
/// Modelo de visualização e orquestrador de estado para a tela principal (MainForm).
/// Desacopla regras de transição de estado, validação de entradas e execução assíncrona dos controles WinForms.
/// </summary>
public sealed class MainFormViewModel : IDisposable
{
    private readonly IPdfAnalyzerService _pdfAnalyzer;
    private readonly ICompressionEngine _compressionEngine;
    private readonly IFileManagerService _fileManager;
    private readonly IFileLauncherService _fileLauncher;
    private readonly IDiagnosticLogger _logger;
    private readonly IUiDispatcher _dispatcher;
    private readonly CultureInfo _culture;

    private CancellationTokenSource? _compressionCts;
    private bool _disposed;

    /// <summary>
    /// Evento disparado sempre que o estado da interface (<see cref="UiState"/>) for alterado.
    /// </summary>
    public event EventHandler<UiState>? StateChanged;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="MainFormViewModel"/> com os serviços necessários.
    /// </summary>
    public MainFormViewModel(
        IPdfAnalyzerService pdfAnalyzer,
        ICompressionEngine compressionEngine,
        IFileManagerService fileManager,
        IFileLauncherService fileLauncher,
        IDiagnosticLogger logger,
        IUiDispatcher? dispatcher = null,
        CultureInfo? culture = null)
    {
        _pdfAnalyzer = pdfAnalyzer ?? throw new ArgumentNullException(nameof(pdfAnalyzer));
        _compressionEngine = compressionEngine ?? throw new ArgumentNullException(nameof(compressionEngine));
        _fileManager = fileManager ?? throw new ArgumentNullException(nameof(fileManager));
        _fileLauncher = fileLauncher ?? throw new ArgumentNullException(nameof(fileLauncher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = dispatcher ?? ImmediateUiDispatcher.Instance;
        _culture = culture ?? CultureInfo.GetCultureInfo("pt-BR");

        RevalidateTargetSize();
    }

    /// <summary>
    /// Construtor de conveniência que resolve as dependências a partir do contêiner de serviços da aplicação.
    /// </summary>
    public MainFormViewModel(AppServiceContainer services, IUiDispatcher? dispatcher = null)
        : this(
            services?.PdfAnalyzer ?? throw new ArgumentNullException(nameof(services)),
            services.CompressionEngine ?? throw new InvalidOperationException("CompressionEngine não está registrado no contêiner."),
            services.FileManager,
            services.FileLauncher,
            services.Logger,
            dispatcher)
    {
    }

    /// <summary>
    /// Estado atual do ciclo de vida da interface.
    /// </summary>
    public UiState State { get; private set; } = UiState.Idle;

    /// <summary>Caminho completo do arquivo PDF selecionado atualmente.</summary>
    public string? SelectedFilePath { get; private set; }

    /// <summary>Nome do arquivo PDF selecionado ou mensagem padrão quando nenhum estiver selecionado.</summary>
    public string SelectedFileName => !string.IsNullOrEmpty(SelectedFilePath) ? Path.GetFileName(SelectedFilePath) : "Nenhum arquivo selecionado";

    /// <summary>Informações e diagnóstico de metadados do PDF analisado.</summary>
    public PdfInfo? CurrentPdfInfo { get; private set; }

    /// <summary>Tamanho do arquivo original formatado para exibição.</summary>
    public string OriginalFileSizeText => CurrentPdfInfo != null ? TargetSizeUnitExtensions.FormatBytes(CurrentPdfInfo.FileSizeBytes, _culture) : "-";

    /// <summary>Quantidade de páginas formatada para exibição.</summary>
    public string PageCountText => CurrentPdfInfo?.PageCount != null ? $"{CurrentPdfInfo.PageCount.Value} páginas" : (CurrentPdfInfo != null ? "Desconhecido" : "-");

    /// <summary>Indica se foram detectados indicadores de assinatura digital no documento.</summary>
    public bool HasSignatureWarning => CurrentPdfInfo != null && (CurrentPdfInfo.HasLikelySignature || !string.IsNullOrWhiteSpace(CurrentPdfInfo.WarningMessage));

    /// <summary>Mensagem de aviso sobre potencial invalidação de assinatura digital.</summary>
    public string? SignatureWarningText => CurrentPdfInfo?.WarningMessage ?? (CurrentPdfInfo?.HasLikelySignature == true ? PdfInfo.DefaultSignatureWarningMessage : null);

    /// <summary>Predefinição de qualidade/algoritmo de compressão selecionada.</summary>
    public CompressionPreset SelectedPreset { get; private set; } = CompressionPreset.Automatic;

    /// <summary>Texto do tamanho-alvo inserido pelo usuário.</summary>
    public string TargetSizeInput { get; private set; } = "2,0";

    /// <summary>Unidade selecionada para o tamanho-alvo (MB ou KB).</summary>
    public TargetSizeUnit TargetUnit { get; private set; } = TargetSizeUnit.MB;

    /// <summary>Mensagem de erro de validação do tamanho-alvo, se houver.</summary>
    public string? TargetValidationError { get; private set; }

    /// <summary>Indica se o tamanho-alvo inserido é válido.</summary>
    public bool IsTargetSizeValid => TargetValidationError == null;

    /// <summary>Indica se os controles de entrada de tamanho-alvo devem permanecer habilitados.</summary>
    public bool IsTargetInputEnabled => CanConfigureSettings && SelectedPreset == CompressionPreset.Automatic;

    /// <summary>Indica se a seleção de preset deve permanecer habilitada.</summary>
    public bool IsPresetEnabled => CanConfigureSettings;

    /// <summary>Percentual estimado de progresso (0 a 100).</summary>
    public int ProgressPercentage { get; private set; }

    /// <summary>Texto descritivo do progresso da tentativa corrente.</summary>
    public string ProgressText { get; private set; } = string.Empty;

    /// <summary>Mensagem de status global exibida ao usuário.</summary>
    public string StatusMessage { get; private set; } = "Aguardando seleção de arquivo PDF...";

    /// <summary>Indica se a barra de progresso deve estar visível.</summary>
    public bool IsProgressVisible => State is UiState.Analyzing or UiState.Compressing or UiState.Cancelling;

    /// <summary>Resultado consolidado da última operação de compressão executada.</summary>
    public CompressionResult? LastResult { get; private set; }

    /// <summary>Mensagem de erro da última operação que falhou, se aplicável.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Texto de título/banner da seção de resultado.</summary>
    public string ResultTitleText => State switch
    {
        UiState.Success => "✔ Compressão concluída com sucesso! (Alvo atingido)",
        UiState.BestEffort => "⚠ Compressão por melhor esforço (Alvo não alcançado)",
        UiState.NoReduction => "⚠ Nenhuma redução obtida",
        UiState.Cancelled => "✖ Operação cancelada pelo usuário.",
        UiState.Error => "✖ Falha no processamento.",
        _ => "Nenhuma operação realizada ainda."
    };

    /// <summary>Tamanho final obtido formatado para exibição.</summary>
    public string FinalFileSizeText => LastResult?.OutputFilePath != null
        ? TargetSizeUnitExtensions.FormatBytes(LastResult.FinalSizeBytes, _culture)
        : "-";

    /// <summary>Percentual de redução obtido formatado para exibição.</summary>
    public string ReductionPercentageText => LastResult != null ? $"{LastResult.ReductionPercentage.ToString("0.0", _culture)}%" : "-";

    /// <summary>Qualidade DPI final aplicada formatada para exibição.</summary>
    public string FinalDpiText => LastResult?.FinalDpi != null ? $"{LastResult.FinalDpi.Value} DPI" : "-";

    /// <summary>Caminho do arquivo final gerado em disco.</summary>
    public string OutputFilePathText => LastResult?.OutputFilePath ?? "-";

    /// <summary>Indica se as opções de configuração estão editáveis no estado corrente.</summary>
    public bool CanConfigureSettings => State is UiState.Ready or UiState.Success or UiState.BestEffort or UiState.NoReduction or UiState.Cancelled or UiState.Error;

    /// <summary>Indica se a ação de seleção de arquivo está disponível.</summary>
    public bool CanSelectFile => State is not (UiState.Analyzing or UiState.Compressing or UiState.Cancelling);

    /// <summary>Indica se o botão Comprimir pode ser acionado.</summary>
    public bool CanCompress => CanConfigureSettings && CurrentPdfInfo is { IsValid: true } && (SelectedPreset != CompressionPreset.Automatic || IsTargetSizeValid);

    /// <summary>Indica se a ação de cancelamento está ativa.</summary>
    public bool CanCancel => State == UiState.Compressing;

    /// <summary>Indica se a ação de abrir o PDF gerado está habilitada.</summary>
    public bool CanOpenPdf => State is UiState.Success or UiState.BestEffort && !string.IsNullOrEmpty(LastResult?.OutputFilePath);

    /// <summary>Indica se a ação de abrir a pasta do arquivo está habilitada.</summary>
    public bool CanOpenFolder => State is UiState.Success or UiState.BestEffort && !string.IsNullOrEmpty(LastResult?.OutputFilePath);

    /// <summary>
    /// Valida preliminarmente se o caminho informado corresponde a um arquivo PDF existente.
    /// </summary>
    public static bool TryValidateFilePath(string? filePath, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "O caminho do arquivo não pode ser vazio.";
            return false;
        }

        if (Directory.Exists(filePath))
        {
            errorMessage = "Diretórios não são suportados. Selecione um arquivo PDF individual.";
            return false;
        }

        if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "O arquivo selecionado não possui a extensão .pdf.";
            return false;
        }

        if (!File.Exists(filePath))
        {
            errorMessage = "O arquivo selecionado não foi encontrado no sistema.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Define o arquivo selecionado e dispara a análise assíncrona de metadados e segurança.
    /// </summary>
    public async Task SelectAndAnalyzeFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!CanSelectFile) return;

        if (!TryValidateFilePath(filePath, out string? validationError))
        {
            SetError(validationError ?? "Arquivo inválido.");
            return;
        }

        SelectedFilePath = filePath;
        LastResult = null;
        ErrorMessage = null;
        TransitionTo(UiState.Analyzing, "Analisando arquivo PDF...");

        try
        {
            var info = await _pdfAnalyzer.AnalyzeAsync(filePath, cancellationToken).ConfigureAwait(false);
            _dispatcher.Post(() =>
            {
                CurrentPdfInfo = info;
                if (!info.IsValid)
                {
                    SetError(info.ErrorMessage ?? "O arquivo PDF é inválido, está protegido por senha ou inacessível.");
                }
                else
                {
                    TransitionTo(UiState.Ready, "Arquivo pronto para compressão.");
                }
            });
        }
        catch (OperationCanceledException)
        {
            _dispatcher.Post(() => TransitionTo(UiState.Idle, "Análise cancelada."));
        }
        catch (Exception ex)
        {
            _logger.LogError("Falha durante análise do PDF na UI", ex);
            _dispatcher.Post(() => SetError("Erro inesperado ao analisar o arquivo PDF selecionado."));
        }
    }

    /// <summary>
    /// Atualiza o preset selecionado e revalida os requisitos de tamanho-alvo.
    /// </summary>
    public void UpdatePreset(CompressionPreset preset)
    {
        if (!CanConfigureSettings) return;
        SelectedPreset = preset;
        RevalidateTargetSize();
        NotifyState();
    }

    /// <summary>
    /// Atualiza o valor e a unidade do tamanho-alvo.
    /// </summary>
    public void UpdateTargetSize(string input, TargetSizeUnit unit)
    {
        TargetSizeInput = input;
        TargetUnit = unit;
        RevalidateTargetSize();
        NotifyState();
    }

    /// <summary>
    /// Executa o fluxo de compressão de forma assíncrona, gerenciando progresso, cancelamento e resultado.
    /// </summary>
    public async Task CompressAsync()
    {
        if (!CanCompress || string.IsNullOrEmpty(SelectedFilePath)) return;

        if (SelectedPreset == CompressionPreset.Automatic && !TargetSizeValidator.TryValidate(SelectedPreset, TargetSizeInput, TargetUnit, out _, out string? err))
        {
            SetError(err ?? TargetSizeValidator.TargetRequiredMessage);
            return;
        }

        TargetSizeValidator.TryValidate(SelectedPreset, TargetSizeInput, TargetUnit, out long? targetBytes, out _);

        string targetDir = Path.GetDirectoryName(SelectedFilePath) ?? Environment.CurrentDirectory;
        var options = new CompressionOptions(SelectedFilePath, targetDir, SelectedPreset, targetBytes, OverwriteTarget: false);

        _compressionCts?.Dispose();
        _compressionCts = new CancellationTokenSource();
        var ct = _compressionCts.Token;

        LastResult = null;
        ErrorMessage = null;
        ProgressPercentage = 0;
        ProgressText = "Preparando...";
        TransitionTo(UiState.Compressing, "Iniciando processo de compressão...");

        var progress = new Progress<CompressionProgressUpdate>(update =>
        {
            _dispatcher.Post(() =>
            {
                if (State == UiState.Compressing)
                {
                    ProgressPercentage = Math.Clamp(update.PercentageEstimate, 0, 100);
                    ProgressText = $"Tentativa {update.CurrentAttempt} de {update.TotalAttempts} ({update.DpiTested} DPI) - {update.StepDescription}";
                    StatusMessage = ProgressText;
                    NotifyState();
                }
            });
        });

        try
        {
            var result = await _compressionEngine.CompressAsync(options, progress, ct).ConfigureAwait(false);
            _dispatcher.Post(() => HandleCompressionCompleted(result));
        }
        catch (OperationCanceledException)
        {
            _dispatcher.Post(() => TransitionTo(UiState.Cancelled, "Operação de compressão cancelada pelo usuário."));
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro irrecuperável durante a compressão na UI", ex);
            _dispatcher.Post(() => SetError("Falha inesperada durante a compressão. Verifique o log para mais detalhes."));
        }
    }

    /// <summary>
    /// Solicita o cancelamento da operação de compressão em andamento.
    /// </summary>
    public void Cancel()
    {
        if (State != UiState.Compressing) return;
        TransitionTo(UiState.Cancelling, "Cancelando compressão e limpando arquivos temporários...");
        ProgressText = "Cancelando...";
        _compressionCts?.Cancel();
    }

    /// <summary>
    /// Abre o arquivo PDF gerado no visualizador padrão do sistema.
    /// </summary>
    public bool OpenResultPdf() => !string.IsNullOrEmpty(LastResult?.OutputFilePath) && _fileLauncher.OpenPdf(LastResult.OutputFilePath);

    /// <summary>
    /// Abre a pasta contendo o arquivo gerado ou selecionado no explorador do sistema.
    /// </summary>
    public bool OpenResultFolder()
    {
        string? targetPath = LastResult?.OutputFilePath ?? SelectedFilePath;
        return !string.IsNullOrEmpty(targetPath) && _fileLauncher.OpenFolderContainingFile(targetPath);
    }

    /// <summary>
    /// Restaura o estado da interface para Idle, pronto para nova operação limpa.
    /// </summary>
    public void Reset()
    {
        if (State == UiState.Compressing) Cancel();
        SelectedFilePath = null;
        CurrentPdfInfo = null;
        LastResult = null;
        ErrorMessage = null;
        ProgressPercentage = 0;
        ProgressText = string.Empty;
        TransitionTo(UiState.Idle, "Aguardando seleção de arquivo PDF...");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _compressionCts?.Cancel();
        _compressionCts?.Dispose();
    }

    private void HandleCompressionCompleted(CompressionResult result)
    {
        LastResult = result;
        switch (result.Status)
        {
            case CompressionStatus.TargetMet:
                ProgressPercentage = 100;
                ProgressText = "Concluído com sucesso.";
                TransitionTo(UiState.Success, "Compressão concluída com sucesso! Alvo de tamanho atingido.");
                break;
            case CompressionStatus.BestEffortAboveTarget:
                ProgressPercentage = 100;
                ProgressText = "Concluído (melhor esforço).";
                TransitionTo(UiState.BestEffort, result.Message ?? "Nenhuma tentativa atingiu o alvo solicitado. O melhor resultado obtido foi preservado.");
                break;
            case CompressionStatus.NoReduction:
                ProgressPercentage = 100;
                ProgressText = "Nenhum arquivo novo foi gerado.";
                TransitionTo(UiState.NoReduction, result.Message ?? "Nenhuma tentativa reduziu o arquivo original. Nenhum arquivo novo foi gerado.");
                break;
            case CompressionStatus.Cancelled:
                TransitionTo(UiState.Cancelled, "Operação de compressão cancelada.");
                break;
            case CompressionStatus.ToolUnavailable:
            case CompressionStatus.InvalidInput:
            case CompressionStatus.EngineFailed:
            default:
                SetError(result.Message ?? "Falha no processo de compressão.");
                break;
        }
    }

    private void RevalidateTargetSize()
    {
        TargetSizeValidator.TryValidate(SelectedPreset, TargetSizeInput, TargetUnit, out _, out string? error);
        TargetValidationError = error;
    }

    private void TransitionTo(UiState newState, string status)
    {
        State = newState;
        StatusMessage = status;
        NotifyState();
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        TransitionTo(UiState.Error, message);
    }

    private void NotifyState() => StateChanged?.Invoke(this, State);
}
