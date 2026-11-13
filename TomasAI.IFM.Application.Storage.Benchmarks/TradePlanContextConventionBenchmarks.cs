using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

/// <summary>Measures anchored offset reads, including the cached chart on the first page.</summary>
[MemoryDiagnoser, ThreadingDiagnoser]
public class TradePlanContextConventionBenchmarks
{
    delegate Task<StrategyTradePlanHistoryPage> ReadOffset(StrategyPositionId position, TradeStrategyKind kind,
        DateOnly date, int offset, int size, long? revision, CancellationToken token);
    delegate Task<StrategyTradePlanHistoryPage> ReadOwner(ITradePlanDbContext store, StrategyPositionId position,
        TradeStrategyKind kind, DateOnly date, int offset, int size, long? revision, CancellationToken token);
    ReadOffset read = null!;
    StrategyTradePlanSnapshot fixture = null!;

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
            .Add(TradePlanDbContext.TradePlanDbConnection, builder.ConnectionString, "System.Data.Postgres")
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, builder.ConnectionString, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        await new TradePlanSchemaDb(settings, logger).CreateAllAsync();
        var store = new TradePlanDbContext(settings, logger);
        if (store.GetType().GetMethod("GetOffsetHistoryAsync") is { } legacy)
            read = legacy.CreateDelegate<ReadOffset>(store);
        else
        {
            var owner = Type.GetType("TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.TradePlanOffsetHistory, TomasAI.IFM.Domain.Trade", true)!;
            var execute = owner.GetMethod("ReadAsync", BindingFlags.Public | BindingFlags.Static)!.CreateDelegate<ReadOwner>();
            read = (position, kind, date, offset, size, revision, token) =>
                execute(store, position, kind, date, offset, size, revision, token);
        }

        fixture = Plan(310001, 1, -1m, true);
        if (await store.GetCurrentAsync(fixture.Position.Id, fixture.Position.StrategyKind, fixture.ValueDate) is null)
        {
            var tx = new PostgresEventTransaction(settings);
            await tx.ExecuteOnceAsync(async (transaction, token) =>
            {
                for (var revision = 1; revision <= 120; revision++)
                {
                    var item = Plan(310001, revision, -revision, true);
                    item = item with { IronCondorTradePlanSnapshot = new()
                    {
                        Position = item.Position, CombinedSpreadPrice = -10m - revision / 100m,
                        CombinedForwardPrice = -11m - revision / 100m, CalculationVersion = "LegacyScenario/v1"
                    } };
                    item = item with { ContentHash = TradePlanContractIdentity.PlanHash(item) };
                    await store.ProjectMaterialAsync(transaction, item, token).ConfigureAwait(false);
                }
                return true;
            }).ConfigureAwait(false);
        }
        var latest = await ReadLatestWithChart();
        var older = await ReadAnchoredPage();
        if (latest.MaximumRevision != 120 || latest.Items.Length != 20 || latest.ChartPoints.Length != 120
            || older.Items.Length != 20 || older.ChartPoints.Length != 0 || older.Items[0].PlanRevision != 80)
            throw new InvalidOperationException("Trade Plan history qualification preflight failed.");
    }

    [Benchmark] public Task<StrategyTradePlanHistoryPage> ReadLatestWithChart() =>
        read(fixture.Position.Id, fixture.Position.StrategyKind, fixture.ValueDate, 0, 20, null, CancellationToken.None);

    [Benchmark] public Task<StrategyTradePlanHistoryPage> ReadAnchoredPage() =>
        read(fixture.Position.Id, fixture.Position.StrategyKind, fixture.ValueDate, 40, 20, 120, CancellationToken.None);

    internal static StrategyTradePlanSnapshot Plan(int suffix, long revision, decimal pnl, bool material)
    {
        var now = new DateTime(2026, 11, 10, 14, 0, 0, DateTimeKind.Utc).AddMinutes(revision * 2);
        var position = new StrategyPositionSnapshot
        {
            Id = new(new TradeEntityId(suffix, suffix + 1, suffix + 2, suffix + 3),
                TradePlanContractIdentity.DeterministicId($"position-{suffix}")),
            StrategyKind = TradeStrategyKind.IronCondor,
            Phase = StrategyPositionPhase.MarkToMarket,
            PositionSequence = revision,
            RouteGeneration = 1,
            Legs = Enumerable.Range(1, 4).Select(index => new StrategyPositionLeg
            {
                TradeLegId = TradePlanContractIdentity.DeterministicId($"leg-{suffix}-{index}"),
                ContractId = $"ES-{suffix}-{index}",
                AssetFamily = TradeAssetFamily.FuturesOption,
                SignedQuantity = index % 2 == 0 ? -1 : 1,
                OpeningPrice = 10m + index,
                CurrentPrice = 10.1m + index,
                LastSourceSequence = revision,
                LastPriceAtUtc = now
            }).ToArray(),
            UnrealizedPnl = pnl,
            AsOfUtc = now,
            IsOpen = true
        };
        var plan = new StrategyTradePlanSnapshot
        {
            Position = position,
            ValueDate = DateOnly.FromDateTime(now),
            PlanRevision = revision,
            CurrentValue = 1m,
            TotalPnl = pnl,
            ForwardTradePrice = 1m,
            ForwardPnl = pnl,
            State = TradePlanState.Normal,
            Action = TradePlanAction.Monitor,
            MaterialChange = material,
            ReasonCode = "PLAN.NORMAL",
            Explanation = "isolated benchmark",
            Parameters = new(),
            CalculatedAtUtc = now
        };
        return plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
    }
}
