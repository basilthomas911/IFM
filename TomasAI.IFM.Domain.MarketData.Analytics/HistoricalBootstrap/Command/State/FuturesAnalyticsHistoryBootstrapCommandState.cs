using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.State;

/// <summary>Reconstructs durable bootstrap-request state from its event stream.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapCommandState
    : BaseEventSourceActorState<FuturesAnalyticsHistoryBootstrapCommandState>,
      IEventSourceActorState<FuturesAnalyticsHistoryBootstrapCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>Gets whether the request was durably accepted.</summary>
    public bool IsRequested { get; private set; }

    /// <summary>Gets the immutable accepted parameters.</summary>
    public FuturesAnalyticsHistoryBootstrapParameters? Parameters { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesAnalyticsHistoryBootstrapRequestedEvent requested) return false;
        IsRequested = true;
        Parameters = requested.Parameters;
        return true;
    }
}
