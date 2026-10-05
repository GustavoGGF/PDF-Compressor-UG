using System.Drawing;

namespace PdfCompressor.UI;

/// <summary>
/// Define as dimensões mínimas do conteúdo rolável do formulário principal.
/// </summary>
internal static class MainFormLayout
{
    /// <summary>
    /// Obtém a altura necessária para manter todos os grupos e seus botões acessíveis.
    /// </summary>
    internal static Size RequiredContentSize => new(0, 610);
}
