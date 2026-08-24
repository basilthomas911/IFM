using TomasAI.IFM.Domain.Trade.Shared;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Query.Extensions;

/// <summary>Exposes readonly SpreadDistributionQuery Query context properties.</summary>
public static class SpreadDistributionQueryContextExtensions
{
    extension(IQueryActorContext<SpreadDistributionQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public ISpreadDistributionQueryContext DomainContext =>
            IsArgumentNull.Set(context as ISpreadDistributionQueryContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<SpreadDistributionQueryActor> Logger => context.DomainContext.Logger;
    }
}
