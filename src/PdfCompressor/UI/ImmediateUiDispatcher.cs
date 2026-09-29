namespace PdfCompressor.UI;

/// <summary>
/// Implementação imediata de despachante que executa as ações de forma síncrona na thread corrente.
/// Ideal para testes automatizados unitários e ambientes desacoplados de loop de mensagens.
/// </summary>
public sealed class ImmediateUiDispatcher : IUiDispatcher
{
    /// <summary>
    /// Instância compartilhada singleton do despachante imediato.
    /// </summary>
    public static readonly ImmediateUiDispatcher Instance = new();

    /// <inheritdoc />
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
    }
}
