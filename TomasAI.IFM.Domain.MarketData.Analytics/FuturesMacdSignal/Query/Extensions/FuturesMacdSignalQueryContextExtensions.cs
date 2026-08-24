using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query.Extensions;

/// <summary>Exposes readonly FuturesMacdSignalQuery Query context properties.</summary>
public static class FuturesMacdSignalQueryContextExtensions
{
    extension(IQueryActorContext<FuturesMacdSignalQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IFuturesMacdSignalQueryContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesMacdSignalQueryContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbFactory service retained by the typed context.</summary>
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<FuturesMacdSignalQueryActor> Logger => context.DomainContext.Logger;
    }
}
