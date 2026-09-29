using System.Text.RegularExpressions;

namespace PdfCompressor.Services;

/// <summary>
/// Implementa a estratégia em 3 vias para localização do Ghostscript (DEC-08).
/// Procura em: caminhos aprovados do Program Files, Registro do Windows e variável PATH.
/// </summary>
public sealed class GhostscriptLocator : IGhostscriptLocator
{
    private static readonly Regex VersionRegex = new(@"\b\d+(\.\d+)+\b", RegexOptions.Compiled);

    private readonly IGhostscriptSystemEnvironment _environment;
    private readonly IDiagnosticLogger? _logger;

    public GhostscriptLocator(
        IGhostscriptSystemEnvironment? environment = null,
        IDiagnosticLogger? logger = null)
    {
        _environment = environment ?? new DefaultGhostscriptSystemEnvironment();
        _logger = logger;
    }

    public bool IsAvailable()
    {
        return FindExecutablePath() != null;
    }

    public string? FindExecutablePath(string? customPath = null)
    {
        // 1. Caminho customizado informado com prioridade máxima
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            var customResult = CheckCustomPath(customPath);
            if (customResult != null)
            {
                _logger?.LogInfo($"Ghostscript localizado via caminho customizado: {customResult}");
                return customResult;
            }
        }

        // 2. Procura em diretórios padrão do Program Files (C:\Program Files\gs\gs*\bin\gswin64c.exe)
        var programFilesResult = SearchProgramFiles();
        if (programFilesResult != null)
        {
            _logger?.LogInfo($"Ghostscript localizado no Program Files: {programFilesResult}");
            return programFilesResult;
        }

        // 3. Procura no Registro do Windows (HKLM / HKCU)
        var registryResult = SearchRegistry();
        if (registryResult != null)
        {
            _logger?.LogInfo($"Ghostscript localizado via Registro do Windows: {registryResult}");
            return registryResult;
        }

        // 4. Procura na variável de ambiente PATH
        var pathResult = SearchPathEnvironment();
        if (pathResult != null)
        {
            _logger?.LogInfo($"Ghostscript localizado via variável PATH: {pathResult}");
            return pathResult;
        }

        _logger?.LogWarning("Executável gswin64c.exe não foi encontrado em nenhum dos caminhos conhecidos.");
        return null;
    }

    public string? GetInstalledVersion(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return null;
        }

        var output = _environment.GetProcessVersionOutput(executablePath);
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var match = VersionRegex.Match(output);
        return match.Success ? match.Value : null;
    }

    private string? CheckCustomPath(string path)
    {
        if (_environment.FileExists(path))
        {
            return path;
        }

        if (_environment.DirectoryExists(path))
        {
            var candidates = new[]
            {
                CombinePath(path, "gswin64c.exe"),
                CombinePath(path, "bin", "gswin64c.exe"),
                CombinePath(path, "gs"),
                CombinePath(path, "bin", "gs")
            };

            foreach (var candidate in candidates)
            {
                if (_environment.FileExists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private string? SearchProgramFiles()
    {
        var roots = new List<string>();

        void AddIfNotEmpty(string? dir)
        {
            if (!string.IsNullOrWhiteSpace(dir) && !roots.Contains(dir, StringComparer.OrdinalIgnoreCase))
            {
                roots.Add(dir);
            }
        }

        AddIfNotEmpty(_environment.GetEnvironmentVariable("ProgramFiles"));
        AddIfNotEmpty(_environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        AddIfNotEmpty(_environment.GetEnvironmentVariable("ProgramW6432"));

        if (_environment.IsWindows)
        {
            AddIfNotEmpty(@"C:\Program Files");
            AddIfNotEmpty(@"C:\Program Files (x86)");
        }

        foreach (var root in roots)
        {
            var gsRoot = CombinePath(root, "gs");
            if (!_environment.DirectoryExists(gsRoot))
            {
                continue;
            }

            var versionDirs = _environment.GetDirectories(gsRoot, "gs*")
                .OrderByDescending(ExtractDirectoryVersion)
                .ThenByDescending(d => d, StringComparer.OrdinalIgnoreCase);

            foreach (var versionDir in versionDirs)
            {
                var candidate = CombinePath(versionDir, "bin", "gswin64c.exe");
                if (_environment.FileExists(candidate))
                {
                    return candidate;
                }

                var directCandidate = CombinePath(versionDir, "gswin64c.exe");
                if (_environment.FileExists(directCandidate))
                {
                    return directCandidate;
                }
            }
        }

        return null;
    }

    private string? SearchRegistry()
    {
        foreach (var candidate in _environment.GetRegistryInstallPaths())
        {
            if (_environment.FileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private string? SearchPathEnvironment()
    {
        var pathVar = _environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathVar))
        {
            return null;
        }

        var pathSeparator = _environment.IsWindows ? ';' : Path.PathSeparator;
        var directories = pathVar.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var dir in directories)
        {
            var winCandidate = CombinePath(dir, "gswin64c.exe");
            if (_environment.FileExists(winCandidate))
            {
                return winCandidate;
            }

            if (!_environment.IsWindows)
            {
                var nixCandidate = CombinePath(dir, "gs");
                if (_environment.FileExists(nixCandidate))
                {
                    return nixCandidate;
                }
            }
        }

        return null;
    }

    private string CombinePath(string p1, string p2)
    {
        var sep = _environment.IsWindows ? '\\' : Path.DirectorySeparatorChar;
        var t1 = p1.TrimEnd('\\', '/');
        var t2 = p2.TrimStart('\\', '/');
        return $"{t1}{sep}{t2}";
    }

    private string CombinePath(string p1, string p2, string p3)
    {
        return CombinePath(CombinePath(p1, p2), p3);
    }

    private static Version ExtractDirectoryVersion(string dirPath)
    {
        var folderName = dirPath.TrimEnd('\\', '/');
        var lastSlash = folderName.LastIndexOfAny(['\\', '/']);
        if (lastSlash >= 0)
        {
            folderName = folderName[(lastSlash + 1)..];
        }

        if (folderName.StartsWith("gs", StringComparison.OrdinalIgnoreCase))
        {
            folderName = folderName[2..];
        }

        if (Version.TryParse(folderName, out var v))
        {
            return v;
        }

        return new Version(0, 0);
    }
}
