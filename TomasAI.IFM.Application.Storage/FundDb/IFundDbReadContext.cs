using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Application.Storage.FundDb;

public interface IFundDbReadContext 
{
    Task<FundReadModel?> GetFundAsync(int fundId);
    Task<ICollection<FundReadModel>> GetFundsAsync();
    Task<ICollection<FundReadModel>> GetFundsAsync(CancellationToken cancellationToken);
    Task<FundOrderReadModel?> GetFundOrderAsync(int fundId, int orderId);
    Task<ICollection<FundOrderReadModel>> GetFundOrdersAsync();
    Task<ICollection<FundOrderReadModel>> GetFundOrdersAsync(CancellationToken cancellationToken);
    ICollection<FundOrderReadModel>GetFundOrders();
    Task<FundOrderTradeReadModel?> GetFundOrderTradeAsync(int fundId, int orderId, int tradeId);
    Task<ICollection<FundOrderTradeReadModel>> GetFundOrderTradesAsync();
    Task<ICollection<FundOrderTradeReadModel>> GetFundOrderTradesAsync(CancellationToken cancellationToken);
    ICollection<FundOrderTradeReadModel> GetFundOrderTrades();
    Task<FundTransactionReadModel?> GetFundTransactionAsync(int fundId, DateOnly valueDate, int orderId, int tradeId, TradeType tradeType, FundTransactionType transactionType, DateTime transactionDate);
    Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<ICollection<FundTransactionReadModel>> GetFundTransactionsAsync();
    Task<ICollection<FundPnlReadModel>> GetFundPnlAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<decimal> GetFundBalanceAsync(int fundId);
    Task<decimal> GetFundBalanceAsync(int fundId, CancellationToken cancellationToken);
    Task<decimal> GetFundStartingBalanceAsync(int fundId, DateOnly startDate);
    Task<decimal> GetFundStartingBalanceAsync(int fundId, DateOnly startDate, CancellationToken cancellationToken);
    Task<decimal> GetFundEndingBalanceAsync(int fundId, DateOnly endDate);
    Task<decimal> GetFundEndingBalanceAsync(int fundId, DateOnly endDate, CancellationToken cancellationToken);
    Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate);
    Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<decimal> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate);
    Task<decimal> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<decimal> GetFundTradeCommissionAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<decimal> GetFundTradeCommissionAsync(int fundId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<int> GetFundIdFromOrderIdAsync(int orderId);
    Task<int> GetFundIdFromOrderIdAsync(int orderId, CancellationToken cancellationToken);
    Task<ICollection<FundOrderAmountReadModel>> GetFundLossOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FundOrderAmountReadModel>> GetFundLossOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<ICollection<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(int fundId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<ICollection<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate);
}
