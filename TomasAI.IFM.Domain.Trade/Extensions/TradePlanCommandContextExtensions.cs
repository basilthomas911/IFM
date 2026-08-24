using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Exposes readonly TradePlanCommand Command context properties.</summary>
public static class TradePlanCommandContextExtensions
{
    extension(ICommandActorContext<TradePlanCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ITradePlanCommandActorContext DomainContext =>
            IsArgumentNull.Set(context as ITradePlanCommandActorContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the EventProducer service retained by the typed context.</summary>
        public ITradeEventProducer EventProducer => context.DomainContext.EventProducer;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<TradePlanCommandActor> Logger => context.DomainContext.Logger;
    }
}
