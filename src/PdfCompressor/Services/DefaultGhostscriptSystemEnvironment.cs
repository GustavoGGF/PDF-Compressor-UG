using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PdfCompressor.Services;

/// <summary>
/// Provedor padrão de ambiente para descoberta do Ghostscript e consulta de versão.
/// </summary>
public sealed class DefaultGhostscriptSystemEnvironment : IGhostscriptSystemEnvironment
{
    public bool IsWindows => OperatingSystem.IsWindows();

    public bool FileExists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

    public bool DirectoryExists(string path) => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);

    public IEnumerable<string> GetDirectories(string path, string searchPattern)
    {
        if (!DirectoryExists(path))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory.GetDirectories(path, searchPattern);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string? GetEnvironmentVariable(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName);
    }

    public IEnumerable<string> GetRegistryInstallPaths()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<string>();
        }

        return QueryWindowsRegistryForGhostscript();
    }

    [SupportedOSPlatform("windows")]
    private static List<string> QueryWindowsRegistryForGhostscript()
    {
        var foundPaths = new List<string>();

        string[] registryRoots =
        [
            @"SOFTWARE\Artifex\Ghostscript",
            @"SOFTWARE\WOW6432Node\Artifex\Ghostscript",
            @"SOFTWARE\GPL Ghostscript",
            @"SOFTWARE\WOW6432Node\GPL Ghostscript"
        ];

        RegistryKey[] baseKeys = [Registry.LocalMachine, Registry.CurrentUser];

        foreach (var baseKey in baseKeys)
        {
            foreach (var subKeyPath in registryRoots)
            {
                try
                {
                    using var key = baseKey.OpenSubKey(subKeyPath);
                    if (key == null) continue;

                    foreach (var versionName in key.GetSubKeyNames())
                    {
                        using var versionKey = key.OpenSubKey(versionName);
                        if (versionKey == null) continue;

                        var gsDll = versionKey.GetValue("GS_DLL") as string;
                        if (!string.IsNullOrWhiteSpace(gsDll))
                        {
                            var binDir = Path.GetDirectoryName(gsDll);
                            if (!string.IsNullOrWhiteSpace(binDir))
                            {
                                var exePath = Path.Combine(binDir, "gswin64c.exe");
                                foundPaths.Add(exePath);
                            }
                        }
                    }
                }
                catch
                {
                    // Falhas de leitura no registro não devem interromper o fluxo
                }
            }
        }

        return foundPaths;
    }

    public string? GetProcessVersionOutput(string executablePath)
    {
        if (!FileExists(executablePath))
        {
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-v");

            using var process = Process.Start(psi);
            if (process == null) return null;

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return null;
            }

            return !string.IsNullOrWhiteSpace(stdout) ? stdout : stderr;
        }
        catch
        {
            return null;
        }
    }
}
