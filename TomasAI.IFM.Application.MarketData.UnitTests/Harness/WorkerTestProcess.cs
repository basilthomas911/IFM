using System.Reflection;

namespace TomasAI.IFM.Application.MarketData.UnitTests.Harness;

/// <summary>Launches workers with their own dependency manifest and complete build output.</summary>
internal static class WorkerTestProcess
{
    internal static string AssemblyPath
    {
        get
        {
            if (Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_HOST_DIRECTORY") is { Length: > 0 } package)
                return RequireAssembly(Path.Combine(package, "Workers", "TomasAI.IFM.Application.MarketData.Host.Worker.dll"));
            return BuildAssembly("TomasAI.IFM.Application.MarketData.Host.Worker");
        }
    }

    internal static string WorkerDirectory => Path.GetDirectoryName(AssemblyPath)!;
    internal static string QualificationAssemblyPath => BuildAssembly("TomasAI.IFM.Application.MarketData.QualificationHost");
    internal static string HostDirectory => Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_HOST_DIRECTORY") is { Length: > 0 } package
        ? Path.GetFullPath(package) : Path.GetDirectoryName(BuildAssembly("TomasAI.IFM.Application.MarketData.Host"))!;

    private static string BuildAssembly(string project)
    {
        var configuration = typeof(WorkerTestProcess).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        // --artifacts-path puts sibling executables outside the source checkout.
        var sibling = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", project, configuration.ToLowerInvariant(), project + ".dll"));
        if (File.Exists(sibling)) return RequireAssembly(sibling);
        return RequireAssembly(Path.Combine(RepositoryRoot(), project, "bin", configuration, "net10.0", project + ".dll"));
    }

    internal static string RepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(source)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Cannot locate the Market Data source checkout.");
    }

    private static string RequireAssembly(string path)
    {
        if (!File.Exists(path) || !File.Exists(Path.ChangeExtension(path, "deps.json"))
            || !File.Exists(Path.ChangeExtension(path, "runtimeconfig.json")))
            throw new FileNotFoundException("Build or publish the complete Market Data worker before running lifecycle tests.", path);
        return Path.GetFullPath(path);
    }
}
