using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.State;

/// <summary>Reconstructs durable data load-request state from its event stream.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderCommandState
    : BaseEventSourceActorState<FuturesAnalyticsHistoricalDataLoaderCommandState>,
      IEventSourceActorState<FuturesAnalyticsHistoricalDataLoaderCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>Gets whether the request was durably accepted.</summary>
    public bool IsRequested { get; private set; }

    /// <summary>Gets the immutable accepted parameters.</summary>
    public FuturesAnalyticsHistoricalDataLoaderParameters? Parameters { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested) return false;
        IsRequested = true;
        Parameters = requested.Parameters;
        return true;
    }
}
