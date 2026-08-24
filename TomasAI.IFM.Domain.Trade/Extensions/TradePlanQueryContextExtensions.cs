using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Plan;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Plan;

/// <summary>Exposes readonly TradePlanQuery Query context properties.</summary>
public static class TradePlanQueryContextExtensions
{
    extension(IQueryActorContext<TradePlanQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ITradePlanQueryContext DomainContext =>
            IsArgumentNull.Set(context as ITradePlanQueryContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<TradePlanQueryActor> Logger => context.DomainContext.Logger;
    }
}
