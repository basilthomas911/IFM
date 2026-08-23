using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query.Extensions;

/// <summary>Exposes yield-curve query services as readonly extension properties.</summary>
public static class YieldCurveRateQueryExtensions
{
    extension(IQueryActorContext<YieldCurveRateQueryActor> context)
    {
        /// <summary>Gets the domain context.</summary>
        public IYieldCurveRateQueryContext YieldCurveRateContext => IsArgumentNull.Set(context as IYieldCurveRateQueryContext)!;
        /// <summary>Gets the database factory.</summary>
        public IDbContextFactory DbFactory => context.YieldCurveRateContext.DbFactory;
        /// <summary>Gets the actor logger.</summary>
        public ILogger<YieldCurveRateQueryActor> Logger => context.YieldCurveRateContext.Logger;
    }
}
