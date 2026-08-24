using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Event.Extensions;

/// <summary>Exposes readonly FuturesAtrSignalEvent Event context properties.</summary>
public static class FuturesAtrSignalEventContextExtensions
{
    extension(IEventActorContext<FuturesAtrSignalEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesAtrSignalEventContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesAtrSignalEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the StatusConsoleWriter service retained by the typed context.</summary>
        public IStatusConsoleWriter StatusConsoleWriter => context.DomainContext.StatusConsoleWriter;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesAtrSignalEventActor> Logger => context.DomainContext.Logger;
        /// <summary>Gets the MarketDataApi service retained by the typed context.</summary>
        public IMarketDataApi MarketDataApi => context.DomainContext.MarketDataApi;
    }
}
