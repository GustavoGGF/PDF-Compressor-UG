using System.Globalization;
using PdfCompressor.Models;
using PdfCompressor.UI;
using Xunit;

namespace PdfCompressor.Tests.UI;

public sealed class MainFormViewModelTests
{
    private readonly FakePdfAnalyzerService _analyzer = new();
    private readonly FakeCompressionEngineService _engine = new();
    private readonly FakeFileManagerService _fileManager = new();
    private readonly FakeFileLauncherService _launcher = new();
    private readonly FakeDiagnosticLogger _logger = new();

    private MainFormViewModel CreateViewModel(CultureInfo? culture = null) =>
        new(_analyzer, _engine, _fileManager, _launcher, _logger, ImmediateUiDispatcher.Instance, culture);

    [Fact]
    public void InitialState_IsIdle_AndControlsAreConfiguredCorrectly()
    {
        using var vm = CreateViewModel();

        Assert.Equal(UiState.Idle, vm.State);
        Assert.True(vm.CanSelectFile);
        Assert.False(vm.CanCompress);
        Assert.False(vm.CanCancel);
        Assert.False(vm.CanOpenPdf);
        Assert.False(vm.CanOpenFolder);
        Assert.False(vm.IsProgressVisible);
        Assert.False(vm.IsPresetEnabled);
        Assert.False(vm.IsTargetInputEnabled);
        Assert.Null(vm.SelectedFilePath);
        Assert.Equal("Nenhum arquivo selecionado", vm.SelectedFileName);
        Assert.Equal("-", vm.OriginalFileSizeText);
        Assert.Equal("-", vm.PageCountText);
    }

    [Fact]
    public void TryValidateFilePath_ValidatesExtensionDirectoryAndExistence()
    {
        Assert.False(MainFormViewModel.TryValidateFilePath(null, out string? err1));
        Assert.Equal("O caminho do arquivo não pode ser vazio.", err1);

        string tempDir = Path.GetTempPath();
        Assert.False(MainFormViewModel.TryValidateFilePath(tempDir, out string? err2));
        Assert.Equal("Diretórios não são suportados. Selecione um arquivo PDF individual.", err2);

        string nonPdf = Path.Combine(tempDir, "arquivo.docx");
        Assert.False(MainFormViewModel.TryValidateFilePath(nonPdf, out string? err3));
        Assert.Equal("O arquivo selecionado não possui a extensão .pdf.", err3);

        string nonExistent = Path.Combine(tempDir, "inexistente_12345.pdf");
        Assert.False(MainFormViewModel.TryValidateFilePath(nonExistent, out string? err4));
        Assert.Equal("O arquivo selecionado não foi encontrado no sistema.", err4);

        string validPdf = Path.Combine(tempDir, "valido.pdf");
        try
        {
            File.WriteAllText(validPdf, "%PDF-1.4 dummy");
            Assert.True(MainFormViewModel.TryValidateFilePath(validPdf, out string? err5));
            Assert.Null(err5);
        }
        finally
        {
            if (File.Exists(validPdf)) File.Delete(validPdf);
        }
    }

    [Fact]
    public async Task SelectAndAnalyzeFileAsync_WithValidPdf_TransitionsIdleToAnalyzingToReady()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 valid test file");
            _analyzer.ConfiguredResult = PdfInfo.Success(tempFile, 1_500_000, 3);

            var stateTransitions = new List<UiState>();
            vm.StateChanged += (_, s) => stateTransitions.Add(s);

            await vm.SelectAndAnalyzeFileAsync(tempFile);

            Assert.Equal(UiState.Ready, vm.State);
            Assert.Contains(UiState.Analyzing, stateTransitions);
            Assert.Contains(UiState.Ready, stateTransitions);
            Assert.Equal(tempFile, vm.SelectedFilePath);
            Assert.Equal(Path.GetFileName(tempFile), vm.SelectedFileName);
            Assert.Equal("1,50 MB", vm.OriginalFileSizeText);
            Assert.Equal("3 páginas", vm.PageCountText);
            Assert.False(vm.HasSignatureWarning);
            Assert.Null(vm.SignatureWarningText);
            Assert.True(vm.CanCompress);
            Assert.True(vm.IsPresetEnabled);
            Assert.True(vm.IsTargetInputEnabled);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SelectAndAnalyzeFileAsync_WithSignedPdf_DisplaysSignatureWarningAndAllowsCompression()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_signed_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 signed test file");
            _analyzer.ConfiguredResult = PdfInfo.Warning(
                tempFile,
                2_000_000,
                5,
                hasLikelySignature: true,
                warningMessage: PdfInfo.DefaultSignatureWarningMessage
            );

            await vm.SelectAndAnalyzeFileAsync(tempFile);

            Assert.Equal(UiState.Ready, vm.State);
            Assert.True(vm.HasSignatureWarning);
            Assert.Equal(PdfInfo.DefaultSignatureWarningMessage, vm.SignatureWarningText);
            Assert.True(vm.CanCompress);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task SelectAndAnalyzeFileAsync_WithInvalidOrProtectedPdf_TransitionsToError()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_encrypted_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 encrypted dummy");
            _analyzer.ConfiguredResult = PdfInfo.Failed(
                tempFile,
                PdfInfo.PasswordProtectedMessage,
                fileSizeBytes: 50_000,
                isEncrypted: true
            );

            await vm.SelectAndAnalyzeFileAsync(tempFile);

            Assert.Equal(UiState.Error, vm.State);
            Assert.Equal(PdfInfo.PasswordProtectedMessage, vm.ErrorMessage);
            Assert.False(vm.CanCompress);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task UpdatePreset_SwitchesBetweenAutomaticAndManualDpi()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_preset_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 preset test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            Assert.Equal(CompressionPreset.Automatic, vm.SelectedPreset);
            Assert.True(vm.IsTargetInputEnabled);

            vm.UpdatePreset(CompressionPreset.HighQuality);
            Assert.Equal(CompressionPreset.HighQuality, vm.SelectedPreset);
            Assert.False(vm.IsTargetInputEnabled);
            Assert.True(vm.CanCompress);

            vm.UpdatePreset(CompressionPreset.Automatic);
            Assert.Equal(CompressionPreset.Automatic, vm.SelectedPreset);
            Assert.True(vm.IsTargetInputEnabled);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task UpdateTargetSize_WhenInvalid_DisablesCanCompress()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_val_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 validation test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            Assert.True(vm.CanCompress);

            vm.UpdateTargetSize("0", TargetSizeUnit.MB);
            Assert.False(vm.IsTargetSizeValid);
            Assert.False(vm.CanCompress);
            Assert.Equal(TargetSizeValidator.MustBeGreaterThanZeroMessage, vm.TargetValidationError);

            vm.UpdateTargetSize("1,5", TargetSizeUnit.MB);
            Assert.True(vm.IsTargetSizeValid);
            Assert.True(vm.CanCompress);
            Assert.Null(vm.TargetValidationError);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CompressAsync_WhenTargetMet_TransitionsReadyToCompressingToSuccess()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_compress_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 compress test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            string outFile = Path.Combine(Path.GetTempPath(), "teste_compress_compactado.pdf");
            _engine.ConfiguredResult = new CompressionResult(
                Status: CompressionStatus.TargetMet,
                SourceFilePath: tempFile,
                OutputFilePath: outFile,
                OriginalSizeBytes: 10_000_000,
                FinalSizeBytes: 2_000_000,
                FinalDpi: 150,
                Attempts: [],
                TotalDuration: TimeSpan.FromSeconds(2)
            );

            var stateTransitions = new List<UiState>();
            vm.StateChanged += (_, s) => stateTransitions.Add(s);

            await vm.CompressAsync();

            Assert.Equal(UiState.Success, vm.State);
            Assert.Contains(UiState.Compressing, stateTransitions);
            Assert.Contains(UiState.Success, stateTransitions);
            Assert.Equal(100, vm.ProgressPercentage);
            Assert.Equal("2,00 MB", vm.FinalFileSizeText);
            Assert.Equal("80,0%", vm.ReductionPercentageText);
            Assert.Equal("150 DPI", vm.FinalDpiText);
            Assert.Equal(outFile, vm.OutputFilePathText);
            Assert.True(vm.CanOpenPdf);
            Assert.True(vm.CanOpenFolder);
            Assert.False(vm.CanCancel);
            Assert.True(vm.CanCompress);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CompressAsync_WhenBestEffortAboveTarget_TransitionsToBestEffort()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_besteffort_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 best effort test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            string outFile = Path.Combine(Path.GetTempPath(), "teste_besteffort_compactado.pdf");
            _engine.ConfiguredResult = new CompressionResult(
                Status: CompressionStatus.BestEffortAboveTarget,
                SourceFilePath: tempFile,
                OutputFilePath: outFile,
                OriginalSizeBytes: 5_000_000,
                FinalSizeBytes: 3_000_000,
                FinalDpi: 72,
                Attempts: [],
                TotalDuration: TimeSpan.FromSeconds(3),
                Message: "Nenhuma tentativa atingiu o tamanho-alvo de 1,00 MB. O melhor resultado obtido (3,00 MB a 72 DPI) foi preservado."
            );

            await vm.CompressAsync();

            Assert.Equal(UiState.BestEffort, vm.State);
            Assert.Equal(100, vm.ProgressPercentage);
            Assert.True(vm.CanOpenPdf);
            Assert.True(vm.CanOpenFolder);
            Assert.Contains("melhor esforço", vm.ResultTitleText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task CompressAsync_WhenToolUnavailable_TransitionsToErrorWithClearMessage()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_notool_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 no tool test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            _engine.ConfiguredResult = new CompressionResult(
                Status: CompressionStatus.ToolUnavailable,
                SourceFilePath: tempFile,
                OutputFilePath: null,
                OriginalSizeBytes: 1_000_000,
                FinalSizeBytes: 0,
                FinalDpi: null,
                Attempts: [],
                TotalDuration: TimeSpan.Zero,
                Message: "O Ghostscript (gswin64c.exe) não foi encontrado no sistema."
            );

            await vm.CompressAsync();

            Assert.Equal(UiState.Error, vm.State);
            Assert.Equal("O Ghostscript (gswin64c.exe) não foi encontrado no sistema.", vm.ErrorMessage);
            Assert.False(vm.CanOpenPdf);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Cancel_WhenCompressing_CancelsEngineAndTransitionsToCancelled()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_cancel_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 cancel test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            _engine.Delay = TimeSpan.FromMilliseconds(500);

            var compressTask = vm.CompressAsync();
            Assert.Equal(UiState.Compressing, vm.State);
            Assert.True(vm.CanCancel);

            vm.Cancel();
            Assert.Equal(UiState.Cancelling, vm.State);
            Assert.False(vm.CanCancel);

            await compressTask;

            Assert.Equal(UiState.Cancelled, vm.State);
            Assert.False(vm.CanOpenPdf);
            Assert.True(vm.CanSelectFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task Reset_FromTerminalState_RestoresIdleStateSafely()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_reset_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 reset test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);
            await vm.CompressAsync();

            Assert.Equal(UiState.Success, vm.State);

            vm.Reset();

            Assert.Equal(UiState.Idle, vm.State);
            Assert.Null(vm.SelectedFilePath);
            Assert.Null(vm.CurrentPdfInfo);
            Assert.Null(vm.LastResult);
            Assert.False(vm.CanOpenPdf);
            Assert.False(vm.CanCompress);
            Assert.True(vm.CanSelectFile);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task OpenResultPdf_And_OpenResultFolder_CallLauncherService()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_open_{Guid.NewGuid():N}.pdf");
        string outFile = Path.Combine(Path.GetTempPath(), "saida_open.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 open test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            _engine.ConfiguredResult = new CompressionResult(
                Status: CompressionStatus.TargetMet,
                SourceFilePath: tempFile,
                OutputFilePath: outFile,
                OriginalSizeBytes: 1_000_000,
                FinalSizeBytes: 500_000,
                FinalDpi: 150,
                Attempts: [],
                TotalDuration: TimeSpan.FromSeconds(1)
            );

            await vm.CompressAsync();

            bool openPdfResult = vm.OpenResultPdf();
            bool openFolderResult = vm.OpenResultFolder();

            Assert.True(openPdfResult);
            Assert.True(openFolderResult);
            Assert.Contains(outFile, _launcher.OpenedPdfs);
            Assert.Contains(outFile, _launcher.OpenedFolders);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ProgressUpdates_AreReportedDuringCompression()
    {
        using var vm = CreateViewModel();
        string tempFile = Path.Combine(Path.GetTempPath(), $"teste_prog_{Guid.NewGuid():N}.pdf");

        try
        {
            await File.WriteAllTextAsync(tempFile, "%PDF-1.4 progress test");
            await vm.SelectAndAnalyzeFileAsync(tempFile);

            _engine.ReportSteps = true;
            await vm.CompressAsync();

            // Ao finalizar, o progresso atinge 100% no sucesso
            Assert.Equal(100, vm.ProgressPercentage);
            Assert.Equal("Concluído com sucesso.", vm.ProgressText);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
