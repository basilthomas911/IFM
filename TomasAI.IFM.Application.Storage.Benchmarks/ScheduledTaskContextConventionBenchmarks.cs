using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Query;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures persisted administrative snapshots and the complete domain-owned close-date query.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class ScheduledTaskContextConventionBenchmarks
{
    ScheduledTaskDbContext context = null!;
    ScheduledTaskRun run = null!;
    ScheduledTaskDefinition definition = null!;
    ScheduledTaskCatalog catalog = null!;
    long revision = 1;
    readonly string environment = "context-qualification-" + Guid.NewGuid().ToString("N");
    const string Host = ScheduledTaskKeys.ApiMarketCloseHost;

    [Params(false, true)]
    public bool ColdConnection { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var connection = Environment.GetEnvironmentVariable("IFM_DBCONTEXT_BENCHMARK_CONNECTION")
            ?? throw new InvalidOperationException("Provision the disposable qualification database first.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Port != 58231 || settings.Database != "ifm_dbcontext_benchmark")
            throw new InvalidOperationException("Only the disposable qualification database is allowed.");
        await using (var guard = new NpgsqlConnection(connection))
        {
            await guard.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT current_setting('fsync'),current_setting('synchronous_commit'),current_setting('full_page_writes')", guard);
            await using var row = await command.ExecuteReaderAsync();
            await row.ReadAsync();
            if (Enumerable.Range(0, 3).Any(index => row.GetString(index) != "on"))
                throw new InvalidOperationException("Qualification requires full WAL durability.");
        }
        settings.Pooling = !ColdConnection;
        settings.Username = string.Empty;
        settings.Password = string.Empty;
        var databases = new DbConnectionSettings().Add(ScheduledTaskDbContext.ConnectionName, settings.ConnectionString, "System.Data.Postgres");
        await new ScheduledTaskSchemaDb(databases, NullLogger<DbProvider>.Instance).CreateAllAsync();
        context = new(databases, NullLogger<DbProvider>.Instance);
        var schedule = ScheduledTaskIdentities.Catalog(environment, Host);
        definition = new() { Id = schedule, Revision = 1, Schedule = new() { Environment = environment, HostId = Host } };
        catalog = new() { Id = schedule, Revision = 1, Environment = environment, HostId = Host };
        run = new()
        {
            Id = new(Guid.NewGuid()), ScheduleId = schedule, Environment = environment, HostId = Host, Revision = 1,
            TaskKey = ScheduledTaskKeys.FuturesMarketClose, IntendedFireTimeUtc = new(2026, 11, 10, 22, 0, 0, TimeSpan.Zero),
            Status = ScheduledTaskRunStatus.Succeeded, Stage = ScheduledTaskKeys.MarketClosedStage,
            ExitCode = 0, CompletedEndOfDayValueDate = new(2026, 11, 10)
        };
        await context.ProjectAsync(definition);
        await context.ProjectAsync(catalog);
        await context.ProjectAsync(run);
        for (var index = 1; index <= 9; index++)
            await context.ProjectAsync(run with
            {
                Id = new(Guid.NewGuid()), IntendedFireTimeUtc = run.IntendedFireTimeUtc.AddMinutes(index),
                Status = ScheduledTaskRunStatus.Failed, Stage = "MarketCloseFailed", ExitCode = 1
            });
        if (await ReadDefinition() is null || await ReadCatalog() is null || await ReadRun() is null
            || await ReadCompletedClose() != run.CompletedEndOfDayValueDate || (await ReadHistoryPage()).Runs.Length != 10)
            throw new InvalidOperationException("Scheduled-task projection preflight failed.");
    }

    [Benchmark]
    public Task<ScheduledTaskDefinition?> ReadDefinition()
        => context.GetDefinitionAsync(environment, Host, definition.Id).AsTask();

    [Benchmark]
    public Task<ScheduledTaskCatalog?> ReadCatalog()
        => context.GetCatalogAsync(environment, Host).AsTask();

    [Benchmark]
    public Task<ScheduledTaskRun?> ReadRun()
        => context.GetRunAsync(environment, Host, run.ScheduleId, run.Id, run.IntendedFireTimeUtc).AsTask();

    [Benchmark]
    public Task<DateOnly?> ReadCompletedClose()
        => context.GetCompletedMarketCloseAsync(environment).AsTask();

    [Benchmark]
    public Task<ScheduledTaskRunPage> ReadHistoryPage()
        => context.GetRunHistoryAsync(environment, Host, run.ScheduleId, 30, null).AsTask();

    [Benchmark]
    public Task UpdateRun()
        => context.ProjectAsync(run with { Revision = Interlocked.Increment(ref revision) }).AsTask();

    [Benchmark]
    public Task RetryRun()
        => context.ProjectAsync(run).AsTask();

    [Benchmark]
    public Task UpdateEightConcurrentRevisions()
        => Task.WhenAll(Enumerable.Range(0, 8).Select(_ => UpdateRun()));
}
