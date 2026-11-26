using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;

public sealed class IronCondorPublicationRecoveryAuditTests
{
    [Fact]
    public async Task Failed_plan_send_is_retried_when_the_same_plan_is_already_in_cache()
    {
        var supervisor = Substitute.For<IActorSupervisor>(); var producer = Substitute.For<IActorProducer>();
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var attempts = 0;
        producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { attempts++; return ValueTask.FromException(new IOException("Injected publication failure.")); });
        var cache = new TradeRealtimeCacheRegistry(); var position = IronCondorPositionDeliveryFixture.Position(1);
        cache.Register(new EstablishedTradeDefinition { Id = position.Id.Trade, StrategyKind = TradeStrategyKind.IronCondor }, position.ValueDate);
        using var captures = new IronCondorPositionRequests();
        var context = new IronCondorTradePositionRealtimeContext(supervisor, NullLogger<IronCondorTradePositionRealtimeActor>.Instance, realtimeCache: cache, requests: captures);
        var plan = new StrategyTradePlanSnapshot { Position = position, ValueDate = position.ValueDate, PlanRevision = 1 };
        await Assert.ThrowsAsync<IOException>(() => context.PublishRealtimeAsync(position, plan).AsTask());
        Assert.Equal(1, attempts);
        producer.SendAsync<TradeRealtimeSnapshotChangedEvent, TradeEntityId>(Arg.Any<ActorSubject>(), Arg.Any<TradeRealtimeSnapshotChangedEvent>(), Arg.Any<CancellationToken>())
            .Returns(_ => { attempts++; return ValueTask.CompletedTask; });
        await context.PublishRealtimeAsync(position, plan);
        Assert.Equal(2, attempts);
        Assert.True(cache.HasPublishedPosition(position.Id.Trade, position.RouteGeneration, position.PositionSequence));
        Assert.Empty(captures.Publications.Pending());
        Assert.Equal(1, cache.Find(position.Id.Trade)!.Read().Plan!.PlanRevision);
    }
}
