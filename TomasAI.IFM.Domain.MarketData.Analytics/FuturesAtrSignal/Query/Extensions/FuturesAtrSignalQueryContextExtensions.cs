using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query.Extensions;

/// <summary>Exposes readonly FuturesAtrSignalQuery Query context properties.</summary>
public static class FuturesAtrSignalQueryContextExtensions
{
    extension(IQueryActorContext<FuturesAtrSignalQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesAtrSignalQueryContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesAtrSignalQueryContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesAtrSignalQueryActor> Logger => context.DomainContext.Logger;
    }
}
