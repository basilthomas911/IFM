using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.SystemAdmin.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared;

namespace TomasAI.IFM.Domain.SystemAdmin.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class DatabaseNamesBenchmarks
{
    [Benchmark(Baseline = true)]
    public string[] BeforeBuildMutableResponse()
    {
        string[] names =
        [
            DatabaseBackupNames.EventDb,
            DatabaseBackupNames.FundDb,
            DatabaseBackupNames.LogDb,
            DatabaseBackupNames.MarketDataDb,
            DatabaseBackupNames.OptionPricerDb,
            DatabaseBackupNames.ReferenceDb,
            DatabaseBackupNames.TradeDb
        ];
        return names;
    }

    [Benchmark]
    public IReadOnlyList<string> AfterCachedReadOnlyResponse()
        => SystemAdminQueryState.GetDatabaseNames().Names;
}
