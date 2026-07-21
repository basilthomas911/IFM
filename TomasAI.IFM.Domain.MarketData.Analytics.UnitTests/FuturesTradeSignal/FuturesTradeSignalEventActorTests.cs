using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Event;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.StatusConsole.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesTradeSignal;

public class FuturesTradeSignalEventActorTests : IClassFixture<MarketDataAnalyticsTestFixture>
{
    readonly MarketDataAnalyticsTestFixture _fixture;

    public FuturesTradeSignalEventActorTests(MarketDataAnalyticsTestFixture fixture)
    {
        _fixture = fixture;
    }

    public class TestableFuturesTradeSignalEventActor : FuturesTradeSignalEventActor
    {
        public TestableFuturesTradeSignalEventActor(IActorSupervisor supervisor, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesTradeSignalEventActor> logger)
            : base(supervisor, statusConsoleWriter, logger)
        {
        }

        public IEvent InvokeParseMessage(IEventActorContext context, NatsMsg<byte[]> message)
            => ParseMessage(context, message);

        public async ValueTask InvokeReceiveAsync(IEventActorContext context, IEvent @event)
            => await ReceiveAsync(context, @event);


        public async ValueTask InvokeOnExceptionAsync(IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception ex)
            => await OnExceptionAsync(context, threadId, @event, ex);

        public async ValueTask InvokeOnStartup(IEventActorContext context)
            => await OnStartup(context);

        public async ValueTask InvokeOnShutdown(IEventActorContext context)
            => await OnShutdown(context);
    }

   
}
