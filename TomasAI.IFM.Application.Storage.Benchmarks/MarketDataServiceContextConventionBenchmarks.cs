using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures the real recovery projection path and the complete atomic VX pair operation.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class MarketDataServiceContextConventionBenchmarks
{
    static readonly DateOnly Date = new(2026, 11, 10);
    static readonly DateTime Utc = new(2026, 11, 10, 22, 30, 0, DateTimeKind.Utc);
    const string Dataset = "GLBX.MDP3";
    delegate Task<IReadOnlyList<string>> ReadLog(DateOnly date, string? dataset, Guid? operation, int limit, CancellationToken token);
    MarketDataServiceDbContext store = null!;
    Func<MarketDataFeedRecoveryFact, CancellationToken, Task> append = null!;
    ReadLog read = null!;
    MarketDataFeedRecoveryFact existing = null!;
    FuturesRolloverContractAssignment front = null!;
    FuturesRolloverContractAssignment second = null!;

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
        settings.Username = "";
        settings.Password = "";
        var logger = NullLogger<DbProvider>.Instance;
        var databases = new DbConnectionSettings().Add(MarketDataServiceDbContext.MarketDataServiceDbConnection,
            settings.ConnectionString, "System.Data.Postgres");
        await new MarketDataServiceSchemaDb(databases, logger).CreateAllAsync();
        var constructor = typeof(MarketDataServiceDbContext).GetConstructors().Single();
        store = (MarketDataServiceDbContext)constructor.Invoke(constructor.GetParameters().Length == 2
            ? [databases, logger] : [databases, null!, null!, logger]);
        var legacy = typeof(MarketDataServiceDbContext).Assembly.GetType(
            "TomasAI.IFM.Application.Storage.MarketDataServiceDb.Recovery.MarketDataRecoveryLog");
        if (legacy?.GetMethod("ProjectAsync") is { } previousWrite)
        {
            var previousRead = legacy.GetMethod("QueryAsync")!.CreateDelegate<
                Func<string, DateOnly, string?, Guid?, int, CancellationToken, Task<IReadOnlyList<string>>>>();
            var write = previousWrite.CreateDelegate<Func<string, MarketDataFeedRecoveryFact, CancellationToken, Task>>();
            append = (fact, token) => write(store.ConnectionString, fact, token);
            read = (date, dataset, operation, limit, token) =>
                previousRead(store.ConnectionString, date, dataset, operation, limit, token);
        }
        else
        {
            var projector = Type.GetType("TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.EventProjector.MarketDataFeedRecoveryEventProjector, TomasAI.IFM.Domain.MarketData.Feed", true)!;
            var map = projector.GetMethod("ToProjectionRow", BindingFlags.Static | BindingFlags.NonPublic)!;
            var write = store.GetType().GetMethod("AppendRecoveryLogAsync")!;
            var fact = Expression.Parameter(typeof(MarketDataFeedRecoveryFact));
            var record = Expression.Parameter(typeof(RecoveryRecord));
            var token = Expression.Parameter(typeof(CancellationToken));
            var invoke = Expression.Call(Expression.Constant(store), write, Expression.Call(map, fact, record), token);
            var persist = Expression.Lambda<Func<MarketDataFeedRecoveryFact, RecoveryRecord, CancellationToken, Task>>(invoke, fact, record, token).Compile();
            append = (value, ct) => persist(value, RecoveryRecord.Parse(value.Payload, value.Digest), ct);
            read = store.GetType().GetMethod("ReadRecoveryLogAsync")!.CreateDelegate<ReadLog>(store);
        }

        existing = NewFact();
        await AppendExistingRecovery();
        if ((await ReadRecovery()).Count == 0) throw new InvalidOperationException("Recovery log qualification preflight failed.");
        front = Assignment(DatabentoContractRole.VxFrontMonth, "VX20261216", new(2026, 12, 16));
        second = Assignment(DatabentoContractRole.VxSecondMonth, "VX20270120", new(2027, 1, 20));
        var current = await store.ListAssignmentsAsync();
        var saved = await store.ReplaceVxAssignmentsAsync(front, second,
            current.SingleOrDefault(row => row.ContractRole == front.ContractRole)?.RowVersion ?? 0,
            current.SingleOrDefault(row => row.ContractRole == second.ContractRole)?.RowVersion ?? 0);
        front = saved.Single(row => row.ContractRole == front.ContractRole);
        second = saved.Single(row => row.ContractRole == second.ContractRole);
    }

    [Benchmark] public Task<IReadOnlyList<string>> ReadRecovery()
        => read(Date, Dataset, null, 32, CancellationToken.None);

    [Benchmark] public Task AppendExistingRecovery() => append(existing, CancellationToken.None);

    [Benchmark] public Task AppendNewRecovery() => append(NewFact(), CancellationToken.None);

    [Benchmark] public async Task ReplaceVxPair()
    {
        var saved = await store.ReplaceVxAssignmentsAsync(front, second, front.RowVersion, second.RowVersion);
        front = saved.Single(row => row.ContractRole == front.ContractRole);
        second = saved.Single(row => row.ContractRole == second.ContractRole);
    }

    static MarketDataFeedRecoveryFact NewFact()
    {
        var id = Guid.NewGuid();
        var row = new RecoveryRecord
        {
            Version = 1, RecordId = id, OperationId = Guid.NewGuid(), Sequence = 1, Kind = "Started",
            RuntimeId = "benchmark", Role = "Options", Dataset = Dataset, ValueDate = Date,
            OccurredAtUtc = Utc, StartedAtUtc = Utc, RecoveryDeadlineUtc = Utc.AddMinutes(2),
            MissingProofs = ["Transport", "Pricing"]
        };
        var json = JsonSerializer.Serialize(row);
        return new MarketDataFeedRecoveryStartedEvent
        {
            Id = id, EventId = 1, ReceivedOn = Utc, Payload = json, Digest = RecoveryRecord.Digest(json)
        };
    }

    static FuturesRolloverContractAssignment Assignment(DatabentoContractRole role, string contract, DateOnly maturity)
        => new()
        {
            ContractRole = role, RootSymbol = "VX", ContractId = contract, Description = "Fixture",
            LocalSymbol = contract, SecurityType = "FUT", Currency = "USD", Exchange = "CFE", Multiplier = "1000",
            LastTradeDate = maturity, NextRolloverDate = maturity, SourceContractHash = new('a', 64),
            CreatedOnUtc = Utc, CreatedBy = "fixture", UpdatedOnUtc = Utc, UpdatedBy = "fixture"
        };
}
