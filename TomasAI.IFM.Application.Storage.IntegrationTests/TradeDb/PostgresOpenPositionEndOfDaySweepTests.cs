using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[CollectionDefinition("PostgreSQL open-position EOD", DisableParallelization = true)]
public sealed class OpenPositionEodCollection : ICollectionFixture<PostgresTradePlanFixture>;

[Collection("PostgreSQL open-position EOD")]
public sealed class PostgresOpenPositionEndOfDaySweepTests(PostgresTradePlanFixture fixture)
{
    static readonly DateOnly Date = new(2026, 11, 17);
    static readonly DateTime Boundary = FuturesTradingValueDate.GetSessionEndUtc(Date).UtcDateTime;

    async Task<TradeDbContext> Context()
    {
        // Fresh creation, populated upgrade and reapplication use the normal schema entry point.
        await fixture.Transactions.ExecuteOnceAsync((db, ct) => db.ExecuteAsync(TradeSchemaDb.CreateStatement, [], ct));
        return new(new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection, fixture.Connection, "System.Data.Postgres"),
            Mock.Of<IDbContextFactory>(), Mock.Of<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
    }

    static StrategyPositionSnapshot Position(TradeStrategyKind strategy, bool noTick = false, int? portfolio = null)
    {
        var observed = noTick ? Boundary.AddDays(-1).AddHours(-2) : Boundary.AddMinutes(-1);
        return new()
        {
            Id = StrategyPositionId.Create(new(portfolio ?? Random.Shared.Next(60000000, 70000000), 1, 1, 1), strategy),
            StrategyKind = strategy, IsOpen = true, Phase = StrategyPositionPhase.MarkToMarket,
            ValueDate = noTick ? Date.AddDays(-1) : Date, OpeningValueDate = Date.AddDays(-1),
            PositionSequence = 1, RouteGeneration = 1, AsOfUtc = observed, LastMarketObservationUtc = observed,
            HistoryAsOfUtc = observed, HistoryPositionSequence = 1,
            Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "EOD-FIXTURE", SignedQuantity = 1,
                OpeningPrice = 10, CurrentPrice = 12, LastPriceAtUtc = observed, LastSourceSequence = 1 }]
        };
    }

    static OpenPositionEndOfDaySweep Sweep(TradeDbContext db, IStrategyPositionCommandApi commands)
        => new(db, commands, new CmeFuturesMarketSessionCalendar(), new Clock(), new());

    [Theory]
    [InlineData(TradeStrategyKind.IronCondor, false)]
    [InlineData(TradeStrategyKind.IronCondor, true)]
    [InlineData(TradeStrategyKind.VerticalSpread, false)]
    [InlineData(TradeStrategyKind.VerticalSpread, true)]
    [InlineData(TradeStrategyKind.FuturesOutright, false)]
    [InlineData(TradeStrategyKind.FuturesOutright, true)]
    public async Task Missing_EOD_without_routes_or_feed_intent_is_sealed_once_and_old_observation_times_are_retained(TradeStrategyKind strategy, bool noTick)
    {
        var db = await Context();
        var original = Position(strategy, noTick);
        await db.UpsertStrategyPositionAsync(original);
        // Model missing recovery metadata without removing the authoritative owner.
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.ExecuteAsync("DELETE FROM trade.open_position_route WHERE positionId=$1;", [original.Id.PositionId], token);
            await transaction.ExecuteAsync("DELETE FROM trade.open_position_route_recovery WHERE positionId=$1;", [original.Id.PositionId], token);
            return true;
        });
        try
        {
            Assert.Empty(await db.GetOpenPositionRouteSnapshotAsync());
            var commands = new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
            commands.Setup(api => api.EndOfDayAsync(original.Id, strategy, Date, Boundary, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    var machine = new StrategyPositionActorStateMachine();
                    machine.Replay((await db.GetStrategyPositionAsync(original.Id))!);
                    var seal = machine.EndOfDay(Date, Boundary);
                    Assert.True(seal.Accepted, seal.Detail);
                    await db.UpsertStrategyPositionAsync(seal.Value!);
                    return new ServiceOk<Guid>(Guid.NewGuid());
                });
            var sweep = Sweep(db, commands.Object);
            var first = await sweep.RunAsync(default);
            var second = await sweep.RunAsync(default);
            commands.Verify(api => api.EndOfDayAsync(original.Id, strategy, Date, Boundary, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(1, first.Recovered);
            Assert.Equal(0, second.CatchUpAttempts);
            Assert.Equal(noTick ? 1 : 0, second.FailedPositions);
            if (noTick) Assert.Equal("StaleCloseObservation", Assert.Single(second.Issues).Code);
            var current = (await db.GetStrategyPositionAsync(original.Id))!;
            Assert.Equal(Date, current.LatestSealedValueDate);
            Assert.Equal(Boundary, current.CloseBoundaryUtc);
            Assert.Equal(original.LastMarketObservationUtc, current.LastMarketObservationUtc);
            Assert.Equal(original.Legs[0].LastPriceAtUtc, current.Legs[0].LastPriceAtUtc);
            var history = await db.GetStrategyPositionHistoryAsync(original.Id.PositionId, Boundary.AddDays(-4), Boundary, 64);
            Assert.Single(history.Items, row => row.ValueDate == Date && row.Phase == StrategyPositionPhase.EndOfDay);
        }
        finally { await Close(db, original); }
    }

    [Fact]
    public async Task Failed_command_retries_and_a_fresh_whole_position_does_not_hide_a_stale_leg()
    {
        var db = await Context();
        var original = Position(TradeStrategyKind.IronCondor);
        await db.UpsertStrategyPositionAsync(original);
        try
        {
            var commands = new Mock<IStrategyPositionCommandApi>();
            var attempts = 0;
            commands.Setup(api => api.EndOfDayAsync(original.Id, original.StrategyKind, Date, Boundary, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    if (++attempts == 1) return (ServiceResult<Guid>)new ServiceFailed<Guid>(503, "Injected unavailable actor");
                    var machine = new StrategyPositionActorStateMachine(); machine.Replay(original);
                    var seal = machine.EndOfDay(Date, Boundary).Value!;
                    await db.UpsertStrategyPositionAsync(seal with { Legs = [seal.Legs[0] with { LastPriceAtUtc = Boundary.AddHours(-2) }] });
                    return new ServiceOk<Guid>(Guid.NewGuid());
                });
            var sweep = Sweep(db, commands.Object);
            Assert.Equal("MissingSeal", Assert.Single((await sweep.RunAsync(default)).Issues).Code);
            Assert.Equal("StaleCloseObservation", Assert.Single((await sweep.RunAsync(default)).Issues).Code);
            Assert.Equal(2, attempts);
        }
        finally { await Close(db, original); }
    }

    [Fact]
    public async Task Bounded_owner_pages_cover_all_positions_without_routes_and_skip_closed_owners()
    {
        var db = await Context();
        var positions = Enumerable.Range(0, 130).Select(index => Position(TradeStrategyKind.FuturesOutright, portfolio: 80000000 + index)).ToArray();
        foreach (var position in positions) await db.UpsertStrategyPositionAsync(position);
        try
        {
            await Close(db, positions[3]);
            await Context();
            var found = new List<StrategyPositionSnapshot>(); StrategyPositionId? cursor = null;
            while (true)
            {
                var page = await db.GetOpenPositionsForEndOfDayAsync(cursor, 40);
                Assert.True(page.Length <= 40);
                if (page.Length == 0) break;
                found.AddRange(page); cursor = page[^1].Id;
            }
            Assert.Equal(129, found.Count);
            Assert.Equal(129, found.Select(position => position.Id).Distinct().Count());
            Assert.DoesNotContain(found, position => position.Id == positions[3].Id);
        }
        finally { foreach (var position in positions) await Close(db, position); }
    }

    [Fact]
    public async Task Sealed_marker_without_durable_history_is_not_accepted_as_complete()
    {
        var db = await Context();
        var original = Position(TradeStrategyKind.IronCondor);
        var machine = new StrategyPositionActorStateMachine(); machine.Replay(original);
        await db.UpsertStrategyPositionAsync(machine.EndOfDay(Date, Boundary).Value!);
        try
        {
            await fixture.Transactions.ExecuteOnceAsync((transaction, token) => transaction.ExecuteAsync(
                "DELETE FROM trade.strategy_position_history WHERE positionId=$1;", [original.Id.PositionId], token));
            var commands = new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
            var audit = await Sweep(db, commands.Object).RunAsync(default);
            Assert.Equal("MissingSeal", Assert.Single(audit.Issues).Code);
            Assert.Equal(0, audit.CatchUpAttempts);
        }
        finally { await Close(db, original); }
    }

    [Fact]
    public async Task Position_opened_in_the_new_session_requires_no_previous_day_seal()
    {
        var db = await Context();
        var original = Position(TradeStrategyKind.FuturesOutright) with { OpeningValueDate = Date.AddDays(1), ValueDate = Date.AddDays(1) };
        await db.UpsertStrategyPositionAsync(original);
        try
        {
            var commands = new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
            var audit = await Sweep(db, commands.Object).RunAsync(default);
            Assert.Equal(0, audit.CatchUpAttempts); Assert.Equal(0, audit.FailedPositions); Assert.Equal(1, audit.NotRequired);
        }
        finally { await Close(db, original); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Next_close_covers_all_strategy_owners_and_restart_retries_without_duplicate_seals(bool unavailableAtClose)
    {
        var db = await Context();
        var nextDate = Date.AddDays(1);
        var nextBoundary = FuturesTradingValueDate.GetSessionEndUtc(nextDate).UtcDateTime;
        var clock = new MutableClock(new DateTimeOffset(nextBoundary.AddSeconds(-1)));
        var positions = new[] { TradeStrategyKind.IronCondor, TradeStrategyKind.VerticalSpread, TradeStrategyKind.FuturesOutright }
            .Select(kind => Position(kind)).ToArray();
        foreach (var position in positions)
        {
            var machine = new StrategyPositionActorStateMachine(); machine.Replay(position);
            var priorSeal = machine.EndOfDay(Date, Boundary).Value!;
            await db.UpsertStrategyPositionAsync(priorSeal);
            await db.UpsertStrategyPositionAsync(priorSeal with {
                ValueDate = nextDate, Phase = StrategyPositionPhase.MarkToMarket,
                PositionSequence = priorSeal.PositionSequence + 1, HistoryPositionSequence = priorSeal.PositionSequence + 1,
                HistoryAsOfUtc = nextBoundary.AddMinutes(-1), AsOfUtc = nextBoundary.AddMinutes(-1), CloseBoundaryUtc = null,
                LastMarketObservationUtc = nextBoundary.AddMinutes(-1),
                Legs = [priorSeal.Legs[0] with { LastPriceAtUtc = nextBoundary.AddMinutes(-1) }]
            });
        }
        var commands = new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
        var allowCommands = !unavailableAtClose;
        foreach (var owner in positions)
            commands.Setup(api => api.EndOfDayAsync(owner.Id, owner.StrategyKind, nextDate, nextBoundary, It.IsAny<CancellationToken>()))
                .Returns(async () => {
                    if (!allowCommands) return (ServiceResult<Guid>)new ServiceFailed<Guid>(503, "Injected unavailable actor at close");
                    var current = (await db.GetStrategyPositionAsync(owner.Id))!;
                    var machine = new StrategyPositionActorStateMachine(); machine.Replay(current);
                    var seal = machine.EndOfDay(nextDate, nextBoundary); Assert.True(seal.Accepted, seal.Detail);
                    await db.UpsertStrategyPositionAsync(seal.Value!);
                    return new ServiceOk<Guid>(Guid.NewGuid());
                });
        OpenPositionEndOfDaySweep Create(TradeDbContext context) => new(context, commands.Object,
            new CmeFuturesMarketSessionCalendar(), clock, new());
        try
        {
            var before = await Create(db).RunAsync(default);
            Assert.Equal(Date, before.ValueDate); Assert.Equal(0, before.CatchUpAttempts); Assert.Equal(0, before.FailedPositions);
            clock.Now = new DateTimeOffset(nextBoundary);
            var atClose = await Create(db).RunAsync(default);
            Assert.Equal(nextDate, atClose.ValueDate); Assert.Equal(3, atClose.CatchUpAttempts);
            Assert.Equal(unavailableAtClose ? 3 : 0, atClose.FailedPositions);
            allowCommands = true;
            // Fresh context and sweep: no in-memory audit or retry state survives this simulated process restart.
            var reopened = await Context();
            var afterRestart = await Create(reopened).RunAsync(default);
            Assert.Equal(unavailableAtClose ? 3 : 0, afterRestart.CatchUpAttempts);
            Assert.Equal(0, afterRestart.FailedPositions);
            Assert.Equal(0, (await Create(reopened).RunAsync(default)).CatchUpAttempts);
            foreach (var owner in positions)
            {
                var saved = (await reopened.GetStrategyPositionAsync(owner.Id))!;
                Assert.True(saved.IsOpen); Assert.Equal(nextDate, saved.LatestSealedValueDate);
                Assert.Equal(nextBoundary.AddMinutes(-1), saved.Legs[0].LastPriceAtUtc);
                var history = await reopened.GetStrategyPositionHistoryAsync(owner.Id.PositionId, Boundary.AddDays(-1), nextBoundary, 64);
                Assert.Single(history.Items, p => p.ValueDate == nextDate && p.Phase == StrategyPositionPhase.EndOfDay);
                commands.Verify(api => api.EndOfDayAsync(owner.Id, owner.StrategyKind, nextDate, nextBoundary, It.IsAny<CancellationToken>()), Times.Exactly(unavailableAtClose ? 2 : 1));
            }
        }
        finally { foreach (var owner in positions) await Close(db, owner); }
    }

    [Fact]
    public async Task Audit_without_recovery_dependency_detects_stale_seals_after_restart_without_inventing_quotes()
    {
        var db = await Context();
        var original = Position(TradeStrategyKind.IronCondor, noTick: true);
        var machine = new StrategyPositionActorStateMachine(); machine.Replay(original);
        await db.UpsertStrategyPositionAsync(machine.EndOfDay(Date, Boundary).Value!);
        try
        {
            var commands = new Mock<IStrategyPositionCommandApi>(MockBehavior.Strict);
            var first = await Sweep(db, commands.Object).RunAsync(default);
            var restarted = await Sweep(await Context(), commands.Object).RunAsync(default);
            Assert.Equal("StaleCloseObservation", Assert.Single(first.Issues).Code);
            Assert.Equal("StaleCloseObservation", Assert.Single(restarted.Issues).Code);
            Assert.Equal(0, restarted.CatchUpAttempts);
            Assert.Equal(original.Legs[0].LastPriceAtUtc, (await db.GetStrategyPositionAsync(original.Id))!.Legs[0].LastPriceAtUtc);
            commands.VerifyNoOtherCalls();
        }
        finally { await Close(db, original); }
    }

    sealed class MutableClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static async Task Close(TradeDbContext db, StrategyPositionSnapshot original)
    {
        var current = (await db.GetStrategyPositionAsync(original.Id))!;
        await db.UpsertStrategyPositionAsync(current with { IsOpen = false, PositionSequence = current.PositionSequence + 1 });
    }
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 17, 23, 5, 0, TimeSpan.Zero); }
}
