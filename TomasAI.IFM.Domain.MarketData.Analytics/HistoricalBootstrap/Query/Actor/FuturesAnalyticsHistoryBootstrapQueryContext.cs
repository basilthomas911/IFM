using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Query.Actor;

/// <summary>Defines readonly services required by the bootstrap diagnostics Query actor.</summary>
public interface IFuturesAnalyticsHistoryBootstrapQueryContext
    : IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor>
{
    /// <summary>Gets the durable bootstrap operational store.</summary>
    IHistoricalBootstrapStore BootstrapStore { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsHistoryBootstrapQueryActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the bootstrap diagnostics Query actor.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapQueryContext
    : QueryActorContext,
      IQueryActorContext<FuturesAnalyticsHistoryBootstrapQueryActor>,
      IFuturesAnalyticsHistoryBootstrapQueryContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsHistoryBootstrapQueryContext(
        IActorSupervisor supervisor,
        IHistoricalBootstrapStore bootstrapStore,
        ILogger<FuturesAnalyticsHistoryBootstrapQueryActor> logger)
        : base(supervisor, new(ActorType.Query, FuturesAnalyticsHistoryBootstrapQueryActor.ActorName))
    {
        BootstrapStore = IsArgumentNull.Set(bootstrapStore);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IHistoricalBootstrapStore BootstrapStore { get; }
    /// <inheritdoc />
    public ILogger<FuturesAnalyticsHistoryBootstrapQueryActor> Logger { get; }
}
