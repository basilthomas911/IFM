using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.State;

/// <summary>Reconstructs authoritative completed-bar publication state from its event stream.</summary>
public sealed class FuturesTradeSessionBarSignalCommandState
    : BaseEventSourceActorState<FuturesTradeSessionBarSignalCommandState>,
      IEventSourceActorState<FuturesTradeSessionBarSignalCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>Gets the most recently published deterministic bar identity.</summary>
    public FuturesTradeSessionBarId LastPublishedBarId { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesTradeSessionBarPublishedEvent published) return false;
        LastPublishedBarId = published.Bar.ObservationId;
        return true;
    }
}
