using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Event.Actor;

/// <summary>Defines readonly services required by the durable bootstrap Event actor.</summary>
public interface IFuturesAnalyticsHistoryBootstrapEventContext
    : IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor>
{
    /// <summary>Gets the provider-neutral bootstrap coordinator.</summary>
    HistoricalBootstrapCoordinator Coordinator { get; }
    /// <summary>Gets the durable operational bootstrap store.</summary>
    IHistoricalBootstrapStore BootstrapStore { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsHistoryBootstrapEventActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the durable bootstrap Event actor.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapEventContext
    : EventActorContext,
      IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor>,
      IFuturesAnalyticsHistoryBootstrapEventContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsHistoryBootstrapEventContext(
        IActorSupervisor supervisor,
        HistoricalBootstrapCoordinator coordinator,
        IHistoricalBootstrapStore bootstrapStore,
        ILogger<FuturesAnalyticsHistoryBootstrapEventActor> logger)
        : base(supervisor, new(ActorType.Event, FuturesAnalyticsHistoryBootstrapEventActor.ActorName))
    {
        Coordinator = IsArgumentNull.Set(coordinator);
        BootstrapStore = IsArgumentNull.Set(bootstrapStore);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public HistoricalBootstrapCoordinator Coordinator { get; }
    /// <inheritdoc />
    public IHistoricalBootstrapStore BootstrapStore { get; }
    /// <inheritdoc />
    public ILogger<FuturesAnalyticsHistoryBootstrapEventActor> Logger { get; }
}
