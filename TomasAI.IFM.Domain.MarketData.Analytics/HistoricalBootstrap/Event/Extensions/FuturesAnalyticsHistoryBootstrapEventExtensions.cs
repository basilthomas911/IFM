using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Event.Extensions;

/// <summary>Exposes bootstrap Event services as readonly extension properties.</summary>
public static class FuturesAnalyticsHistoryBootstrapEventExtensions
{
    extension(IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesAnalyticsHistoryBootstrapEventContext BootstrapContext =>
            context as IFuturesAnalyticsHistoryBootstrapEventContext
            ?? throw new InvalidOperationException("The bootstrap Event actor requires its typed context.");
        /// <summary>Gets the bootstrap coordinator.</summary>
        public HistoricalBootstrapCoordinator BootstrapCoordinator => context.BootstrapContext.Coordinator;
        /// <summary>Gets the bootstrap operational store.</summary>
        public IHistoricalBootstrapStore BootstrapStore => context.BootstrapContext.BootstrapStore;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesAnalyticsHistoryBootstrapEventActor> Logger => context.BootstrapContext.Logger;
    }
}
