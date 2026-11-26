using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class OptionTradeDeliverySafetyTests
{
    [Fact]
    public async Task Option_trade_handler_failure_is_failed_settlement_without_optional_metrics()
    {
        // Required processing failure reaches settlement independently of optional metrics.
        var id = new ActorMailboxId(ActorType.Realtime, FuturesOptionTickDataRealtimeActor.ActorName);
        var context = Substitute.For<IFuturesOptionTickDataRealtimeContext>();
        context.ActorId.Returns(id); context.Logger.Returns(NullLogger<FuturesOptionTickDataRealtimeActor>.Instance);
        context.StatusConsoleWriter.Returns(Substitute.For<IStatusConsoleWriter>());
        var api = Substitute.For<IMarketDataApi>(); context.MarketDataApi.Returns(api);
        api.IsTickDataStreamActive(Arg.Any<string>()).Returns(true);
        api.GetFuturesOptionContractAsync(Arg.Any<string>()).Returns(_ => Task.FromException<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel?>(new IOException("Required option contract lookup failed.")));
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>()).Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var real = new ActorMailbox(supervisor, id); var mailbox = Substitute.For<IActorMailbox>();
        mailbox.ThreadQueues.Returns(real.ThreadQueues); mailbox.Metrics.Returns((IActorMetricsStore)null!);
        supervisor.CreateMailbox(id).Returns(mailbox);
        var actor = new FuturesOptionTickDataRealtimeActor(context); supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var source = new FuturesTickTradeDataInsertedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), AssetTypeId = AssetTypeId.FuturesOption,
            EntityId = new("ES-OPTION-SAFETY", new(2026, 8, 25), AssetTypeId.FuturesOption),
            Subject = new(ActorType.Realtime, id.Name, FuturesTickTradeDataInsertedEvent.Verb, "same")
        };
        var message = Substitute.For<IActorMessage, IActorDeliveryCompletion>(); message.Subject.Returns(source.Subject); message.AsEvent<FuturesTickTradeDataInsertedEvent>().Returns(source);
        var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
        try
        {
            await actor.StartAsync(supervisor); Assert.True(await real.ThreadQueues.WriteAsync(message));
            Assert.False(await settled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            await api.Received(1).GetFuturesOptionContractAsync(source.EntityId.ContractId);
        }
        finally { await actor.StopAsync(); }
    }
}
