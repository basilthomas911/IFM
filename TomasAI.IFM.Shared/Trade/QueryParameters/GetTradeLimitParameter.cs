using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve trade limit for a specific trade.
/// </summary>
[MessagePackObject(false)]
public record GetTradeLimitParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int TradeId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradeLimitParameter() { }

    [SerializationConstructor]
    public GetTradeLimitParameter(int tradeId)
    {
        TradeId = tradeId;
        QueryParams = $"tradeId={TradeId}";
    }

    public string Format()
        => $"{TradeId}";
}
