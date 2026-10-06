using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
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
    public FuturesAnalyticsHistoricalDataLoaderParameters? FuturesAnalyticsHistoricalDataLoaderParameters { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        return domainEvent switch
        {
            FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested => On(requested),
            _ => false
        };
    }

    /// <summary>Records the accepted historical load request only from its domain event.</summary>
    bool On(FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested)
    {
        IsRequested = true;
        FuturesAnalyticsHistoricalDataLoaderParameters = requested.FuturesAnalyticsHistoricalDataLoaderParameters;
        return true;
    }
}
