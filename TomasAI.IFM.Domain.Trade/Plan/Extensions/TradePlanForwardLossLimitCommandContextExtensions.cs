using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit;

/// <summary>Exposes readonly TradePlanForwardLossLimitCommand Command context properties.</summary>
public static class TradePlanForwardLossLimitCommandContextExtensions
{
    extension(ICommandActorContext<TradePlanForwardLossLimitCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ITradePlanForwardLossLimitCommandActorContext DomainContext =>
            IsArgumentNull.Set(context as ITradePlanForwardLossLimitCommandActorContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the EventProducer service retained by the typed context.</summary>
        public ITradeEventProducer EventProducer => context.DomainContext.EventProducer;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<TradePlanForwardLossLimitCommandActor> Logger => context.DomainContext.Logger;
    }
}
