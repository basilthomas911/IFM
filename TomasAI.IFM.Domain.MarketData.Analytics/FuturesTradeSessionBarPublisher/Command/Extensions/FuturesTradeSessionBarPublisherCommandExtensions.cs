using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.Extensions;

/// <summary>Exposes trade-session bar publisher Command services as readonly extension properties.</summary>
public static class FuturesTradeSessionBarPublisherCommandExtensions
{
    extension(ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor> context)
    {
        /// <summary>Gets the typed publisher context.</summary>
        public IFuturesTradeSessionBarPublisherCommandContext BarPublisherContext =>
            context as IFuturesTradeSessionBarPublisherCommandContext
            ?? throw new InvalidOperationException("The bar publisher Command actor requires its typed context.");
        /// <summary>Gets the event-source state repository.</summary>
        public IEventSourceActorStateRepository<FuturesTradeSessionBarPublisherCommandState> BarPublisherRepository =>
            context.BarPublisherContext.Repository;
        /// <summary>Gets the durable EventProjector.</summary>
        public IEventProjector<FuturesTradeSessionBarPublisherCommandActor> BarPublisherProjector =>
            context.BarPublisherContext.EventProjector;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesTradeSessionBarPublisherCommandActor> Logger => context.BarPublisherContext.Logger;
    }
}
