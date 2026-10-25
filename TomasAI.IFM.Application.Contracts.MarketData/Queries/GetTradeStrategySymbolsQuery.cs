using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed class GetTradeStrategySymbolsQuery : IQuery<TradeStrategySymbolReadModel[]>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetTradeStrategySymbolsQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="family">The Family field.</param>
    [SerializationConstructor]
    public GetTradeStrategySymbolsQuery(ActorSubject subject, IActorEntityId entityId, TradeStrategyFamilyType family)
    {
        Subject = subject;
        EntityId = entityId;
        Family = family;
    }
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetTradeStrategySymbols";
    [IgnoreMember] public const int ErrorId = 1062;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public TradeStrategyFamilyType Family { get; set; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string? QueryParams => $"family={Family}";
}
