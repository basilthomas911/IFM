using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve trade quantity for a specific trade.
/// </summary>
[MessagePackObject(false)]
public record GetTradeQuantityParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int TradeId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetTradeQuantityParameter() { }

    [SerializationConstructor]
    public GetTradeQuantityParameter(int tradeId)
    {
        TradeId = tradeId;
        QueryParams = $"tradeId={TradeId}";
    }

    public string Format()
        => $"{TradeId}";
}
