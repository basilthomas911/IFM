using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

public abstract record StrategyPositionQueryBase<TResult> : IQuery<TResult>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1000", Justification = "The factory preserves the existing typed generic result model API.")]
    public static string Actor => PositionActorNames.Query;
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public StrategyPositionId PositionId { get; init; }
    [IgnoreMember] public int ErrorCode => 25211;
    [IgnoreMember] public string? QueryParams => null;
}
