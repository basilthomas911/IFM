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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName,
            "TomasAI.IFM.Application.MarketData.Host.Worker", "TomasAI.IFM.Application.MarketData.Host.Worker.csproj")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Cannot locate the Market Data worker project.");
        var configuration = typeof(WorkerTestProcess).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        return RequireAssembly(Path.Combine(directory.FullName, project, "bin", configuration, "net10.0", project + ".dll"));
    }

    private static string RequireAssembly(string path)
    {
        if (!File.Exists(path) || !File.Exists(Path.ChangeExtension(path, "deps.json"))
            || !File.Exists(Path.ChangeExtension(path, "runtimeconfig.json")))
            throw new FileNotFoundException("Build or publish the complete Market Data worker before running lifecycle tests.", path);
        return Path.GetFullPath(path);
    }
}
