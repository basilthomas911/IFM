using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;

/// <summary>Queries one persisted global cache policy version from ScyllaDB.</summary>
[MessagePackObject]
public sealed record GetStrategyOptionChainParametersQuery : IQuery<StrategyOptionChainParameterSet>
{
    public const string Actor = "MarketDataQuery";
    public const string Verb = "GetStrategyOptionChainParameters";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public Guid ParameterSetId { get; init; }
    [Key(3)] public int Version { get; init; }
    [IgnoreMember] public int ErrorCode => 20598;
    [IgnoreMember] public string? QueryParams => null;
}
