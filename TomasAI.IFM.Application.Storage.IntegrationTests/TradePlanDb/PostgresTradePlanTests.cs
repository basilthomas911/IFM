using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Query.Actor;
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
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using TomasAI.IFM.IntegrationTesting;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[CollectionDefinition("PostgreSQL plan atomicity", DisableParallelization = true)]
public sealed class PostgresTradePlanCollection : ICollectionFixture<PostgresTradePlanFixture>;

public sealed class PostgresTradePlanFixture : IAsyncLifetime
{
    readonly IsolatedIntegrationInfrastructure infrastructure = new("portfolioatomicity", includeCql: false);
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Daily_forward_chart_uses_final_observation_per_date_at_eod_and_preserves_missing_values(bool rawClosingCost)
    {
        var position = Plan().Position.Id;
        var date = new DateOnly(2026, 10, 30);
        // EOD crosses the Eastern daylight-saving transition between these two sessions.
        var firstEnd = new DateTime(2026, 10, 30, 21, 0, 0, DateTimeKind.Utc);
        var secondEnd = new DateTime(2026, 11, 2, 22, 0, 0, DateTimeKind.Utc);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection);
        await connection.OpenAsync();
        long revision = 0;
        async Task Insert(DateOnly day, DateTime at, decimal? forward, Guid? otherPosition = null)
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO trade_plan.iron_condor_trade_plan
                (portfolioId,fundId,orderId,tradeId,positionId,valueDate,planRevision,calculatedAtUtc,combinedForwardPrice,calculationVersion)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10)
                """, connection);
            PostgresValueBinding.AddParameters(command, [position.Trade.PortfolioId, position.Trade.FundId,
                position.Trade.OrderId, position.Trade.TradeId, otherPosition ?? position.PositionId, day, ++revision, at,
                TomasAI.IFM.Shared.Storage.BindValues.Numeric(forward),
                rawClosingCost ? "IronCondor/EntryCreditForwardPnl/v2" : "LegacyScenario/v1"]);
            await command.ExecuteNonQueryAsync();
        }
        decimal Signed(decimal price) => rawClosingCost ? -price : price;
        await Insert(date, firstEnd.AddMinutes(-1), Signed(-10m));
        await Insert(date, firstEnd, Signed(-11m));
        await Insert(date, firstEnd, Signed(-12m)); // Revision resolves timestamp ties.
        await Insert(date, firstEnd.AddSeconds(1), Signed(-99m));
        await Insert(date, firstEnd, Signed(-99m), Guid.NewGuid());
        var secondDate = new DateOnly(2026, 11, 2);
        await Insert(secondDate, secondEnd.AddMinutes(-1), Signed(-13m));
        await Insert(secondDate, secondEnd, null); // Do not substitute an earlier valid calculation.
        var missingDate = secondDate.AddDays(1);
        var points = await fixture.Plans.GetDailyForwardSpreadsAsync(position, new Dictionary<DateOnly, DateTime> {
            [date] = firstEnd, [secondDate] = secondEnd, [missingDate] = secondEnd.AddDays(1) });
        points.Count.Should().Be(2);
        points[date].Should().Be(-12m);
        points[secondDate].Should().BeNull();
        points.ContainsKey(missingDate).Should().BeFalse();
    }

    static StrategyTradePlanSnapshot Plan(long revision = 1) => TradePlanStorageTests.Plan(Random.Shared.Next(6_000_000, 9_000_000), revision, -42m, true);
    static IronCondorTradePlanUpdatedEvent Source(StrategyTradePlanSnapshot plan) => new()
    {
        Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), Plan = plan, ReceivedOn = DateTime.UtcNow,
        AggregateId = plan.Position.Id.PositionId.ToString()
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_chart_samples_latest_quarter_hour_across_full_session_and_excludes_outside_bounds(bool rawClosingCost)
    {
        var position=Plan().Position.Id;
        var start=new DateTime(2026,10,22,22,0,0,DateTimeKind.Utc);
        var end=start.AddHours(23);
        await using var connection=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection);
        await connection.OpenAsync();
        long revision=0;
        async Task Insert(DateOnly date,DateTime at,decimal price,Guid? differentPosition=null)
        {
            await using var command=new NpgsqlCommand("""
                INSERT INTO trade_plan.iron_condor_trade_plan
                (portfolioId,fundId,orderId,tradeId,positionId,valueDate,planRevision,calculatedAtUtc,combinedSpreadPrice,combinedForwardPrice,calculationVersion)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
                """,connection);
            foreach(var value in new object[]{position.Trade.PortfolioId,position.Trade.FundId,position.Trade.OrderId,
                position.Trade.TradeId,differentPosition??position.PositionId,date,++revision,at,price,
                rawClosingCost?-price+0.5m:price-0.5m,rawClosingCost?"IronCondor/EntryCreditForwardPnl/v2":"LegacyScenario/v1"})
                command.Parameters.Add(new NpgsqlParameter{Value=value});
            await command.ExecuteNonQueryAsync();
        }
        var first=new DateOnly(2026,10,23);var last=first;
        await Insert(first,start.AddSeconds(-1),-99m);
        await Insert(first,start,-1m);
        await Insert(first,start.AddHours(4),-2m);
        await Insert(last,end.AddMinutes(-30),-3m);
        await Insert(last,end.AddMinutes(-25),-4m);
        await Insert(last,end,-5m);
        await Insert(last,end.AddSeconds(1),-99m);
        await Insert(first.AddDays(-1),start.AddHours(2),-99m);
        await Insert(last,end,-99m,Guid.NewGuid());
        var series=await fixture.Plans.GetChartRangeAsync(position,first,last,start,end);
        series.StartUtc.Should().Be(start);series.EndUtc.Should().Be(end);
        series.Points.Select(p=>p.NetSpread).Should().Equal(-1m,-2m,-4m,-5m);
        series.Points.Select(p=>p.ForwardSpread).Should().Equal(-1.5m,-2.5m,-4.5m,-5.5m);
        var empty=await fixture.Plans.GetChartRangeAsync(position,first,last,start.AddHours(-6),start.AddSeconds(-2));
        empty.Points.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chart_reads_six_hours_of_price_only_samples_separately_from_the_grid_page(bool rawClosingCost)
    {
        var original = Plan();
        var at = DateTime.SpecifyKind(new DateTime(2026, 10, 19, 10, 0, 0), DateTimeKind.Utc);
        foreach (var (revision, hours, price) in new[] { (1,0,-10m), (2,5,-11m), (3,7,-12m), (4,7,-13m) })
        {
            var plan = original with { PlanRevision = revision, CalculatedAtUtc = at.AddHours(hours).AddSeconds(revision == 4 ? 40 : 0),
                IronCondorTradePlanSnapshot = new() { Position = original.Position, CombinedSpreadPrice = price,
                    CalculationVersion = rawClosingCost ? "IronCondor/EntryCreditForwardPnl/v2" : "LegacyScenario/v1",
                    CombinedForwardPrice = rawClosingCost ? -(price - .5m) : price - .5m } };
            plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) };
            await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
            { await fixture.Plans.ProjectMaterialAsync(transaction, plan, token); return true; });
        }
        var first = await TradePlanOffsetHistory.ReadPageAsync(fixture.Plans, original.Position.Id, original.Position.StrategyKind, original.ValueDate, 0, 1);
        first.PagingState.Should().NotBeNullOrEmpty();
        first.Items.Should().HaveCount(1);
        first.Items[0].IronCondorTradePlanSnapshot!.CombinedForwardPrice.Should().Be(rawClosingCost ? 13.5m : -13.5m);
        first.Items[0].IronCondorTradePlanSnapshot!.ForwardSpreadForChart.Should().Be(-13.5m);
        first.ChartEndUtc.Should().BeCloseTo(at.AddHours(7).AddSeconds(40), TimeSpan.FromMicroseconds(1));
        first.ChartPoints.Select(point => point.NetSpread).Should().Equal(-11m,-13m);
        first.ChartPoints.Select(point => point.ForwardSpread).Should().Equal(-11.5m,-13.5m);
        // A new live cycle must see newly persisted samples even within the chart cache's one-minute lifetime.
        var next = original with { PlanRevision = 5, CalculatedAtUtc = at.AddHours(7).AddMinutes(1),
            IronCondorTradePlanSnapshot = new() { Position = original.Position, CombinedSpreadPrice = -14m,
                CalculationVersion = rawClosingCost ? "IronCondor/EntryCreditForwardPnl/v2" : "LegacyScenario/v1",
                CombinedForwardPrice = rawClosingCost ? 14.5m : -14.5m } };
        next = next with { ContentHash = TradePlanContractIdentity.PlanHash(next) };
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        { await fixture.Plans.ProjectMaterialAsync(transaction, next, token); return true; });
        var older = await TradePlanOffsetHistory.ReadPageAsync(fixture.Plans, original.Position.Id, original.Position.StrategyKind,
            original.ValueDate, 1, 1, first.MaximumRevision, first.PagingState);
        older.Items.Select(plan => plan.PlanRevision).Should().Equal(3L);
        older.MaximumRevision.Should().Be(4);
        older.TotalCount.Should().Be(4);
        older.ChartEndUtc.Should().Be(first.ChartEndUtc);
        older.ChartPoints.Should().BeEmpty();
        var refreshed = await TradePlanOffsetHistory.ReadPageAsync(fixture.Plans, original.Position.Id, original.Position.StrategyKind, original.ValueDate, 0, 1);
        refreshed.MaximumRevision.Should().Be(5);
        refreshed.ChartEndUtc.Should().Be(next.CalculatedAtUtc);
        refreshed.ChartPoints.Select(point => point.NetSpread).Should().Equal(-11m, -13m, -14m);
        refreshed.ChartPoints.Select(point => point.ForwardSpread).Should().Equal(-11.5m, -13.5m, -14.5m);
        // Paging against an older revision still retains the original baseline.
        var anchored = await TradePlanOffsetHistory.ReadPageAsync(fixture.Plans, original.Position.Id, original.Position.StrategyKind, original.ValueDate, 0, 1, first.MaximumRevision);
        anchored.ChartEndUtc.Should().Be(first.ChartEndUtc);
        anchored.ChartPoints.Should().BeEquivalentTo(first.ChartPoints);
    }

    [Fact]
    public async Task Sequential_and_offset_pages_return_the_same_rows_at_the_original_revision_anchor()
    {
        var original = Plan();
        async Task Insert(long revision)
        {
            var plan = original with { PlanRevision = revision };
            plan = plan with { ContentHash = TradePlanContractIdentity.PlanHash(plan) + revision };
            await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
            { await fixture.Plans.ProjectMaterialAsync(transaction, plan, token); return true; });
        }
        for (var revision = 1; revision <= 125; revision++) await Insert(2L * revision);
        var factory = Substitute.For<IDbContextFactory>();
        factory.TradePlanDb.Returns(fixture.Plans);
        var context = Substitute.For<IIronCondorTradePlanQueryContext>();
        context.DbFactory.Returns(factory);
        async Task<StrategyTradePlanHistoryPage> Read(int? offset, long? anchor = null, byte[]? cursor = null, DateOnly? date = null)
        {
            var query = new GetIronCondorTradePlanHistoryQuery
            {
                PlanId = new(original.Position.Id, date ?? original.ValueDate),
                PageSize = 50, Offset = offset, MaximumRevision = anchor, PagingState = cursor
            };
            var result = await query.ExecuteAsync(context, default);
            return result.Value!;
        }
        var first = await Read(0);
        first.TotalCount.Should().Be(125);
        first.MaximumRevision.Should().Be(250);
        first.PagingState.Should().NotBeNullOrEmpty();
        first.Items.Should().HaveCount(50);
        first.Items.Select(plan => plan.PlanRevision).Should().Equal(Enumerable.Range(76, 50).Reverse().Select(i => 2L * i));
        var legacyFirst = await Read(null);
        legacyFirst.PagingState.Should().NotBeNullOrEmpty();
        await Insert(252);
        var next = await Read(50, cursor: first.PagingState);
        next.Offset.Should().Be(50); next.TotalCount.Should().Be(125);
        next.MaximumRevision.Should().Be(first.MaximumRevision);
        next.ChartEndUtc.Should().Be(first.ChartEndUtc);
        next.Items.Select(plan => plan.PlanRevision).Should().Equal(Enumerable.Range(26, 50).Reverse().Select(i => 2L * i));
        (await Read(null, cursor: legacyFirst.PagingState)).Items.Should().BeEquivalentTo(next.Items, options => options.WithStrictOrdering());
        var jump = await Read(50, first.MaximumRevision);
        jump.Should().BeEquivalentTo(next, options => options.WithStrictOrdering());
        jump.PagingState.Should().NotBeNullOrEmpty();
        var tail = await Read(100, first.MaximumRevision, jump.PagingState);
        tail.Items.Should().HaveCount(25);
        tail.Items.Select(plan => plan.PlanRevision).Should().Equal(Enumerable.Range(1, 25).Reverse().Select(i => 2L * i));
        tail.PagingState.Should().BeNull();
        tail.Should().BeEquivalentTo(await Read(100, first.MaximumRevision), options => options.WithStrictOrdering());
        var empty = await Read(10000, first.MaximumRevision);
        empty.Items.Should().BeEmpty(); empty.TotalCount.Should().Be(125);
        empty.PagingState.Should().BeNull();
        (await Read(0)).MaximumRevision.Should().Be(252);
        await FluentActions.Awaiting(() => Read(-1)).Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => Read(51, cursor: first.PagingState)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => Read(50, 252, first.PagingState)).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => Read(50, cursor: first.PagingState, date: original.ValueDate.AddDays(1))).Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => Read(50, cursor: [0])).Should().ThrowAsync<ArgumentException>();
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
            await transaction.AppendAsync(new EnlistedEventAppend(stream, source.CommandId, source, 0), token);
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
            await transaction.AppendAsync(new EnlistedEventAppend(stream, source.CommandId, source, 0), token);
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
        await TradePlanProjectionWriter.TryWriteDisposableAsync(target, plan, logger);
        await TradePlanProjectionWriter.TryWriteDisposableAsync(target, plan with { ContentHash = "conflicting-snapshot", TotalPnl = -999m }, logger);
        (await target.GetCurrentAsync(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate)).Should().BeEquivalentTo(plan);
        logger.ReceivedCalls().Should().Contain(call => call.GetMethodInfo().Name == "Log" && call.GetArguments().Any(argument => argument is InvalidOperationException));
    }
}
