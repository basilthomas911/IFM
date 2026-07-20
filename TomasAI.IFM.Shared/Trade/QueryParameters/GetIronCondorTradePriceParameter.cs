using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the iron condor trade price for a specific trade and value date.
/// </summary>
[MessagePackObject(false)]
public record GetIronCondorTradePriceParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int TradeId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetIronCondorTradePriceParameter() { }

    [SerializationConstructor]
    public GetIronCondorTradePriceParameter(int tradeId, DateOnly valueDate)
    {
        TradeId = tradeId;
        ValueDate = valueDate;
        QueryParams = $"tradeId={TradeId}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{TradeId}.{ValueDate:yyyy-MM-dd}";
}
