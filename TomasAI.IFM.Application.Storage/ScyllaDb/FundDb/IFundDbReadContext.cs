using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;

public interface IFundDbReadContext 
{
    Task<FundReadModel?> GetFundAsync(int fundId);
    Task<ICollection<FundReadModel>> GetFundsAsync();
    Task<FundOrderReadModel?> GetFundOrderAsync(int fundId, int orderId);
    Task<ICollection<FundOrderReadModel>> GetFundOrdersAsync();
    ICollection<FundOrderReadModel>GetFundOrders();
    Task<FundOrderTradeReadModel?> GetFundOrderTradeAsync(int fundId, int orderId, int tradeId);
    Task<ICollection<FundOrderTradeReadModel>> GetFundOrderTradesAsync();
    ICollection<FundOrderTradeReadModel> GetFundOrderTrades();
    Task<FundTransactionReadModel?> GetFundTransactionAsync(int fundId, DateOnly valueDate, int orderId, int tradeId, TradeType tradeType, FundTransactionType transactionType, DateTime transactionDate);
    Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync();
    Task<ICollection<FundPnlReadModel>> GetFundPnlAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<decimal> GetFundBalanceAsync(int fundId);
    Task<decimal> GetFundStartingBalanceAsync(int fundId, DateOnly startDate);
    Task<decimal> GetFundEndingBalanceAsync(int fundId, DateOnly endDate);
    Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate);
    Task<decimal> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate);
    Task<decimal> GetFundTradeCommissionAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<int> GetFundIdFromOrderIdAsync(int orderId);
    Task<ICollection<FundOrderAmountReadModel>> GetFundLossOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate);
}
