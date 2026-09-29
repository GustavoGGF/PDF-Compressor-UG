using System.Diagnostics;

namespace PdfCompressor.Services;

/// <summary>
/// Executa processos nativos do sistema operacional de forma estritamente controlada,
/// sem shell intermediário e com suporte a cancelamento de árvore de processos.
/// </summary>
public sealed class DefaultGhostscriptProcessStarter : IGhostscriptProcessStarter
{
    public async Task<ProcessRunOutput> StartAndRunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();

        if (!process.Start())
        {
            throw new InvalidOperationException($"Não foi possível iniciar o processo '{executablePath}'.");
        }

        // Leitura assíncrona dos fluxos para evitar deadlocks de buffer de pipe
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        // Registro de cancelamento para encerramento forçado da árvore do processo
        await using var registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignora falhas de encerramento concorrente caso o processo já tenha finalizado
            }
        });

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // Processo já encerrado
            }

            throw;
        }

        stopwatch.Stop();

        string stdout;
        string stderr;

        try
        {
            stdout = await stdoutTask.ConfigureAwait(false);
            stderr = await stderrTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            stdout = string.Empty;
            stderr = string.Empty;
        }

        return new ProcessRunOutput(
            ExitCode: process.ExitCode,
            StandardOutput: stdout,
            StandardError: stderr,
            Duration: stopwatch.Elapsed
        );
    }
}
