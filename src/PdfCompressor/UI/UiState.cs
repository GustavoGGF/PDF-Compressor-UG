namespace PdfCompressor.UI;

/// <summary>
/// Representa os estados explícitos do ciclo de vida da interface gráfica do PDF Compressor.
/// Garante controle previsível de habilitação de componentes, textos informativos e ações permitidas.
/// </summary>
public enum UiState
{
    /// <summary>
    /// Estado inicial de repouso. Aguarda a seleção de um arquivo PDF pelo usuário.
    /// </summary>
    Idle,

    /// <summary>
    /// Análise assíncrona do arquivo selecionado em andamento (leitura de tamanho, páginas e marcadores de assinatura).
    /// </summary>
    Analyzing,

    /// <summary>
    /// Arquivo analisado com sucesso. Parâmetros de compressão editáveis e pronto para iniciar o processamento.
    /// </summary>
    Ready,

    /// <summary>
    /// Processo de compressão em andamento com tentativas ativas de qualidade e progresso reportado.
    /// </summary>
    Compressing,

    /// <summary>
    /// Usuário solicitou o cancelamento da compressão. Aguardando encerramento do processo e limpeza de temporários.
    /// </summary>
    Cancelling,

    /// <summary>
    /// Compressão finalizada com sucesso pleno (alvo de tamanho alcançado conforme solicitado).
    /// </summary>
    Success,

    /// <summary>
    /// Compressão finalizada com sucesso parcial (melhor esforço). O alvo não foi alcançado, mas o menor arquivo foi mantido.
    /// </summary>
    BestEffort,

    /// <summary>
    /// Nenhuma tentativa reduziu o arquivo original; nenhum arquivo novo foi gerado.
    /// </summary>
    NoReduction,

    /// <summary>
    /// Operação cancelada com segurança pelo usuário, com temporários limpos e arquivo original intacto.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Ocorreu uma falha na análise, validação de entrada, dependência ausente ou erro do motor Ghostscript.
    /// </summary>
    Error
}
