using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Extensions;

/// <summary>Exposes readonly FuturesRsiSignalEvent Event context properties.</summary>
public static class FuturesRsiSignalEventContextExtensions
{
    extension(IEventActorContext<FuturesRsiSignalEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesRsiSignalEventContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesRsiSignalEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the MarketDataApi service retained by the typed context.</summary>
        public IMarketDataApi MarketDataApi => context.DomainContext.MarketDataApi;
        /// <summary>Gets the StatusConsoleWriter service retained by the typed context.</summary>
        public IStatusConsoleWriter StatusConsoleWriter => context.DomainContext.StatusConsoleWriter;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesRsiSignalEventActor> Logger => context.DomainContext.Logger;
        /// <summary>Gets the BlackboardService service retained by the typed context.</summary>
        public IBlackboardService BlackboardService => context.DomainContext.BlackboardService;
    }
}
