using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Model;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Event.Extensions;

/// <summary>Exposes readonly FuturesAdxSignalEvent Event context properties.</summary>
public static class FuturesAdxSignalEventContextExtensions
{
    extension(IEventActorContext<FuturesAdxSignalEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesAdxSignalEventContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesAdxSignalEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the StatusConsoleWriter service retained by the typed context.</summary>
        public IStatusConsoleWriter StatusConsoleWriter => context.DomainContext.StatusConsoleWriter;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesAdxSignalEventActor> Logger => context.DomainContext.Logger;
        /// <summary>Gets the MarketDataApi service retained by the typed context.</summary>
        public IMarketDataApi MarketDataApi => context.DomainContext.MarketDataApi;
    }
}
