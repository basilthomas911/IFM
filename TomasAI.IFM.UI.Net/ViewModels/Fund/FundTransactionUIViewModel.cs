using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.Fund;

public record FundTransactionUIViewModel
{
    public FundTransactionUIViewModel(FundTransactionReadModel e)
    {
        TransactionId = $"{e.TransactionId}";
        TransactionDate = $"{e.TransactionDate:g}";
        TransactionType = $"{e.TransactionType}";
        FundId = $"{e.FundId}";
        OrderId = $"{e.OrderId}";
        TradeId = $"{e.TradeId}";
        TradeType = $"{e.TradeType}";
        ValueDate = $"{e.ValueDate:d}";
        TradeStatus = $"{e.TradeStatus}";
        Description = e.Description;
        Amount = $"{e.Amount:C}";
        Balance = $"{e.Balance:C}";
    }

    public string TransactionId { get; init; }
    public string TransactionDate { get; init; }
    public string TransactionType { get; init; }
    public string FundId { get; init; }
    public string OrderId { get; init; }
    public string TradeId { get; init; }
    public string TradeType { get; init; }
    public string ValueDate { get; init; }
    public string TradeStatus { get; init; }
    public string Description { get; init; }
    public string Amount { get; init; }
    public string Balance { get; init; }
}
