using Microsoft.Extensions.DependencyInjection;
using PdfCompressor.Services;

namespace PdfCompressor.Infrastructure;

/// <summary>
/// Extensões para registro e composição de dependências da aplicação.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra os serviços fundamentais do PDF Compressor na coleção de serviços.
    /// </summary>
    public static IServiceCollection AddPdfCompressorCoreServices(this IServiceCollection services)
    {
        services.AddSingleton<IFileManagerService, FileManagerService>();
        services.AddSingleton<IDiagnosticLogger, DiagnosticLogger>();
        services.AddSingleton<IPdfAnalyzerService, PdfAnalyzerService>();
        services.AddSingleton<IGhostscriptSystemEnvironment, DefaultGhostscriptSystemEnvironment>();
        services.AddSingleton<IGhostscriptLocator, GhostscriptLocator>();
        services.AddSingleton<IGhostscriptProcessStarter, DefaultGhostscriptProcessStarter>();
        services.AddSingleton<IGhostscriptProcessRunner, GhostscriptProcessRunner>();
        return services;
    }
}
