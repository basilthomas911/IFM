using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve option trade spread bar data within a date range.
/// </summary>
[MessagePackObject(false)]
public record GetOptionTradeSpreadBarDataParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int OrderId { get; init; }
    [Key(1)] public int TradeId { get; init; }
    [Key(2)] public TradeType TradeType { get; init; }
    [Key(3)] public DateOnly ValueDate { get; init; }
    [Key(4)] public DateTime StartDate { get; init; }
    [Key(5)] public DateTime EndDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetOptionTradeSpreadBarDataParameter() { }

    [SerializationConstructor]
    public GetOptionTradeSpreadBarDataParameter(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        StartDate = startDate;
        EndDate = endDate;
        QueryParams = $"orderId={OrderId}&tradeId={TradeId}&tradeType={TradeType}&valueDate={ValueDate:yyyy-MM-dd}&startDate={StartDate:o}&endDate={EndDate:o}";
    }

    public string Format()
        => $"{OrderId}.{TradeId}.{TradeType}.{ValueDate:yyyy-MM-dd}.{StartDate:o}.{EndDate:o}";
}
