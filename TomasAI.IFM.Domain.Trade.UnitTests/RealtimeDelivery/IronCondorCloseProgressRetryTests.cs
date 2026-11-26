using MessagePack;
using Npgsql;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Persistence;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class IronCondorCloseProgressRetryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Closed_route_identity_preserves_origin_and_separates_authoritative_revisions(int originMode)
    {
        var original = IronCondorPositionDeliveryFixture.Position(7) with { RouteGeneration = 3, IsOpen = false };
        var source = new IronCondorTradePlanUpdatedEvent { Id = Guid.NewGuid(), ReceivedOn = original.AsOfUtc };
        MessageOrigin? origin = originMode == 2 ? new MessageOrigin(new DateTimeOffset(source.ReceivedOn), "closed-routes") : null;
        if (originMode > 0) MessageOrigins.Restore(source, origin);
        var first = IronCondorCloseProgress.ClosedRoutes(source, original);
        var retry = IronCondorCloseProgress.ClosedRoutes(source, original);
        var next = IronCondorCloseProgress.ClosedRoutes(source, original with { PositionSequence = 8, RealizedPnl = 12 });
        Assert.Equal(MessagePackSerializer.Serialize(first), MessagePackSerializer.Serialize(retry));
        Assert.Equal(source.ReceivedOn, retry.ReceivedOn); Assert.Equal(origin, MessageOrigins.ForPublication(retry, TimeProvider.System));
        Assert.NotEqual(first.Id, next.Id);
        Assert.NotEqual(first.Id, IronCondorCloseProgress.ClosedRoutes(source, original with { RouteGeneration = 4 }).Id);
        var root = Path.Combine(Path.GetTempPath(), "ifm-close-control-identity", Guid.NewGuid().ToString("N"));
        await using var capture = new StrictRealtimeCaptureFixture(root);
        var receipt = await capture.CaptureAsync(first);
        Assert.True((await capture.CaptureAsync(retry)).AlreadyCaptured);
        Assert.False((await capture.CaptureAsync(next)).AlreadyCaptured);
        // Same authoritative revision with conflicting facts is rejected, never silently aliased.
        await Assert.ThrowsAsync<InvalidDataException>(async () => await capture.CaptureAsync(
            IronCondorCloseProgress.ClosedRoutes(source, original with { RealizedPnl = 13 })));
        Assert.NotEmpty(receipt.SourceSha256);
    }

    [Fact]
    [Trait("Category", "DisposablePostgres")]
    public async Task Accepted_route_capture_then_failed_feed_cleanup_retries_after_restart_with_identical_source_hash()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("closecontrol", includeCql: false);
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres");
        var transactions = new PostgresEventTransaction(settings);
        await using var sql = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString) {
            Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString); await sql.OpenAsync();
        await using (var ddl = new NpgsqlCommand("CREATE SCHEMA trade_plan; CREATE SCHEMA trade; " + TradePlanSchemaSql.IronCondorExitIntentCreate +
            "CREATE TABLE trade.live_feed_recovery(trade_id text,value_date date);", sql)) await ddl.ExecuteNonQueryAsync();
        var position = IronCondorPositionDeliveryFixture.Position(7) with { RouteGeneration = 3, IsOpen = false };
        var plan = new StrategyTradePlanSnapshot { Position = position, ValueDate = position.ValueDate, State = TradePlanState.Closed };
        var source = new IronCondorTradePlanUpdatedEvent { Id = Guid.NewGuid(), ReceivedOn = position.AsOfUtc, Plan = plan };
        MessageOrigins.Restore(source, new MessageOrigin(new DateTimeOffset(source.ReceivedOn), "close-control-recovery"));
        await using (var seed = new NpgsqlCommand("INSERT INTO trade_plan.iron_condor_exit_intent(positionid,payload) VALUES($1,$2);", sql))
        { seed.Parameters.AddWithValue(position.Id.PositionId); seed.Parameters.AddWithValue(MessagePackBinarySerializer.Shared.Serialize(source)); await seed.ExecuteNonQueryAsync(); }
        var directory = Path.Combine(Path.GetTempPath(), "ifm-close-control-recovery", Guid.NewGuid().ToString("N"));
        StrictRealtimeCaptureFixture? capture = new(directory);
        try
        {
            var producer = Substitute.For<IActorProducer>(); var market = Substitute.For<IMarketDataApi>();
            var steps = new List<string>(); var receipts = new List<RealtimeCaptureReceipt>(); var routes = new List<byte[]>();
            async ValueTask SendRoutesAsync(OpenPositionRoutesChangedEvent value)
            { steps.Add("route"); routes.Add(MessagePackSerializer.Serialize(value)); receipts.Add(await capture!.CaptureAsync(value)); }
            producer.SendAsync<OpenPositionRoutesChangedEvent, StrategyPositionId>(Arg.Any<ActorSubject>(), Arg.Any<OpenPositionRoutesChangedEvent>(), Arg.Any<CancellationToken>())
                .Returns(call => SendRoutesAsync(call.Arg<OpenPositionRoutesChangedEvent>()));
            producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
                .Returns(_ => { steps.Add("final-notify"); return ValueTask.CompletedTask; });
            var disableAttempts = 0;
            market.DisableTradeFeedRecoveryAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns(_ => { steps.Add("disable"); return Task.FromResult(++disableAttempts > 1); });
            var cache = new TradeRealtimeCacheRegistry();
            var trade = new EstablishedTradeDefinition { Id = position.Id.Trade, StrategyKind = TradeStrategyKind.IronCondor };
            var first = new IronCondorCloseProgress(Substitute.For<IDbContextFactory>(), transactions, producer, market, cache,
                new Clock(position.AsOfUtc));
            await Assert.ThrowsAsync<InvalidOperationException>(() => first.CompleteClosedAsync(source, position, position, trade, plan, default));
            Assert.Equal(new[] { "route", "disable" }, steps); Assert.False(receipts.Single().AlreadyCaptured);
            await using (var check = new NpgsqlCommand("SELECT finalized FROM trade_plan.iron_condor_exit_intent;", sql)) Assert.False((bool)(await check.ExecuteScalarAsync())!);
            await capture.DisposeAsync(); capture = new StrictRealtimeCaptureFixture(directory);
            var restarted = new IronCondorCloseProgress(Substitute.For<IDbContextFactory>(), transactions, producer, market, cache,
                new Clock(position.AsOfUtc.AddHours(1)));
            await restarted.CompleteClosedAsync(source, position, position, trade, plan, default);
            Assert.Equal(new[] { "route", "disable", "route", "disable", "final-notify" }, steps);
            Assert.True(receipts[1].AlreadyCaptured); Assert.Equal(receipts[0].SourceSha256, receipts[1].SourceSha256);
            Assert.Equal(routes[0], routes[1]); Assert.Null(cache.Find(position.Id.Trade));
            await using var final = new NpgsqlCommand("SELECT finalized FROM trade_plan.iron_condor_exit_intent;", sql);
            Assert.True((bool)(await final.ExecuteScalarAsync())!);
        }
        finally { if (capture is not null) await capture.DisposeAsync(); }
    }
    sealed class Clock(DateTime at) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(at); }
}
