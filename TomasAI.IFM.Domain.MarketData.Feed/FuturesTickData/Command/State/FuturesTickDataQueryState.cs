using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State;

/// <summary>
/// Represents the state for a futures tick data query actor.
/// </summary>
public class FuturesTickDataQueryState()
    : BaseQueryActorState<FuturesTickDataQueryState>
{
    public override ActorThreadId Id { get; set; } = default!;

    public FuturesTickDataQueryState As => this;

    public ActorMessageInfo MsgInfo { get; set; } = default!;
}
