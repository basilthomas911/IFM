using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.FundDb;

public interface IFundDbWriteContext 
{
    Task DeleteFundAsync(int fundId);
    Task DeleteFundOrderAsync(int fundId, int orderId);
    Task DeleteFundOrderTradeAsync(int fundId, int orderId, int tradeId);
    Task DeleteFundTransactionAsync(int fundId, DateOnly valueDate, int orderId, int tradeId, TradeType tradeType, FundTransactionType transactionType, DateTime transactionDate);

    Task InsertFundAsync(FundReadModel fund);
    Task InsertFundsAsync(ICollection<FundReadModel> funds);
    Task<long> InsertFundsAsync(IEnumerable<FundReadModel> funds);
    Task InsertFundOrderAsync(FundOrderReadModel fundOrder);
    Task InsertFundOrdersAsync(ICollection<FundOrderReadModel> fundOrders);
    Task<long> InsertFundOrdersAsync(IEnumerable<FundOrderReadModel> fundOrders);
    Task InsertFundOrderTradeAsync(FundOrderTradeReadModel fundOrderTrade);
    Task InsertFundOrderTradesAsync(ICollection<FundOrderTradeReadModel> fundOrderTrades);
    Task<long> InsertFundOrderTradesAsync(IEnumerable<FundOrderTradeReadModel> fundOrderTrades);
    Task InsertFundTransactionAsync(FundTransactionReadModel fundTransaction);
    Task InsertFundTransactionsAsync(ICollection<FundTransactionReadModel> fundTransactions);
    Task<long> InsertFundTransactionsAsync(IEnumerable<FundTransactionReadModel> fundTransactions);
    Task UpdateFundOrderStatusAsync(int fundId, int orderId, Domain.Fund.Shared.OrderStatus orderStatus);
    Task UpdateFundOrderTradeStateAsync(int fundId, int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy);
    Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);
}
