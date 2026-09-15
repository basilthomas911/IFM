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

    /// <summary>Gets the deterministic identity of the last event applied to this state.</summary>
    public FuturesTradeSessionBarId LastAppliedBarId { get; private set; }

    /// <summary>Gets the last event's bar; its market interval need not be the newest.</summary>
    public FuturesTradeSessionBarReadModel? LastAppliedBar { get; private set; }

    /// <inheritdoc />
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not FuturesTradeSessionBarPublishedEvent published) return false;
        if (published.Bar is null) return false;
        LastAppliedBarId = published.Bar.ObservationId;
        LastAppliedBar = published.Bar;
        return true;
    }
}
