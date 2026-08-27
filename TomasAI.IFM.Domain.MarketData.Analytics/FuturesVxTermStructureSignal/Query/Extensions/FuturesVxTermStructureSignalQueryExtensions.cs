using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Extensions;

/// <summary>Exposes readonly VX term-structure Query context properties.</summary>
public static class FuturesVxTermStructureSignalQueryExtensions
{
    extension(IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context)
    {
        public IFuturesVxTermStructureSignalQueryContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesVxTermStructureSignalQueryContext, nameof(context))!;
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        public ILogger<FuturesVxTermStructureSignalQueryActor> Logger => context.DomainContext.Logger;
    }
}
