using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Paired acceptance configuration; generated dependency builds also stay on E:.</summary>
public sealed class ApiServerPerformanceConfig : ManualConfig
{
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Job> Jobs = new(StringComparer.OrdinalIgnoreCase);
    public ApiServerPerformanceConfig()
    {
        WithOption(ConfigOptions.DisableParallelBuild, true);
        var buildRoot = Environment.GetEnvironmentVariable("IFM_API_PERF_BUILD_ROOT")
            ?? @"E:\IFM\BenchmarkArtifacts\ApiServerPerformance\20261107\generated";
        buildRoot = Path.GetFullPath(buildRoot);
        if (!string.Equals(Path.GetPathRoot(buildRoot), @"E:\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("API performance build output must be on E:.");
        // All classes in one run must share the same toolchain/job identity. Creating a
        // new toolchain per attribute instance makes BenchmarkDotNet rebuild the full
        // dependency graph once per class instead of sharing a generated executable.
        AddJob(Jobs.GetOrAdd(buildRoot, static root => CreateJob(root)));
    }

    static Job CreateJob(string buildRoot)
    {
        var standard = CsProjCoreToolchain.NetCoreApp10_0;
        var toolchain = new Toolchain("Pinned-ApiServerPerformance-Net10", new PinnedGenerator(buildRoot), standard.Builder, standard.Executor);
        return Job.Default.WithId("ApiServerPerformance-Net10-ServerGC")
            .WithRuntime(CoreRuntime.Core10_0).WithGcServer(true)
            .WithToolchain(toolchain)
            .WithLaunchCount(2).WithWarmupCount(3).WithIterationCount(10)
            .WithArguments([new MsBuildArgument("/p:ArtifactsPath=" + buildRoot),
                new MsBuildArgument("/p:DatabentoNativeImplementation=Rust"),
                new MsBuildArgument("/p:DatabentoNativeConfiguration=Debug"), new MsBuildArgument("/m:2")]);
    }

    sealed class PinnedGenerator(string buildRoot) : CsProjGenerator("net10.0", null!, null!, null!)
    {
        // Source exports intentionally contain duplicate project names. Discovery
        // must not search the repository or follow its .artifacts junction.
        protected override FileInfo GetProjectFilePath(Type benchmarkTarget, ILogger logger)
        {
            var project = Environment.GetEnvironmentVariable("IFM_API_PERF_PROJECT") ?? DefaultProject(benchmarkTarget);
            var file = new FileInfo(Path.GetFullPath(project));
            if (!file.Exists) throw new FileNotFoundException("Pinned benchmark project is missing.", file.FullName);
            return file;
        }

        protected override string GetBuildArtifactsDirectoryPath(BuildPartition partition, string artifactsRoot)
            => Path.Combine(buildRoot, partition.ProgramName);

        static string DefaultProject(Type target, [CallerFilePath] string source = "")
        {
            var workspace = Path.GetDirectoryName(Path.GetDirectoryName(source)!)!;
            var name = target.Assembly.GetName().Name!;
            return Path.Combine(workspace, name, name + ".csproj");
        }
    }
}
