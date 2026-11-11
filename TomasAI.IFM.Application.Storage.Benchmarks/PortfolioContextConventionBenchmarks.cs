using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures projected Fund reads and complete owner-coordinated accepted mandate persistence.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class PortfolioContextConventionBenchmarks
{
    static readonly DateTime Utc = new(2026, 11, 10, 22, 0, 0, DateTimeKind.Utc);
    PortfolioDbContext store = null!;
    FundMandateReadModel fund = null!;
    Func<PortfolioProjection<FundMandateReadModel>, CancellationToken, Task> write = null!;
    long revision;

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
        builder.Pooling = !ColdConnection;builder.Username = "";builder.Password = "";
        var settings = new DbConnectionSettings()
            .Add(PortfolioDbContext.PortfolioDbConnection, builder.ConnectionString, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, builder.ConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new PortfolioSchemaDb(settings, logger).CreateAsync(["portfolio_schema"]);
        var transactions = new PostgresEventTransaction(settings);
        store = new(settings, logger, transactions);
        var owner = Type.GetType("TomasAI.IFM.Domain.Portfolio.Projection.AcceptedPortfolioProjection, TomasAI.IFM.Domain.Portfolio");
        var method = owner?.GetMethod("WriteFundProjectionAsync", BindingFlags.Public | BindingFlags.Static);
        if (method is null) write = (row, token) => store.UpsertFundAsync(row, token);
        else
        {
            var persist = method.CreateDelegate<Func<IEnlistedPostgresTransaction, PortfolioProjection<FundMandateReadModel>, CancellationToken, Task>>();
            write = (row, token) => transactions.ExecuteOnceAsync(async (transaction, ct) =>
            {
                await persist(transaction, row, ct).ConfigureAwait(false);
                return true;
            }, token);
        }
        fund = new()
        {
            PortfolioId = 111, FundId = 112, FundCode = "fixture", Name = "Qualification Fund",
            FundMandateVersion = 1, TradingYear = 2026, OperatingState = FundOperatingState.Active,
            EffectiveFromUtc = Utc.AddDays(-1), DecisionHorizon = "Monthly", CreatedOnUtc = Utc, CreatedBy = "fixture",
            UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"], PermittedDirections = ["Long", "Short"]
        };
        revision = (await store.GetFundRevisionAsync(fund.FundId))?.SourceEventId ?? 0;
        await PersistAcceptedMandate();
        if (await ReadFund() is not { FundId: 112 } || (await ReadActiveFunds()).Count != 1)
            throw new InvalidOperationException("Fund qualification preflight failed.");
    }

    [Benchmark] public Task<FundMandateReadModel?> ReadFund() => store.GetFundAsync(fund.FundId);

    [Benchmark] public Task<IReadOnlyList<FundMandateReadModel>> ReadActiveFunds() =>
        store.GetActiveFundsAsync(fund.PortfolioId, fund.TradingYear, fund.DecisionHorizon, Utc, 32);

    [Benchmark] public Task PersistAcceptedMandate()
    {
        var current = ++revision;
        return write(PortfolioProjection<FundMandateReadModel>.Create(fund, current, current, Utc), CancellationToken.None);
    }
}
