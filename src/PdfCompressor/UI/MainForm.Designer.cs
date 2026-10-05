#nullable enable
using System.Drawing;
using System.Windows.Forms;

namespace PdfCompressor.UI;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    private GroupBox grpFileSelection = null!;
    private Button btnSelectFile = null!;
    private Label lblDropZone = null!;
    private TextBox txtFilePath = null!;
    private Label lblOriginalSize = null!;
    private Label lblPageCount = null!;
    private Panel pnlSignatureWarning = null!;
    private Label lblSignatureWarning = null!;

    private GroupBox grpSettings = null!;
    private Label lblPreset = null!;
    private ComboBox cmbPreset = null!;
    private Label lblTargetSize = null!;
    private TextBox txtTargetSize = null!;
    private ComboBox cmbTargetUnit = null!;
    private Label lblTargetError = null!;

    private GroupBox grpProcessing = null!;
    private Button btnCompress = null!;
    private Button btnCancel = null!;
    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private Label lblProgressDetails = null!;

    private GroupBox grpResult = null!;
    private Label lblResultBanner = null!;
    private Label lblResultOriginal = null!;
    private Label lblResultFinal = null!;
    private Label lblResultReduction = null!;
    private Label lblResultDpi = null!;
    private Label lblResultOutput = null!;
    private Button btnOpenPdf = null!;
    private Button btnOpenFolder = null!;
    private Button btnReset = null!;

    private OpenFileDialog openFileDialog = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _viewModel.Dispose();
            components?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.grpFileSelection = new GroupBox();
        this.btnSelectFile = new Button();
        this.lblDropZone = new Label();
        this.txtFilePath = new TextBox();
        this.lblOriginalSize = new Label();
        this.lblPageCount = new Label();
        this.pnlSignatureWarning = new Panel();
        this.lblSignatureWarning = new Label();

        this.grpSettings = new GroupBox();
        this.lblPreset = new Label();
        this.cmbPreset = new ComboBox();
        this.lblTargetSize = new Label();
        this.txtTargetSize = new TextBox();
        this.cmbTargetUnit = new ComboBox();
        this.lblTargetError = new Label();

        this.grpProcessing = new GroupBox();
        this.btnCompress = new Button();
        this.btnCancel = new Button();
        this.progressBar = new ProgressBar();
        this.lblStatus = new Label();
        this.lblProgressDetails = new Label();

        this.grpResult = new GroupBox();
        this.lblResultBanner = new Label();
        this.lblResultOriginal = new Label();
        this.lblResultFinal = new Label();
        this.lblResultReduction = new Label();
        this.lblResultDpi = new Label();
        this.lblResultOutput = new Label();
        this.btnOpenPdf = new Button();
        this.btnOpenFolder = new Button();
        this.btnReset = new Button();

        this.openFileDialog = new OpenFileDialog();

        this.grpFileSelection.SuspendLayout();
        this.pnlSignatureWarning.SuspendLayout();
        this.grpSettings.SuspendLayout();
        this.grpProcessing.SuspendLayout();
        this.grpResult.SuspendLayout();
        this.SuspendLayout();

        // 1. Seleção de Arquivo
        this.grpFileSelection.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.grpFileSelection.Controls.AddRange(new Control[] { btnSelectFile, lblDropZone, txtFilePath, lblOriginalSize, lblPageCount, pnlSignatureWarning });
        this.grpFileSelection.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpFileSelection.Location = new Point(12, 12);
        this.grpFileSelection.Size = new Size(720, 160);
        this.grpFileSelection.TabIndex = 0;
        this.grpFileSelection.TabStop = false;
        this.grpFileSelection.Text = "1. Seleção do Arquivo PDF";

        this.btnSelectFile.Font = new Font("Segoe UI", 9F);
        this.btnSelectFile.Location = new Point(16, 26);
        this.btnSelectFile.Size = new Size(140, 32);
        this.btnSelectFile.TabIndex = 0;
        this.btnSelectFile.Text = "&Selecionar PDF...";
        this.btnSelectFile.UseVisualStyleBackColor = true;

        this.lblDropZone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.lblDropZone.BorderStyle = BorderStyle.FixedSingle;
        this.lblDropZone.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
        this.lblDropZone.ForeColor = SystemColors.GrayText;
        this.lblDropZone.Location = new Point(166, 26);
        this.lblDropZone.Size = new Size(538, 32);
        this.lblDropZone.TabIndex = 1;
        this.lblDropZone.Text = "ou arraste o arquivo PDF e solte aqui";
        this.lblDropZone.TextAlign = ContentAlignment.MiddleCenter;

        this.txtFilePath.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.txtFilePath.Font = new Font("Segoe UI", 9F);
        this.txtFilePath.Location = new Point(16, 66);
        this.txtFilePath.ReadOnly = true;
        this.txtFilePath.Size = new Size(688, 23);
        this.txtFilePath.TabIndex = 2;
        this.txtFilePath.Text = "Nenhum arquivo selecionado";

        this.lblOriginalSize.Font = new Font("Segoe UI", 9F);
        this.lblOriginalSize.Location = new Point(16, 96);
        this.lblOriginalSize.Size = new Size(260, 20);
        this.lblOriginalSize.TabIndex = 3;
        this.lblOriginalSize.Text = "Tamanho original: -";

        this.lblPageCount.Font = new Font("Segoe UI", 9F);
        this.lblPageCount.Location = new Point(290, 96);
        this.lblPageCount.Size = new Size(200, 20);
        this.lblPageCount.TabIndex = 4;
        this.lblPageCount.Text = "Páginas: -";

        this.pnlSignatureWarning.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.pnlSignatureWarning.BackColor = Color.FromArgb(255, 250, 205);
        this.pnlSignatureWarning.BorderStyle = BorderStyle.FixedSingle;
        this.pnlSignatureWarning.Controls.Add(lblSignatureWarning);
        this.pnlSignatureWarning.Location = new Point(16, 120);
        this.pnlSignatureWarning.Size = new Size(688, 28);
        this.pnlSignatureWarning.TabIndex = 5;
        this.pnlSignatureWarning.Visible = false;

        this.lblSignatureWarning.Dock = DockStyle.Fill;
        this.lblSignatureWarning.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        this.lblSignatureWarning.ForeColor = Color.DarkGoldenrod;
        this.lblSignatureWarning.Size = new Size(686, 26);
        this.lblSignatureWarning.Text = "⚠ Assinatura detectada: a compressão pode invalidá-la. O arquivo original não será alterado.";
        this.lblSignatureWarning.TextAlign = ContentAlignment.MiddleLeft;

        // 2. Configurações de Compressão
        this.grpSettings.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.grpSettings.Controls.AddRange(new Control[] { lblPreset, cmbPreset, lblTargetSize, txtTargetSize, cmbTargetUnit, lblTargetError });
        this.grpSettings.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpSettings.Location = new Point(12, 178);
        this.grpSettings.Size = new Size(720, 100);
        this.grpSettings.TabIndex = 1;
        this.grpSettings.TabStop = false;
        this.grpSettings.Text = "2. Configurações de Compressão";

        this.lblPreset.Font = new Font("Segoe UI", 9F);
        this.lblPreset.Location = new Point(16, 26);
        this.lblPreset.Size = new Size(140, 20);
        this.lblPreset.TabIndex = 0;
        this.lblPreset.Text = "&Modo de compressão:";

        this.cmbPreset.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cmbPreset.Font = new Font("Segoe UI", 9F);
        this.cmbPreset.Location = new Point(16, 48);
        this.cmbPreset.Size = new Size(320, 23);
        this.cmbPreset.TabIndex = 1;

        this.lblTargetSize.Font = new Font("Segoe UI", 9F);
        this.lblTargetSize.Location = new Point(360, 26);
        this.lblTargetSize.Size = new Size(120, 20);
        this.lblTargetSize.TabIndex = 2;
        this.lblTargetSize.Text = "&Tamanho-alvo:";

        this.txtTargetSize.Font = new Font("Segoe UI", 9F);
        this.txtTargetSize.Location = new Point(360, 48);
        this.txtTargetSize.Size = new Size(90, 23);
        this.txtTargetSize.TabIndex = 3;
        this.txtTargetSize.Text = "2,0";

        this.cmbTargetUnit.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cmbTargetUnit.Font = new Font("Segoe UI", 9F);
        this.cmbTargetUnit.Location = new Point(458, 48);
        this.cmbTargetUnit.Size = new Size(65, 23);
        this.cmbTargetUnit.TabIndex = 4;

        this.lblTargetError.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.lblTargetError.Font = new Font("Segoe UI", 8.5F);
        this.lblTargetError.ForeColor = Color.Crimson;
        this.lblTargetError.Location = new Point(16, 75);
        this.lblTargetError.Size = new Size(688, 18);
        this.lblTargetError.TabIndex = 5;
        this.lblTargetError.Visible = false;

        // 3. Processamento
        this.grpProcessing.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.grpProcessing.Controls.AddRange(new Control[] { btnCompress, btnCancel, progressBar, lblStatus, lblProgressDetails });
        this.grpProcessing.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpProcessing.Location = new Point(12, 284);
        this.grpProcessing.Size = new Size(720, 130);
        this.grpProcessing.TabIndex = 2;
        this.grpProcessing.TabStop = false;
        this.grpProcessing.Text = "3. Processamento";

        this.btnCompress.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        this.btnCompress.Location = new Point(16, 26);
        this.btnCompress.Size = new Size(150, 34);
        this.btnCompress.TabIndex = 0;
        this.btnCompress.Text = "&Comprimir PDF";
        this.btnCompress.UseVisualStyleBackColor = true;

        this.btnCancel.Enabled = false;
        this.btnCancel.Font = new Font("Segoe UI", 9F);
        this.btnCancel.Location = new Point(176, 26);
        this.btnCancel.Size = new Size(110, 34);
        this.btnCancel.TabIndex = 1;
        this.btnCancel.Text = "Ca&ncelar";
        this.btnCancel.UseVisualStyleBackColor = true;

        this.progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.progressBar.Location = new Point(16, 68);
        this.progressBar.Size = new Size(688, 22);
        this.progressBar.TabIndex = 2;

        this.lblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.lblStatus.Font = new Font("Segoe UI", 9F);
        this.lblStatus.Location = new Point(16, 96);
        this.lblStatus.Size = new Size(420, 22);
        this.lblStatus.TabIndex = 3;
        this.lblStatus.Text = "Aguardando seleção de arquivo PDF...";

        this.lblProgressDetails.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        this.lblProgressDetails.Font = new Font("Segoe UI", 8.5F);
        this.lblProgressDetails.ForeColor = SystemColors.GrayText;
        this.lblProgressDetails.Location = new Point(442, 96);
        this.lblProgressDetails.Size = new Size(262, 22);
        this.lblProgressDetails.TabIndex = 4;
        this.lblProgressDetails.TextAlign = ContentAlignment.MiddleRight;

        // 4. Resultado da Compressão
        // O conteúdo permanece em uma área rolável quando a janela fica menor que o fluxo vertical.
        // Ancorar este grupo ao fundo fazia os botões serem cortados durante o redimensionamento.
        this.grpResult.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.grpResult.Controls.AddRange(new Control[] { lblResultBanner, lblResultOriginal, lblResultFinal, lblResultReduction, lblResultDpi, lblResultOutput, btnOpenPdf, btnOpenFolder, btnReset });
        this.grpResult.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpResult.Location = new Point(12, 420);
        this.grpResult.Size = new Size(720, 190);
        this.grpResult.TabIndex = 3;
        this.grpResult.TabStop = false;
        this.grpResult.Text = "4. Resultado da Compressão";

        this.lblResultBanner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.lblResultBanner.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        this.lblResultBanner.Location = new Point(16, 26);
        this.lblResultBanner.Size = new Size(688, 22);
        this.lblResultBanner.TabIndex = 0;
        this.lblResultBanner.Text = "Nenhuma operação realizada ainda.";

        this.lblResultOriginal.Font = new Font("Segoe UI", 9F);
        this.lblResultOriginal.Location = new Point(16, 55);
        this.lblResultOriginal.Size = new Size(220, 20);
        this.lblResultOriginal.TabIndex = 1;
        this.lblResultOriginal.Text = "Tamanho original: -";

        this.lblResultFinal.Font = new Font("Segoe UI", 9F);
        this.lblResultFinal.Location = new Point(246, 55);
        this.lblResultFinal.Size = new Size(220, 20);
        this.lblResultFinal.TabIndex = 2;
        this.lblResultFinal.Text = "Tamanho final: -";

        this.lblResultReduction.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.lblResultReduction.Location = new Point(476, 55);
        this.lblResultReduction.Size = new Size(220, 20);
        this.lblResultReduction.TabIndex = 3;
        this.lblResultReduction.Text = "Redução obtida: -";

        this.lblResultDpi.Font = new Font("Segoe UI", 9F);
        this.lblResultDpi.Location = new Point(16, 80);
        this.lblResultDpi.Size = new Size(220, 20);
        this.lblResultDpi.TabIndex = 4;
        this.lblResultDpi.Text = "Qualidade aplicada: -";

        this.lblResultOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.lblResultOutput.Font = new Font("Segoe UI", 9F);
        this.lblResultOutput.Location = new Point(16, 105);
        this.lblResultOutput.Size = new Size(688, 20);
        this.lblResultOutput.TabIndex = 5;
        this.lblResultOutput.Text = "Arquivo gerado: -";

        this.btnOpenPdf.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.btnOpenPdf.Enabled = false;
        this.btnOpenPdf.Font = new Font("Segoe UI", 9F);
        this.btnOpenPdf.Location = new Point(16, 142);
        this.btnOpenPdf.Size = new Size(120, 34);
        this.btnOpenPdf.TabIndex = 6;
        this.btnOpenPdf.Text = "&Abrir PDF";
        this.btnOpenPdf.UseVisualStyleBackColor = true;

        this.btnOpenFolder.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        this.btnOpenFolder.Enabled = false;
        this.btnOpenFolder.Font = new Font("Segoe UI", 9F);
        this.btnOpenFolder.Location = new Point(146, 142);
        this.btnOpenFolder.Size = new Size(120, 34);
        this.btnOpenFolder.TabIndex = 7;
        this.btnOpenFolder.Text = "Abrir &Pasta";
        this.btnOpenFolder.UseVisualStyleBackColor = true;

        this.btnReset.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        this.btnReset.Font = new Font("Segoe UI", 9F);
        this.btnReset.Location = new Point(584, 142);
        this.btnReset.Size = new Size(120, 34);
        this.btnReset.TabIndex = 8;
        this.btnReset.Text = "&Novo Arquivo";
        this.btnReset.UseVisualStyleBackColor = true;

        this.openFileDialog.Filter = "Arquivos PDF (*.pdf)|*.pdf|Todos os arquivos (*.*)|*.*";
        this.openFileDialog.Title = "Selecionar arquivo PDF para compressão";

        // MainForm Form Base
        this.AllowDrop = true;
        this.AutoScroll = true;
        this.AutoScrollMinSize = MainFormLayout.RequiredContentSize;
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(744, 621);
        this.Controls.AddRange(new Control[] { grpResult, grpProcessing, grpSettings, grpFileSelection });
        this.Font = new Font("Segoe UI", 9F);
        this.MinimumSize = new Size(680, 580);
        this.Name = "MainForm";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = "PDF Compressor";

        this.grpFileSelection.ResumeLayout(false);
        this.grpFileSelection.PerformLayout();
        this.pnlSignatureWarning.ResumeLayout(false);
        this.grpSettings.ResumeLayout(false);
        this.grpSettings.PerformLayout();
        this.grpProcessing.ResumeLayout(false);
        this.grpResult.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
