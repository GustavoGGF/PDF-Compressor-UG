using System.Drawing;
using System.Windows.Forms;
using PdfCompressor.Infrastructure;
using PdfCompressor.Models;

namespace PdfCompressor.UI;

/// <summary>
/// Formulário principal do PDF Compressor.
/// Responsável exclusivamente pela apresentação visual, captura de eventos de usuário e sincronização com o ViewModel.
/// </summary>
public partial class MainForm : Form, IUiDispatcher
{
    private readonly AppServiceContainer _services;
    private readonly MainFormViewModel _viewModel;
    private bool _isUpdatingControls;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="MainForm"/> com as dependências necessárias.
    /// </summary>
    /// <param name="services">Contêiner de serviços da aplicação.</param>
    public MainForm(AppServiceContainer services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        InitializeComponent();

        // Reutiliza o ícone configurado no executável também na barra de título do formulário.
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        _viewModel = new MainFormViewModel(services, dispatcher: this);
        _viewModel.StateChanged += OnViewModelStateChanged;

        ConfigureInitialUi();
        UpdateUiFromViewModel();
    }

    void IUiDispatcher.Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (InvokeRequired)
        {
            Invoke(action);
        }
        else
        {
            action();
        }
    }

    void IUiDispatcher.Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void ConfigureInitialUi()
    {
        _isUpdatingControls = true;

        cmbPreset.Items.Clear();
        cmbPreset.Items.Add("Automático (busca por tamanho-alvo)");
        cmbPreset.Items.Add("Alta qualidade (300 DPI - Impressão)");
        cmbPreset.Items.Add("Qualidade média (150 DPI - Leitura em tela)");
        cmbPreset.Items.Add("Compressão máxima (72 DPI - Menor tamanho)");
        cmbPreset.SelectedIndex = 0;

        cmbTargetUnit.Items.Clear();
        cmbTargetUnit.Items.Add("MB");
        cmbTargetUnit.Items.Add("KB");
        cmbTargetUnit.SelectedIndex = 0;

        btnSelectFile.Click += async (_, _) => await HandleSelectFileClickAsync();
        btnCompress.Click += async (_, _) => await _viewModel.CompressAsync();
        btnCancel.Click += (_, _) => _viewModel.Cancel();
        btnOpenPdf.Click += (_, _) => _viewModel.OpenResultPdf();
        btnOpenFolder.Click += (_, _) => _viewModel.OpenResultFolder();
        btnReset.Click += (_, _) => _viewModel.Reset();

        cmbPreset.SelectedIndexChanged += (_, _) => HandlePresetChanged();
        txtTargetSize.TextChanged += (_, _) => HandleTargetSizeChanged();
        cmbTargetUnit.SelectedIndexChanged += (_, _) => HandleTargetSizeChanged();

        // Drag-and-drop no formulário e na zona de drop
        DragEnter += HandleDragEnter;
        DragDrop += async (_, e) => await HandleDragDropAsync(e);
        lblDropZone.DragEnter += HandleDragEnter;
        lblDropZone.DragDrop += async (_, e) => await HandleDragDropAsync(e);
        lblDropZone.AllowDrop = true;

        _isUpdatingControls = false;
    }

    private async Task HandleSelectFileClickAsync()
    {
        if (openFileDialog.ShowDialog(this) == DialogResult.OK)
        {
            await _viewModel.SelectAndAnalyzeFileAsync(openFileDialog.FileName);
        }
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        if (!_viewModel.CanSelectFile)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 && files[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }
        }
        e.Effect = DragDropEffects.None;
    }

    private async Task HandleDragDropAsync(DragEventArgs e)
    {
        if (!_viewModel.CanSelectFile) return;

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            await _viewModel.SelectAndAnalyzeFileAsync(files[0]);
        }
    }

    private void HandlePresetChanged()
    {
        if (_isUpdatingControls) return;

        var preset = cmbPreset.SelectedIndex switch
        {
            1 => CompressionPreset.HighQuality,
            2 => CompressionPreset.MediumQuality,
            3 => CompressionPreset.StrongCompression,
            _ => CompressionPreset.Automatic
        };

        _viewModel.UpdatePreset(preset);
        UpdateUiFromViewModel();
    }

    private void HandleTargetSizeChanged()
    {
        if (_isUpdatingControls) return;

        var unit = cmbTargetUnit.SelectedIndex == 1 ? TargetSizeUnit.KB : TargetSizeUnit.MB;
        _viewModel.UpdateTargetSize(txtTargetSize.Text, unit);
        UpdateUiFromViewModel();
    }

    private void OnViewModelStateChanged(object? sender, UiState state)
    {
        ((IUiDispatcher)this).Post(UpdateUiFromViewModel);
    }

    private void UpdateUiFromViewModel()
    {
        if (IsDisposed) return;

        _isUpdatingControls = true;

        txtFilePath.Text = _viewModel.SelectedFilePath ?? "Nenhum arquivo selecionado";
        lblOriginalSize.Text = $"Tamanho original: {_viewModel.OriginalFileSizeText}";
        lblPageCount.Text = $"Páginas: {_viewModel.PageCountText}";

        pnlSignatureWarning.Visible = _viewModel.HasSignatureWarning;
        lblSignatureWarning.Text = _viewModel.SignatureWarningText ?? string.Empty;

        btnSelectFile.Enabled = _viewModel.CanSelectFile;
        lblDropZone.Enabled = _viewModel.CanSelectFile;

        cmbPreset.Enabled = _viewModel.IsPresetEnabled;
        txtTargetSize.Enabled = _viewModel.IsTargetInputEnabled;
        cmbTargetUnit.Enabled = _viewModel.IsTargetInputEnabled;

        lblTargetError.Visible = !_viewModel.IsTargetSizeValid && _viewModel.SelectedPreset == CompressionPreset.Automatic && _viewModel.CanConfigureSettings;
        lblTargetError.Text = _viewModel.TargetValidationError ?? string.Empty;

        btnCompress.Enabled = _viewModel.CanCompress;
        btnCancel.Enabled = _viewModel.CanCancel;

        progressBar.Style = _viewModel.State == UiState.Analyzing ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        progressBar.Value = Math.Clamp(_viewModel.ProgressPercentage, 0, 100);
        lblStatus.Text = _viewModel.StatusMessage;
        lblProgressDetails.Text = _viewModel.ProgressText;

        lblResultBanner.Text = _viewModel.ResultTitleText;
        lblResultBanner.ForeColor = _viewModel.State switch
        {
            UiState.Success => Color.DarkGreen,
            UiState.BestEffort => Color.DarkGoldenrod,
            UiState.NoReduction => Color.DarkGoldenrod,
            UiState.Cancelled => Color.DimGray,
            UiState.Error => Color.Firebrick,
            _ => SystemColors.ControlText
        };

        lblResultOriginal.Text = $"Tamanho original: {(_viewModel.LastResult != null ? TargetSizeUnitExtensions.FormatBytes(_viewModel.LastResult.OriginalSizeBytes) : "-")}";
        lblResultFinal.Text = $"Tamanho final: {_viewModel.FinalFileSizeText}";
        lblResultReduction.Text = $"Redução obtida: {_viewModel.ReductionPercentageText}";
        lblResultDpi.Text = $"Qualidade aplicada: {_viewModel.FinalDpiText}";
        lblResultOutput.Text = $"Arquivo gerado: {_viewModel.OutputFilePathText}";

        btnOpenPdf.Enabled = _viewModel.CanOpenPdf;
        btnOpenFolder.Enabled = _viewModel.CanOpenFolder;
        btnReset.Enabled = _viewModel.State is not (UiState.Compressing or UiState.Cancelling);

        _isUpdatingControls = false;
    }
}
