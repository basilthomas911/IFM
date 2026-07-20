using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.State;

/// <summary>
/// Represents the state for a futures option tick data query actor.
/// </summary>
public class FuturesOptionTickDataQueryState()
    : BaseQueryActorState<FuturesOptionTickDataQueryState>
{
    public override ActorThreadId Id { get; set; } = default!;

    public FuturesOptionTickDataQueryState As => this;

    public ActorMessageInfo MsgInfo { get; set; } = default!;
}
