using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Identity;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the same historical progress read and advance before and after projected diagnostics.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class MarketDataContextConventionBenchmarks
{
    MarketDataDbContext database = null!;
    IHistoricalDataLoaderStore owner = null!;
    HistoricalDataLoaderState state = null!;
    static readonly DateTimeOffset Start = new(2026, 11, 10, 14, 0, 0, TimeSpan.Zero);

    [Params(false, true)] public bool ColdConnection { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var raw = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Host != "127.0.0.1" || builder.Port != 58231 || builder.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");
        await using (var guard = new NpgsqlConnection(raw))
        {
            await guard.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT current_setting('fsync'),current_setting('synchronous_commit'),current_setting('full_page_writes')", guard);
            await using var row = await command.ExecuteReaderAsync();
            await row.ReadAsync();
            if (Enumerable.Range(0, 3).Any(index => row.GetString(index) != "on"))
                throw new InvalidOperationException("Qualification requires full WAL durability.");
        }
        builder.Pooling = !ColdConnection;
        builder.Username = "";
        builder.Password = "";
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            builder.ConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new MarketDataSchemaDb(settings, logger).CreateAsync(
            ["historical_data_loader", "historical_analytics_attempt_projection"]);
        var factory = new DbContextFactory(new DbContextResolver(type => type == typeof(IObjectRepository<MarketDataDbContext>)
            ? database : throw new InvalidOperationException("Unexpected fixture context " + type.Name)));
        database = new(settings, factory, new UnexpectedSequenceUse(), logger);
        owner = new HistoricalDataLoaderStore(factory);
        var attempt = Guid.NewGuid();
        state = new()
        {
            DataLoadAttemptId = attempt, RequestSha256 = "fixture:" + attempt.ToString("N"),
            Status = HistoricalDataLoaderStatus.Processing, UpdatedAtUtc = Start,
            Checkpoint = new HistoricalAcquisitionCheckpoint
            {
                DataLoadAttemptId = attempt, Stage = HistoricalAcquisitionStage.Downloaded,
                ProviderJobId = "fixture-job", ProviderFileId = "fixture-file", BatchOrdinal = 0, SourcePosition = "0"
            }
        };
        await AdvanceProgress();
        var projection = await ReadProgress();
        if (projection is not { Status: "Processing", LastCompletedBatchOrdinal: 1, LastCompletedRecordOrdinal: 64 })
            throw new InvalidOperationException("Historical progress qualification preflight failed.");
    }

    [Benchmark] public Task<FuturesAnalyticsHistoricalDataLoaderDiagnosticsReadModel?> ReadProgress() =>
        database.GetHistoricalAnalyticsAttemptProjectionAsync(state.DataLoadAttemptId);

    [Benchmark] public Task AdvanceProgress()
    {
        var ordinal = state.Checkpoint.BatchOrdinal + 1;
        state = state with
        {
            Checkpoint = state.Checkpoint with { BatchOrdinal = ordinal,
                SourcePosition = (ordinal * 64).ToString(System.Globalization.CultureInfo.InvariantCulture) },
            UpdatedAtUtc = Start.AddMilliseconds(ordinal)
        };
        return owner.SaveAsync(state, CancellationToken.None).AsTask();
    }
    sealed class UnexpectedSequenceUse : ISequenceIdGenerator
    {
        public ValueTask<long> GetSequenceIdAsync(SequenceName name, CancellationToken token = default)
            => throw new InvalidOperationException("The historical checkpoint workload must not allocate a sequence.");

        public ValueTask<long> GetHighWatermarkAsync(SequenceName name, CancellationToken token = default)
            => throw new InvalidOperationException("The historical checkpoint workload must not read a sequence.");
    }
}
