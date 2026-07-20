using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the fund PnL report for a specific fund over a date range.
/// </summary>
[MessagePackObject(false)]
public record GetFundMaxProfitGeneratedParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int FundId { get; init; }
    [Key(1)] public DateOnly TradeDate { get; init; }
    
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFundMaxProfitGeneratedParameter() { }

    [SerializationConstructor]
    public GetFundMaxProfitGeneratedParameter(int fundId, DateOnly tradeDate)
    {
        FundId = fundId;
        TradeDate = tradeDate;
        QueryParams = $"fundId={FundId}&tradeDate={TradeDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{FundId}.{TradeDate:yyyy-MM-dd}";
}

