using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class EodStatisticsDeliverySafetyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EOD_statistics_wait_for_projection_and_do_not_ACK_a_rejected_result(bool workers)
    {
        // Both paths retain source ownership while required work is pending and report failure at settlement.
        var id = new ActorMailboxId(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName);
        var context = Substitute.For<IFuturesEodDataRealtimeContext>(); context.ActorId.Returns(id);
        context.Logger.Returns(NullLogger<FuturesEodDataRealtimeActor>.Instance);
        context.EnableAsyncTradeWorker.Returns(workers); context.TradeDispatch.Returns(new FuturesEodTradeDispatchState());
        context.MarketDataApi.Returns(Substitute.For<IMarketDataApi>());
        context.StatusConsoleWriter.Returns(Substitute.For<IStatusConsoleWriter>());
        var projector = Substitute.For<IRealtimeProjector<FuturesEodDataRealtimeActor>>(); context.Projector.Returns(projector);
        var projectionAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        projector.ProcessRealtimeEventAsync(Arg.Any<IEvent>()).Returns(_ => { projectionAttempted.TrySetResult(); return ValueTask.FromResult(false); });
        var queryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var query = new TaskCompletionSource<ServiceResult<FuturesEodDataV2ReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.RequestAsync<FuturesEodDataV2ReadModel, GetFuturesEodDataQuery>(Arg.Any<GetFuturesEodDataQuery>())
            .Returns(_ => { queryStarted.TrySetResult(); return new ValueTask<ServiceResult<FuturesEodDataV2ReadModel>>(query.Task); });
        var supervisor = Substitute.For<IActorSupervisor>(); var container = Substitute.For<IContainerInstance>();
        container.Resolve<IActorThreadQueue>().Returns(_ => new ActorThreadQueueV2(16)); supervisor.Container.Returns(container);
        supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var pool = new ActorThreadPoolV2(supervisor, NullLogger.Instance); pool.Initialize(1); supervisor.ThreadPool.Returns(pool);
        supervisor.GetThread(Arg.Any<ActorThreadId>()).Returns(call => pool.GetThread(call.Arg<ActorThreadId>()));
        supervisor.GetThreadAsync(Arg.Any<ActorThreadId>(), Arg.Any<CancellationToken>())
            .Returns(call => pool.GetThreadAsync(call.Arg<ActorThreadId>(), call.Arg<CancellationToken>()));
        var mailbox = new ActorMailbox(supervisor, id); supervisor.CreateMailbox(id).Returns(mailbox);
        var actor = new FuturesEodDataRealtimeActor(context);
        supervisor.Children.Returns(new Dictionary<ActorMailboxId, IActor> { [id] = actor });
        var date = new DateOnly(2026, 8, 25);
        var source = new FuturesSessionStatisticsUpdatedRealtimeEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = new("ES-EOD-SAFETY", date),
            Subject = new(ActorType.Realtime, FuturesSessionStatisticsUpdatedRealtimeEvent.Actor, FuturesSessionStatisticsUpdatedRealtimeEvent.Verb, "same"),
            Statistics = new("ES-EOD-SAFETY", date, 98m, 110m, 90m, 1, 1, 20, FuturesSessionVolumeQuality.OfficialFinal)
        };
        var message = Substitute.For<IActorMessage, IActorDeliveryCompletion>();
        message.Subject.Returns(new ActorSubject(ActorType.Realtime, id.Name, source.Subject.Verb, "same"));
        message.AsEvent<FuturesSessionStatisticsUpdatedRealtimeEvent>().Returns(source);
        var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ((IActorDeliveryCompletion)message).CompleteDeliveryAsync(Arg.Any<bool>()).Returns(call => { settled.TrySetResult(call.Arg<bool>()); return ValueTask.CompletedTask; });
        try
        {
            await actor.StartAsync(supervisor); Assert.True(await mailbox.ThreadQueues.WriteAsync(message));
            await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (workers)
            {
                Assert.False(settled.Task.IsCompleted);
                Assert.False(query.Task.IsCompleted);
            }
            query.TrySetResult(new ServiceOk<FuturesEodDataV2ReadModel>(new FuturesEodDataV2ReadModel
            { ContractId = source.EntityId.ContractId, ValueDate = date, Symbol = "ES", OpenPrice = 99m, ClosePrice = 100m, HighPrice = 110m, LowPrice = 90m, Volume = 10 }));
            await projectionAttempted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(await settled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            await projector.Received(1).ProcessRealtimeEventAsync(Arg.Any<IEvent>());
        }
        finally
        {
            query.TrySetResult(new ServiceFailed<FuturesEodDataV2ReadModel>(-1, "Fixture cleanup"));
            await actor.StopAsync();
        }
    }
}
