using System.Drawing;
using PdfCompressor.UI;
using Xunit;

namespace PdfCompressor.Tests.UI;

/// <summary>
/// Verifica os limites de layout usados pelo formulário principal.
/// </summary>
public sealed class MainFormLayoutTests
{
    /// <summary>
    /// Garante que a área rolável cubra a pilha vertical completa do formulário.
    /// </summary>
    [Fact]
    public void RequiredContentSize_CoversTheCompleteVerticalFormLayout()
    {
        Assert.Equal(new Size(0, 610), MainFormLayout.RequiredContentSize);
    }
}
