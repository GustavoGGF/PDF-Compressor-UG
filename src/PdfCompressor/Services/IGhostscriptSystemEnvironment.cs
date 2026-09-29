namespace PdfCompressor.Services;

/// <summary>
/// Abstração do ambiente do sistema operacional (arquivos, variáveis e registro) para localização do Ghostscript.
/// Permite testes unitários determinísticos em qualquer sistema operacional.
/// </summary>
public interface IGhostscriptSystemEnvironment
{
    /// <summary>
    /// Indica se o sistema operacional corrente é Windows.
    /// </summary>
    bool IsWindows { get; }

    /// <summary>
    /// Verifica se o arquivo existe no caminho informado.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Verifica se o diretório existe no caminho informado.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Lista subdiretórios correspondentes ao padrão de busca.
    /// </summary>
    IEnumerable<string> GetDirectories(string path, string searchPattern);

    /// <summary>
    /// Obtém o valor de uma variável de ambiente.
    /// </summary>
    string? GetEnvironmentVariable(string variableName);

    /// <summary>
    /// Obtém caminhos de instalação registrados no Registro do Windows (HKLM/HKCU).
    /// </summary>
    IEnumerable<string> GetRegistryInstallPaths();

    /// <summary>
    /// Executa o executável fornecido solicitando sua versão (--version ou -v) e retorna a saída textual.
    /// </summary>
    string? GetProcessVersionOutput(string executablePath);
}
