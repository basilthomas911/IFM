using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

/// <summary>Computes a read-only strategy preview from the exact displayed market snapshot.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record GetTradeOrderPreviewQuery : IQuery<TradeOrderPreview>
{
    /// <summary>Creates an empty query for actor client initialization.</summary>
    public GetTradeOrderPreviewQuery() { }

    /// <summary>Rehydrates the permanent numeric-key wire contract.</summary>
    [SerializationConstructor]
    public GetTradeOrderPreviewQuery(ActorSubject subject, IActorEntityId entityId, TradeOrderPreviewRequest tradeOrderPreviewRequest)
    {
        Subject = subject;
        EntityId = entityId;
        TradeOrderPreviewRequest = tradeOrderPreviewRequest;
    }

    public const string Actor = TradeOrderActorNames.Query;
    public const string Verb = "GetTradeOrderPreview";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeOrderPreviewRequest TradeOrderPreviewRequest { get; init; } = null!;
    [IgnoreMember] public int ErrorCode => 25212;
    [IgnoreMember] public string? QueryParams => null;
}
