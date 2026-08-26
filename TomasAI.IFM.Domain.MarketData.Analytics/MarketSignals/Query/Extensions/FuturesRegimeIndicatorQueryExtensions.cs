using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Query.Extensions;

/// <summary>Exposes readonly services retained by the regime-indicator query context.</summary>
public static class FuturesRegimeIndicatorQueryExtensions
{
    extension(IQueryActorContext<FuturesRegimeIndicatorQueryActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesRegimeIndicatorQueryContext DomainContext =>
            IsArgumentNull.Set(context as IFuturesRegimeIndicatorQueryContext, nameof(context))!;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesRegimeIndicatorQueryActor> Logger => context.DomainContext.Logger;
    }
}
