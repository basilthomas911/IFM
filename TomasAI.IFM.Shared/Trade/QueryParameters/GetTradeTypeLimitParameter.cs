using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve trade type limit for a specific trade and trade type.
/// </summary>
[MessagePackObject(false)]
public record GetTradeTypeLimitParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int TradeId { get; init; }
    [Key(1)] public TradeType TradeType { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradeTypeLimitParameter() { }

    [SerializationConstructor]
    public GetTradeTypeLimitParameter(int tradeId, TradeType tradeType)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        QueryParams = $"tradeId={TradeId}&tradeType={TradeType}";
    }

    public string Format()
        => $"{TradeId}.{TradeType}";
}
