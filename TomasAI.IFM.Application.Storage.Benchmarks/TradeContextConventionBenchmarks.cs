using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Execution.Projection;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures prepared execution handoff persistence without contacting a broker.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class TradeContextConventionBenchmarks
{
    StrategyExecutionHandoffReadStore owner = null!;
    StrategyExecutionHandoffDefinition row = null!;
    static readonly DateTime At = new(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc);

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
        var settings = new DbConnectionSettings()
            .Add(TradeDbContext.TradeDbConnection, builder.ConnectionString, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, builder.ConnectionString, "System.Data.Postgres");
        await new TradeSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var legacy = typeof(TradeDbContext).Assembly.GetType("TomasAI.IFM.Application.Storage.TradeDb.StrategyExecutionHandoffSql");
        if (legacy?.GetField("Create", BindingFlags.Public | BindingFlags.Static)?.GetRawConstantValue() is string ddl)
        {
            await using var connection = new NpgsqlConnection(raw);
            await connection.OpenAsync().ConfigureAwait(false);
            await using var command = new NpgsqlCommand(ddl, connection);
            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        owner = new(new PostgresEventTransaction(settings));
        row = new()
        {
            Id = new(new TradeOrderId(110, 120, 130), Guid.NewGuid()), Revision = 0,
            Authorization = new() { WorkflowId = new(Guid.NewGuid()) },
            IntentHash = new('b', 64), UpdatedAtUtc = At
        };
        await PersistRevision().ConfigureAwait(false);
        var saved = await ReadHandoff().ConfigureAwait(false);
        if (saved is null || saved.Id != row.Id || saved.Revision != 1 || saved.IntentHash != row.IntentHash)
            throw new InvalidOperationException("Trade handoff fixture failed its exact revision readback.");
    }

    [Benchmark] public Task<StrategyExecutionHandoffDefinition?> ReadHandoff() => owner.ReadAsync(row.Id);

    [Benchmark] public Task PersistRevision()
    {
        row = row with { Revision = row.Revision + 1 };
        return owner.ProjectAsync(row);
    }
}
