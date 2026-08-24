using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Trade.Option.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Option.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Option.Query.Extensions;

/// <summary>Exposes readonly OptionTradeQuery Query context properties.</summary>
public static class OptionTradeQueryContextExtensions
{
    extension(IQueryActorContext<OptionTradeQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IOptionTradeQueryContext DomainContext =>
            IsArgumentNull.Set(context as IOptionTradeQueryContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the BlackboardService service retained by the typed context.</summary>
        public IBlackboardService BlackboardService => context.DomainContext.BlackboardService;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<OptionTradeQueryActor> Logger => context.DomainContext.Logger;
    }
}
