using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using TomasAI.IFM.IntegrationTesting;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[CollectionDefinition("PostgreSQL plan atomicity", DisableParallelization = true)]
public sealed class PostgresTradePlanCollection : ICollectionFixture<PostgresTradePlanFixture>;

public sealed class PostgresTradePlanFixture : IAsyncLifetime
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("portfolioatomicity");
    public string Connection => infrastructure.PostgresConnectionString;
    public DbConnectionSettings Settings { get; private set; } = null!;
    public TradePlanDbContext Plans { get; private set; } = null!;
    public PostgresEventTransaction Transactions { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        await infrastructure.StartAsync();
        Settings = (DbConnectionSettings)new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, Connection, "System.Data.Postgres")
            .Add(TradePlanDbContext.TradePlanDbConnection, Connection, "System.Data.Postgres");
        await new EventSourceSchemaDb(Settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        await new TradePlanSchemaDb(Settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        Plans = new(Settings, NullLogger<DbProvider>.Instance);
        Transactions = new(Settings);
    }
    public async Task DisposeAsync() => await infrastructure.DisposeAsync();

    public async Task<long> EventCountAsync(string stream)
    {
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT count(*) FROM event_log l JOIN event_stream_id s ON l.eventstreamid=s.eventstreamid WHERE s.eventstream=$1;", connection);
        command.Parameters.AddWithValue(stream);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresTradePlanTests(PostgresTradePlanFixture fixture)
{
    static StrategyTradePlanSnapshot Plan(long revision = 1) => TradePlanStorageTests.Plan(Random.Shared.Next(6_000_000, 9_000_000), revision, -42m, true);
    static IronCondorTradePlanUpdatedEvent Source(StrategyTradePlanSnapshot plan) => new()
    {
        Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), Plan = plan, ReceivedOn = DateTime.UtcNow,
        AggregateId = plan.Position.Id.PositionId.ToString()
    };

    [Fact]
    public async Task Chart_reads_six_hours_of_price_only_samples_separately_from_the_grid_page()
    {
        var original = Plan();
        var at = DateTime.SpecifyKind(new DateTime(2026, 10, 19, 10, 0, 0), DateTimeKind.Utc);
        foreach (var (revision, hours, price) in new[] { (1,0,-10m), (2,5,-11m), (3,7,-12m), (4,7,-13m) })
        {
            var plan = original with { PlanRevision = revision, CalculatedAtUtc = at.AddHours(hours).AddSeconds(revision == 4 ? 40 : 0),
                IronCondorTradePlanSnapshot = new() { Position = original.Position, CombinedSpreadPrice = price, CombinedForwardPrice = price - .5m } };
            plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
            await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
            { await fixture.Plans.ProjectMaterialAsync(transaction, plan, token); return true; });
        }
        var first = await fixture.Plans.GetOffsetHistoryAsync(original.Position.Id, original.Position.StrategyKind, original.ValueDate, 0, 1);
        first.Items.Should().HaveCount(1);
        first.ChartEndUtc.Should().BeCloseTo(at.AddHours(7).AddSeconds(40), TimeSpan.FromMicroseconds(1));
        first.ChartPoints.Select(point => point.NetSpread).Should().Equal(-11m,-13m);
        first.ChartPoints.Select(point => point.ForwardSpread).Should().Equal(-11.5m,-13.5m);
        var older = await fixture.Plans.GetOffsetHistoryAsync(original.Position.Id, original.Position.StrategyKind, original.ValueDate, 1, 1, first.MaximumRevision);
        older.ChartPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task Offset_pages_read_only_the_requested_window_and_keep_a_stable_revision_boundary()
    {
        var original = Plan();
        async Task Insert(long revision)
        {
            var plan = original with { PlanRevision = revision };
            plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) + revision };
            await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
            { await fixture.Plans.ProjectMaterialAsync(transaction, plan, token); return true; });
        }
        for (var revision = 1; revision <= 125; revision++) await Insert(revision);
        var first = await fixture.Plans.GetOffsetHistoryAsync(original.Position.Id, original.Position.StrategyKind, original.ValueDate, 0, 50);
        first.TotalCount.Should().Be(125);
        first.Items.Should().HaveCount(50);
        first.Items.Select(plan => plan.PlanRevision).Should().Equal(Enumerable.Range(76, 50).Reverse().Select(i => (long)i));
        await Insert(126);
        var next = await fixture.Plans.GetOffsetHistoryAsync(original.Position.Id, original.Position.StrategyKind, original.ValueDate, 50, 50, first.MaximumRevision);
        next.Offset.Should().Be(50); next.TotalCount.Should().Be(125);
        next.Items.Select(plan => plan.PlanRevision).Should().Equal(Enumerable.Range(26, 50).Reverse().Select(i => (long)i));
        var tail = await fixture.Plans.GetOffsetHistoryAsync(original.Position.Id, original.Position.StrategyKind, original.ValueDate, 100, 50, first.MaximumRevision);
        tail.Items.Should().HaveCount(25);
        var empty = await fixture.Plans.GetOffsetHistoryAsync(original.Position.Id, original.Position.StrategyKind, original.ValueDate, 10000, 50, first.MaximumRevision);
        empty.Items.Should().BeEmpty(); empty.TotalCount.Should().Be(125);
        var invalid = async () => await fixture.Plans.GetOffsetHistoryAsync(original.Position.Id, original.Position.StrategyKind, original.ValueDate, -1, 50);
        await invalid.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Retained_snapshot_fields_round_trip_while_removed_legacy_columns_remain_null()
    {
        var plan = Plan();
        plan = plan with { IronCondorTradePlanSnapshot = new()
        {
            Position = plan.Position, SourceEventId = Guid.NewGuid(), SequenceId = plan.PlanRevision, ValueDate = plan.ValueDate,
            MaxProfit = 850, MaxLoss = -500, MinProfitTarget = 100, DailyProfitTarget = 50,
            StopLossLimit = .2, VolatilityPrice = 18.25m, AssetPrice = 6000, ForwardPrice = 10,
            CalculationStatus = "Unavailable", UnavailableReasons = ["retained-field roundtrip"]
        } };
        plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        { await fixture.Plans.ProjectMaterialAsync(transaction, plan, token); return true; });
        (await fixture.Plans.GetCurrentAsync(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate)).Should().BeEquivalentTo(plan);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT maxProfit,maxLoss,minProfitTarget,dailyProfitTarget,stopLossLimit,volatilityPrice,assetPrice,forwardPrice,mScore,marketTrend FROM trade_plan.iron_condor_trade_plan WHERE positionId=$1 AND planRevision=$2;", connection);
        command.Parameters.AddWithValue(plan.Position.Id.PositionId); command.Parameters.AddWithValue(plan.PlanRevision);
        await using var row = await command.ExecuteReaderAsync();
        (await row.ReadAsync()).Should().BeTrue();
        row.GetDecimal(0).Should().Be(850); row.GetDecimal(1).Should().Be(-500);
        row.GetDecimal(2).Should().Be(100); row.GetDecimal(3).Should().Be(50);
        row.GetDouble(4).Should().Be(.2); row.GetDecimal(5).Should().Be(18.25m);
        row.GetDecimal(6).Should().Be(6000); row.GetDecimal(7).Should().Be(10);
        row.IsDBNull(8).Should().BeTrue(); row.IsDBNull(9).Should().BeTrue();
    }

    [Fact]
    public async Task Event_plan_and_activity_commit_together_and_scalar_values_round_trip()
    {
        var plan = Plan();
        plan = plan with { IronCondorTradePlanSnapshot = new() { Position = plan.Position, SourceEventId = Guid.NewGuid(), SequenceId = 1, DailyPnl = -42m, ForwardLossRatio = 0.8, ValueDate = plan.ValueDate } };
        plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
        var source = Source(plan);
        var stream = "plan-atomic-" + Guid.NewGuid().ToString("N");
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(stream, source.CommandId, source, 0, token);
            await fixture.Plans.ProjectMaterialAsync(transaction, plan, token);
            return true;
        });
        (await fixture.EventCountAsync(stream)).Should().Be(1);
        (await fixture.Plans.GetCurrentAsync(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate)).Should().BeEquivalentTo(plan);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection);
        await connection.OpenAsync();
        await using var scalars = new NpgsqlCommand("SELECT dailyPnl,forwardLossRatio,sequenceId FROM trade_plan.iron_condor_trade_plan WHERE positionId=$1 AND planRevision=$2;", connection);
        scalars.Parameters.AddWithValue(plan.Position.Id.PositionId);
        scalars.Parameters.AddWithValue(plan.PlanRevision);
        await using var scalarReader = await scalars.ExecuteReaderAsync();
        (await scalarReader.ReadAsync()).Should().BeTrue();
        scalarReader.GetDecimal(0).Should().Be(-42m);
        scalarReader.GetDouble(1).Should().Be(0.8);
        scalarReader.GetInt64(2).Should().Be(1);
        var activity = await fixture.Plans.GetActivityAsync(plan.ValueDate, 500);
        activity.Items.Should().Contain(x => x.Position.Id == plan.Position.Id && x.PlanRevision == 1);
    }

    [Fact]
    public async Task Sql_failure_after_event_and_plan_writes_rolls_back_all_rows_without_retry()
    {
        var plan = Plan(); var source = Source(plan); var stream = "plan-rollback-" + Guid.NewGuid().ToString("N"); var attempts = 0;
        await Assert.ThrowsAsync<PostgresException>(() => fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            attempts++;
            await transaction.AppendAsync(stream, source.CommandId, source, 0, token);
            await fixture.Plans.ProjectMaterialAsync(transaction, plan, token);
            await transaction.ExecuteAsync("SELECT 1/0;", [], token);
            return true;
        }));
        attempts.Should().Be(1);
        (await fixture.EventCountAsync(stream)).Should().Be(0);
        (await fixture.Plans.GetCurrentAsync(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate)).Should().BeNull();
        var activity = await fixture.Plans.GetActivityAsync(plan.ValueDate, 500);
        activity.Items.Should().NotContain(x => x.Position.Id == plan.Position.Id);
    }

    [Fact]
    public async Task Confirmed_deadlock_is_not_retried_for_disposable_plan_transaction()
    {
        var attempts = 0;
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.Transactions.ExecuteOnceAsync<int>(async (transaction, token) =>
        {
            attempts++;
            await transaction.ExecuteAsync("DO $$ BEGIN RAISE EXCEPTION 'injected deadlock' USING ERRCODE='40P01'; END $$;", [], token);
            return 0;
        }));
        error.SqlState.Should().Be("40P01"); attempts.Should().Be(1);
    }

    [Fact]
    public async Task Ordered_history_pages_have_no_cap_or_duplicate_and_reject_other_position_cursor()
    {
        var initial = Plan();
        for (var revision = 1; revision <= 7; revision++)
        {
            var plan = initial with { PlanRevision = revision };
            plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
            await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) => { await fixture.Plans.ProjectMaterialAsync(transaction, plan, token); return true; });
        }
        var revisions = new List<long>(); byte[]? cursor = null;
        do
        {
            var page = await fixture.Plans.GetHistoryAsync(initial.Position.Id, initial.Position.StrategyKind, initial.ValueDate, 2, cursor);
            revisions.AddRange(page.Items.Select(x => x.PlanRevision)); cursor = page.PagingState;
        } while (cursor is not null);
        revisions.Should().Equal(7, 6, 5, 4, 3, 2, 1);
        var first = await fixture.Plans.GetHistoryAsync(initial.Position.Id, initial.Position.StrategyKind, initial.ValueDate, 2);
        var other = Plan();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Plans.GetHistoryAsync(other.Position.Id, other.Position.StrategyKind, other.ValueDate, 2, first.PagingState));
    }

    [Fact]
    public async Task Conflicting_revision_does_not_overwrite_persisted_plan()
    {
        var plan = Plan();
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) => { await fixture.Plans.ProjectMaterialAsync(transaction, plan, token); return true; });
        var conflict = plan with { ContentHash = "different-content", TotalPnl = -500m };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Transactions.ExecuteOnceAsync(async (transaction, token) => { await fixture.Plans.ProjectMaterialAsync(transaction, conflict, token); return true; }));
        (await fixture.Plans.GetCurrentAsync(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate)).Should().BeEquivalentTo(plan);
    }
    [Fact]
    public async Task Standalone_snapshot_conflict_is_logged_and_dropped_without_changing_the_saved_revision()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<DbProvider>>();
        logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error).Returns(true);
        var target = new TradePlanDbContext(fixture.Settings, logger);
        var plan = Plan();
        await target.ProjectMaterialAsync(plan);
        await target.ProjectMaterialAsync(plan with { ContentHash = "conflicting-snapshot", TotalPnl = -999m });
        (await target.GetCurrentAsync(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate)).Should().BeEquivalentTo(plan);
        logger.ReceivedCalls().Should().Contain(call => call.GetMethodInfo().Name == "Log" && call.GetArguments().Any(argument => argument is InvalidOperationException));
    }
}
