using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.EventModelActor;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesMarketPrice.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[CollectionDefinition("MarketPriceDeliverySafety", DisableParallelization = true)]
public sealed class MarketPriceDeliverySafetyCollection { }
[Collection("MarketPriceDeliverySafety")]
public sealed class MarketPriceDeliverySafetyTests
{
    [Fact]
    public async Task Primary_market_price_records_failed_legacy_trade_forward_as_failed_settlement()
    {
        // Exception-based failure is already observed by the actual mailbox metrics/scheduler.
        AppContext.TryGetSwitch("IFM.RawFuturesDurableStorage", out var previous);
        AppContext.SetSwitch("IFM.RawFuturesDurableStorage", false);
        var id = new ActorMailboxId(ActorType.Realtime, FuturesMarketPriceRealtimeActor.ActorName);
        var context = Substitute.For<IFuturesMarketPriceRealtimeContext>();
        context.ActorId.Returns(id); context.Logger.Returns(NullLogger<FuturesMarketPriceRealtimeActor>.Instance);
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>())
            .Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var mailbox = new ActorMailbox(supervisor, id); supervisor.CreateMailbox(id).Returns(mailbox);
        var actor = new FuturesMarketPriceRealtimeActor(context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var entity = new TickDataEntityId("ES-SAFETY", new(2026, 8, 25), AssetTypeId.Futures);
        var source = new FuturesMarketPriceUpdatedRealtimeEvent
        {
            Id = Guid.NewGuid(), EntityId = entity, Subject = new(ActorType.Realtime, id.Name, FuturesMarketPriceUpdatedRealtimeEvent.Verb, entity.Format()),
            UpdateSource = FuturesMarketPriceUpdateSource.Trade,
            SourceTrade = new FuturesTickTradeDataChangedEvent
            {
                Id = Guid.NewGuid(), EntityId = entity, Subject = new(ActorType.Realtime, FuturesTickTradeDataChangedEvent.Actor,
                    FuturesTickTradeDataChangedEvent.Verb, entity.Format()), Dataset = "GLBX.MDP3",
                TickDataId = new(entity.ContractId, entity.ValueDate, 1, new(2026, 8, 25, 14, 30, 0, DateTimeKind.Utc)),
                TradeData = new FuturesTickTradeData(42, 100, 101, 0, 5_450_250_000_000, 5450.25m, 3, (byte)'T', (byte)'B', 0)
            }
        };
        context.SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(Arg.Any<FuturesTickTradeDataChangedEvent>())
            .Returns(_ => ValueTask.FromException(new IOException("Injected required trade-storage publication failure; nothing accepted.")));
        var message = Substitute.For<IActorMessage, IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject);
        message.AsEvent<FuturesMarketPriceUpdatedRealtimeEvent>().Returns(source);
        var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
        try
        {
            await actor.StartAsync(supervisor); Assert.True(await mailbox.ThreadQueues.WriteAsync(message));
            Assert.False(await settled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            await context.Received(1).SendAsync<FuturesTickTradeDataChangedEvent, TickDataEntityId>(Arg.Any<FuturesTickTradeDataChangedEvent>());
        }
        finally { await actor.StopAsync(); AppContext.SetSwitch("IFM.RawFuturesDurableStorage", previous); }
    }
}
