using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.Extensions;

/// <summary>Exposes history-bootstrap Command services as readonly extension properties.</summary>
public static class FuturesAnalyticsHistoryBootstrapCommandExtensions
{
    extension(ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor> context)
    {
        /// <summary>Gets the typed domain context.</summary>
        public IFuturesAnalyticsHistoryBootstrapCommandContext BootstrapContext =>
            context as IFuturesAnalyticsHistoryBootstrapCommandContext
            ?? throw new InvalidOperationException("The bootstrap Command actor requires its typed context.");
        /// <summary>Gets the state repository.</summary>
        public IEventSourceActorStateRepository<FuturesAnalyticsHistoryBootstrapCommandState> BootstrapRepository =>
            context.BootstrapContext.Repository;
        /// <summary>Gets the durable event projector.</summary>
        public IEventProjector<FuturesAnalyticsHistoryBootstrapCommandActor> BootstrapProjector =>
            context.BootstrapContext.EventProjector;
        /// <summary>Gets the typed logger.</summary>
        public ILogger<FuturesAnalyticsHistoryBootstrapCommandActor> Logger => context.BootstrapContext.Logger;
    }
}
