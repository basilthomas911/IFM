using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesRsiSignal;

public class FuturesRsiSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesRsiSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesRsiSignalEventActor : FuturesRsiSignalEventActor
    {
        public TestableFuturesRsiSignalEventActor(
            IActorSupervisor supervisor,
            IMarketDataApi marketDataApi,
            IStatusConsoleWriter statusConsoleWriter,
            ILogger<FuturesRsiSignalEventActor> logger,
            IBlackboardService blackboardService)
            : base(new FuturesRsiSignalEventContext(
                supervisor,
                marketDataApi,
                statusConsoleWriter,
                logger,
                blackboardService))
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext<FuturesRsiSignalEventActor> context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext<FuturesRsiSignalEventActor> context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext<FuturesRsiSignalEventActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);

        public async ValueTask InvokeOnStartAsync(IEventActorContext<FuturesRsiSignalEventActor> context)
            => await OnStartup(context);

        public async ValueTask InvokeOnStopAsync(IEventActorContext<FuturesRsiSignalEventActor> context)
            => await OnShutdown(context);
    }


}
