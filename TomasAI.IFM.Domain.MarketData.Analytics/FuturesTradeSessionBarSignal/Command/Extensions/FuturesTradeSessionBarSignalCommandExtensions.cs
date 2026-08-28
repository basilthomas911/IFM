using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Extensions;

/// <summary>Exposes trade-session bar signal Command services as readonly extension properties.</summary>
public static class FuturesTradeSessionBarSignalCommandExtensions
{
    extension(ICommandActorContext<FuturesTradeSessionBarSignalCommandActor> context)
    {
        /// <summary>Gets the typed publisher context.</summary>
        public IFuturesTradeSessionBarSignalCommandContext BarSignalContext =>
            context as IFuturesTradeSessionBarSignalCommandContext
            ?? throw new InvalidOperationException("The bar signal Command actor requires its typed context.");
        /// <summary>Gets the event-source state repository.</summary>
        public IEventSourceActorStateRepository<FuturesTradeSessionBarSignalCommandState> BarSignalRepository =>
            context.BarSignalContext.Repository;
        /// <summary>Gets the durable EventProjector.</summary>
        public IEventProjector<FuturesTradeSessionBarSignalCommandActor> BarSignalProjector =>
            context.BarSignalContext.EventProjector;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesTradeSessionBarSignalCommandActor> Logger => context.BarSignalContext.Logger;
    }
}
