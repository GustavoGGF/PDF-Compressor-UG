namespace PdfCompressor.Services;

/// <summary>
/// Fornece operações seguras para abertura de arquivos PDF e visualização de pastas no sistema operacional.
/// Valida a existência física prévia e protege contra chamadas com caminhos maliciosos ou inexistentes.
/// </summary>
public interface IFileLauncherService
{
    /// <summary>
    /// Abre o arquivo PDF especificado no visualizador padrão registrado no sistema operacional.
    /// Valida que o arquivo existe fisicamente e possui extensão .pdf antes de tentar a abertura.
    /// </summary>
    /// <param name="filePath">Caminho completo do arquivo PDF a ser aberto.</param>
    /// <returns>True se o visualizador foi iniciado com sucesso; false se o arquivo não existe ou a inicialização falhou.</returns>
    bool OpenPdf(string filePath);

    /// <summary>
    /// Abre o gerenciador de arquivos do sistema operacional (Windows Explorer no Windows) na pasta do arquivo,
    /// destacando o arquivo gerado quando suportado.
    /// Valida que a pasta ou o arquivo existe antes de tentar a abertura.
    /// </summary>
    /// <param name="filePath">Caminho do arquivo cuja pasta deve ser aberta.</param>
    /// <returns>True se o gerenciador de arquivos foi iniciado com sucesso; false caso contrário.</returns>
    bool OpenFolderContainingFile(string filePath);
}
