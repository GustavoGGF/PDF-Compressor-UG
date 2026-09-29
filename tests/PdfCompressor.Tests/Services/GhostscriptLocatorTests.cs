using PdfCompressor.Services;
using Xunit;

namespace PdfCompressor.Tests.Services;

public sealed class GhostscriptLocatorTests
{
    [Fact]
    public void FindExecutablePath_CustomPathDirectFileExists_ReturnsCustomPath()
    {
        var env = new FakeGhostscriptEnvironment();
        env.ExistingFiles.Add(@"D:\Custom\gs\gswin64c.exe");

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath(@"D:\Custom\gs\gswin64c.exe");

        Assert.Equal(@"D:\Custom\gs\gswin64c.exe", result);
    }

    [Fact]
    public void FindExecutablePath_CustomPathDirectoryContainsGswin64c_ReturnsSubPath()
    {
        var env = new FakeGhostscriptEnvironment();
        env.ExistingDirectories.Add(@"D:\Custom\gs");
        env.ExistingFiles.Add(@"D:\Custom\gs\gswin64c.exe");

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath(@"D:\Custom\gs");

        Assert.Equal(@"D:\Custom\gs\gswin64c.exe", result);
    }

    [Fact]
    public void FindExecutablePath_CustomPathDirectoryContainsBinGswin64c_ReturnsBinPath()
    {
        var env = new FakeGhostscriptEnvironment();
        env.ExistingDirectories.Add(@"D:\Custom\gs");
        env.ExistingDirectories.Add(@"D:\Custom\gs\bin");
        env.ExistingFiles.Add(@"D:\Custom\gs\bin\gswin64c.exe");

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath(@"D:\Custom\gs");

        Assert.Equal(@"D:\Custom\gs\bin\gswin64c.exe", result);
    }

    [Fact]
    public void FindExecutablePath_CustomPathInvalid_FallsBackToStandardSearch()
    {
        var env = new FakeGhostscriptEnvironment();
        env.EnvironmentVariables["ProgramFiles"] = @"C:\Program Files";
        env.ExistingDirectories.Add(@"C:\Program Files\gs");
        env.DirectoriesMap[@"C:\Program Files\gs"] = [@"C:\Program Files\gs\gs10.02.1"];
        env.ExistingFiles.Add(@"C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe");

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath(@"Z:\NonExistent\Path");

        Assert.Equal(@"C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe", result);
    }

    [Fact]
    public void FindExecutablePath_DiscoversInProgramFiles_SortsVersionsDescending()
    {
        var env = new FakeGhostscriptEnvironment();
        env.EnvironmentVariables["ProgramFiles"] = @"C:\Program Files";
        env.ExistingDirectories.Add(@"C:\Program Files\gs");
        env.DirectoriesMap[@"C:\Program Files\gs"] =
        [
            @"C:\Program Files\gs\gs9.55.0",
            @"C:\Program Files\gs\gs10.04.0",
            @"C:\Program Files\gs\gs10.02.1"
        ];
        env.ExistingFiles.Add(@"C:\Program Files\gs\gs9.55.0\bin\gswin64c.exe");
        env.ExistingFiles.Add(@"C:\Program Files\gs\gs10.04.0\bin\gswin64c.exe");
        env.ExistingFiles.Add(@"C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe");

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath();

        // Should choose gs10.04.0 (descending order)
        Assert.Equal(@"C:\Program Files\gs\gs10.04.0\bin\gswin64c.exe", result);
    }

    [Fact]
    public void FindExecutablePath_DiscoversInRegistry_ReturnsRegistryPath()
    {
        var env = new FakeGhostscriptEnvironment();
        // Program files empty
        env.RegistryPaths.Add(@"C:\InstalledSomewhere\Ghostscript\bin\gswin64c.exe");
        env.ExistingFiles.Add(@"C:\InstalledSomewhere\Ghostscript\bin\gswin64c.exe");

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath();

        Assert.Equal(@"C:\InstalledSomewhere\Ghostscript\bin\gswin64c.exe", result);
    }

    [Fact]
    public void FindExecutablePath_DiscoversInPathEnvironmentVariable_ReturnsPathExecutable()
    {
        var env = new FakeGhostscriptEnvironment();
        var separator = env.IsWindows ? ';' : Path.PathSeparator;
        env.EnvironmentVariables["PATH"] = $@"C:\Tools{separator}C:\gs\bin{separator}C:\Windows";
        env.ExistingFiles.Add(@"C:\gs\bin\gswin64c.exe");

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath();

        Assert.Equal(@"C:\gs\bin\gswin64c.exe", result);
    }

    [Fact]
    public void FindExecutablePath_ExecutableNotFoundAnywhere_ReturnsNull()
    {
        var env = new FakeGhostscriptEnvironment();

        var locator = new GhostscriptLocator(env);
        var result = locator.FindExecutablePath();

        Assert.Null(result);
        Assert.False(locator.IsAvailable());
    }

    [Fact]
    public void IsAvailable_WhenFound_ReturnsTrue()
    {
        var env = new FakeGhostscriptEnvironment();
        env.ExistingFiles.Add(@"C:\Program Files\gs\gs10.02.1\bin\gswin64c.exe");
        env.EnvironmentVariables["ProgramFiles"] = @"C:\Program Files";
        env.ExistingDirectories.Add(@"C:\Program Files\gs");
        env.DirectoriesMap[@"C:\Program Files\gs"] = [@"C:\Program Files\gs\gs10.02.1"];

        var locator = new GhostscriptLocator(env);

        Assert.True(locator.IsAvailable());
    }

    [Theory]
    [InlineData("GPL Ghostscript 10.02.1 (2023-11-01)\nCopyright (C) 2023 Artifex Software, Inc.", "10.02.1")]
    [InlineData("GPL Ghostscript 10.04.0 (2024-09-18)", "10.04.0")]
    [InlineData("9.55.0\n", "9.55.0")]
    [InlineData("Ghostscript 10.02.1", "10.02.1")]
    public void GetInstalledVersion_ParsesStandardGhostscriptOutput(string rawOutput, string expectedVersion)
    {
        var env = new FakeGhostscriptEnvironment();
        env.VersionOutputs["gswin64c.exe"] = rawOutput;

        var locator = new GhostscriptLocator(env);
        var version = locator.GetInstalledVersion("gswin64c.exe");

        Assert.Equal(expectedVersion, version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Command not found")]
    [InlineData(null)]
    public void GetInstalledVersion_InvalidOrEmptyOutput_ReturnsNull(string? output)
    {
        var env = new FakeGhostscriptEnvironment();
        env.VersionOutputs["gswin64c.exe"] = output;

        var locator = new GhostscriptLocator(env);
        var version = locator.GetInstalledVersion("gswin64c.exe");

        Assert.Null(version);
    }

    private sealed class FakeGhostscriptEnvironment : IGhostscriptSystemEnvironment
    {
        public bool IsWindows { get; set; } = true;
        public HashSet<string> ExistingFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<string>> DirectoriesMap { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RegistryPaths { get; } = [];
        public Dictionary<string, string?> VersionOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);

        private static string Norm(string p) => p.Replace('/', '\\');

        public bool FileExists(string path) => ExistingFiles.Any(f => string.Equals(Norm(f), Norm(path), StringComparison.OrdinalIgnoreCase));
        public bool DirectoryExists(string path) => ExistingDirectories.Any(d => string.Equals(Norm(d), Norm(path), StringComparison.OrdinalIgnoreCase));

        public IEnumerable<string> GetDirectories(string path, string searchPattern)
        {
            var match = DirectoriesMap.FirstOrDefault(kvp => string.Equals(Norm(kvp.Key), Norm(path), StringComparison.OrdinalIgnoreCase));
            return match.Value ?? Enumerable.Empty<string>();
        }

        public string? GetEnvironmentVariable(string variableName)
        {
            return EnvironmentVariables.TryGetValue(variableName, out var value) ? value : null;
        }

        public IEnumerable<string> GetRegistryInstallPaths() => RegistryPaths;

        public string? GetProcessVersionOutput(string executablePath)
        {
            return VersionOutputs.TryGetValue(executablePath, out var output) ? output : null;
        }
    }
}
