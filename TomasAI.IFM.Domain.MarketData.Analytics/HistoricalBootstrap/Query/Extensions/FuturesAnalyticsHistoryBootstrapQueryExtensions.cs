using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Query.Extensions;

/// <summary>Exposes bootstrap Query services as readonly extension properties.</summary>
public static class FuturesAnalyticsHistoryBootstrapQueryExtensions
{
    extension(IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesAnalyticsHistoryBootstrapQueryContext BootstrapContext =>
            context as IFuturesAnalyticsHistoryBootstrapQueryContext
            ?? throw new InvalidOperationException("The bootstrap Query actor requires its typed context.");
        /// <summary>Gets the operational bootstrap store.</summary>
        public IHistoricalBootstrapStore BootstrapStore => context.BootstrapContext.BootstrapStore;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesAnalyticsHistoryBootstrapQueryActor> Logger => context.BootstrapContext.Logger;
    }
}
