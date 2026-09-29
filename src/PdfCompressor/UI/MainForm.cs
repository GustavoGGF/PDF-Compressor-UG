using System.Windows.Forms;
using PdfCompressor.Infrastructure;

namespace PdfCompressor.UI;

/// <summary>
/// Formulário principal do PDF Compressor.
/// Responsável exclusivamente pela orquestração visual e transição de estados.
/// </summary>
public partial class MainForm : Form
{
    private readonly AppServiceContainer _services;

    public MainForm(AppServiceContainer services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        InitializeComponent();
    }
}
