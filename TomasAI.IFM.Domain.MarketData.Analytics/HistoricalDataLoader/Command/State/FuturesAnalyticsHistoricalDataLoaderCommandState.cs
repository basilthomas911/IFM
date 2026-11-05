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

    /// <summary>Gets the committed terminal outcome, when available.</summary>
    public bool IsCompleted { get; private set; }

    /// <summary>Gets whether the latest committed outcome is a failure.</summary>
    public bool IsFailed { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        return domainEvent switch
        {
            FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested => On(requested),
            FuturesAnalyticsHistoricalDataLoaderCompletedEvent when IsRequested => Complete(),
            FuturesAnalyticsHistoricalDataLoaderFailedEvent when IsRequested => Fail(),
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
    bool Complete()
    {
        IsCompleted = true;
        IsFailed = false;
        return true;
    }

    bool Fail()
    {
        if (IsCompleted) return false;
        IsFailed = true;
        return true;
    }
}
