using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public record FundMaxProfitGeneratedReadModel
{
    [Key(0)]
    public int FundId { get; init; }
    [Key(1)]
    public DateOnly TradeDate { get; init; }
    [Key(2)]
    public decimal FundBalance { get; init; }
    [Key(3)]
    public ICollection<FundOrderAmountReadModel> FundProfitOrders { get; init; }
    [Key(4)]
    public ICollection<FundOrderAmountReadModel> FundLossOrders { get; init; }
    [Key(5)]
    public FundDrawdownBalancesReadModel FundDrawdownBalances { get; init; }

    public FundMaxProfitGeneratedReadModel() { }
    [SerializationConstructor]
    public FundMaxProfitGeneratedReadModel(int fundId, DateOnly tradeDate, decimal fundBalance, ICollection<FundOrderAmountReadModel> fundProfitOrders, ICollection<FundOrderAmountReadModel> fundLossOrders, FundDrawdownBalancesReadModel fundDrawdownBalances)
    {
        FundId = fundId;
        TradeDate = tradeDate;
        FundBalance = fundBalance;
        FundProfitOrders = fundProfitOrders;
        FundLossOrders = fundLossOrders;
        FundDrawdownBalances = fundDrawdownBalances;
    }
}
