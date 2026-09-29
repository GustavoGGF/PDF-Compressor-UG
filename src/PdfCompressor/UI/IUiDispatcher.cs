namespace PdfCompressor.UI;

/// <summary>
/// Abstração para envio de ações de atualização para a thread correta da interface do usuário.
/// Desacopla a orquestração de segundo plano do runtime específico do Windows Forms.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Executa uma ação de forma síncrona na thread da interface.
    /// </summary>
    /// <param name="action">Ação a ser executada.</param>
    void Invoke(Action action);

    /// <summary>
    /// Enfileira uma ação de forma assíncrona para execução na thread da interface.
    /// </summary>
    /// <param name="action">Ação a ser executada.</param>
    void Post(Action action);
}
