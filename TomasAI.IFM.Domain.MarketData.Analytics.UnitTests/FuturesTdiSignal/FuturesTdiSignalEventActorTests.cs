using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Event;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTdiSignal;

public class FuturesTdiSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesTdiSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTdiSignalEventActor : FuturesTdiSignalEventActor
    {
        public TestableFuturesTdiSignalEventActor(IActorSupervisor supervisor, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesTdiSignalEventActor> logger)
            : base(supervisor, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IActorState state, IEvent @event)
            => await ReceiveAsync(context, state, @event);

        public async ValueTask<IActorState> InvokeOnLoadStateAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event)
            => await OnLoadStateAsync(context, threadId, @event);

        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);

        public async ValueTask InvokeOnStartup(IEventActorContext context)
            => await OnStartup(context);

        public async ValueTask InvokeOnShutdown(IEventActorContext context)
            => await OnShutdown(context);
    }

}
