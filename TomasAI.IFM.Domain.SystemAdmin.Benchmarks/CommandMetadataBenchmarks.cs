using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Commands;

namespace TomasAI.IFM.Domain.SystemAdmin.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class CommandMetadataBenchmarks
{
    static readonly string CachedUser =
        string.Concat(Environment.UserDomainName, "\\", Environment.UserName);
    readonly BackupDatabaseCommand _command = new();

    [Benchmark(Baseline = true)]
    public int BeforeRecomputeMetadata()
    {
        var commandName = _command.GetType().Name;
        var eventSource = $"{BackupDatabaseCommand.Actor}Actor";
        var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
        return commandName.Length + eventSource.Length + user.Length;
    }

    [Benchmark]
    public int AfterCachedMetadata()
        => nameof(BackupDatabaseCommand).Length
            + "SystemAdminCommandActor".Length
            + CachedUser.Length;
}
