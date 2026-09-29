using Microsoft.Extensions.DependencyInjection;
using PdfCompressor.Services;

namespace PdfCompressor.Infrastructure;

/// <summary>
/// Provedor tipado de serviços e raiz de composição da aplicação.
/// Permite injeção de fakes em testes e desacoplamento total da camada visual.
/// </summary>
public sealed class AppServiceContainer
{
    private readonly IServiceProvider _serviceProvider;

    public AppServiceContainer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Cria uma instância do contêiner com as implementações padrão.
    /// </summary>
    public static AppServiceContainer CreateDefault()
    {
        var services = new ServiceCollection();
        services.AddPdfCompressorCoreServices();
        return new AppServiceContainer(services.BuildServiceProvider());
    }

    /// <summary>
    /// Cria uma instância do contêiner permitindo customização ou injeção de fakes para testes.
    /// </summary>
    public static AppServiceContainer CreateWithCustomServices(Action<IServiceCollection> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var services = new ServiceCollection();
        services.AddPdfCompressorCoreServices();
        configure(services);
        return new AppServiceContainer(services.BuildServiceProvider());
    }

    public IServiceProvider ServiceProvider => _serviceProvider;

    public IFileManagerService FileManager => _serviceProvider.GetRequiredService<IFileManagerService>();
    public IDiagnosticLogger Logger => _serviceProvider.GetRequiredService<IDiagnosticLogger>();
    public IFileLauncherService FileLauncher => _serviceProvider.GetRequiredService<IFileLauncherService>();
    public IPdfAnalyzerService PdfAnalyzer => _serviceProvider.GetRequiredService<IPdfAnalyzerService>();
    public IGhostscriptLocator? GhostscriptLocator => _serviceProvider.GetService<IGhostscriptLocator>();
    public IGhostscriptProcessRunner? ProcessRunner => _serviceProvider.GetService<IGhostscriptProcessRunner>();
    public ICompressionEngine? CompressionEngine => _serviceProvider.GetService<ICompressionEngine>();
}
