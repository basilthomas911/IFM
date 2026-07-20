using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the iron condor MDI limit for a specific order, trade and value date.
/// </summary>
[MessagePackObject(false)]
public record GetIronCondorMDILimitParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetIronCondorMDILimitParameter() { }

    [SerializationConstructor]
    public GetIronCondorMDILimitParameter(int orderId, int tradeId, DateOnly valueDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{OrderId}.{TradeId}.{ValueDate:yyyy-MM-dd}";
}
