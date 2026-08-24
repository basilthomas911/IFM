using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Queries.Handlers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Queries;

/// <summary>Exposes readonly TradeQuery Query context properties.</summary>
public static class TradeQueryContextExtensions
{
    extension(IQueryActorContext<TradeQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ITradeQueryContext DomainContext =>
            IsArgumentNull.Set(context as ITradeQueryContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the blackboard service retained by the typed context.</summary>
        public IBlackboardService BlackboardService => context.DomainContext.BlackboardService;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<TradeQueryActor> Logger => context.DomainContext.Logger;
    }
}
