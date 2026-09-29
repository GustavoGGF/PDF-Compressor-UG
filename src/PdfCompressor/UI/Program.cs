using System.Windows.Forms;
using PdfCompressor.Infrastructure;

namespace PdfCompressor.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var container = AppServiceContainer.CreateDefault();
        container.Logger.LogInfo("Aplicação iniciada com sucesso.");

        Application.Run(new MainForm(container));
    }
}
