using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

/// <summary>
/// Defines methods for creating and managing funds, fund orders, trades, and transactions within the fund management
/// system.
/// </summary>
/// <remarks>This interface provides asynchronous operations for adding, modifying, and removing fund-related
/// entities, as well as processing transactions and managing trade states. Implementations are expected to handle
/// command tracking via command IDs and support correlation for distributed or batch operations. All methods return a
/// ServiceResult containing the command identifier for tracking purposes.</remarks>
public interface IFundCommandApi
{
    Task<ServiceResult<Guid>> CreateFundAsync(FundReadModel newFund);
    Task<ServiceResult<Guid>> AddOrderToFundAsync(FundOrderReadModel fundOrder);
    Task<ServiceResult<Guid>> AddTradeToFundOrderAsync(FundOrderTradeReadModel fundOrderTrade);
    Task<ServiceResult<Guid>> RemoveTradeFromFundOrderAsync(FundOrderTradeId fundOrderTradeId);
    Task<ServiceResult<Guid>> RemoveOrderFromFundAsync(FundOrderId fundOrderId);
    Task<ServiceResult<Guid>> CloseFundOrderAsync(FundOrderId fundOrderId);
    Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState, Guid correlationId);
    Task<ServiceResult<Guid>> ChangeFundOrderTradeStateAsync(FundOrderTradeId fundOrderTradeId, TradeState tradeState);
    Task<ServiceResult<Guid>> GenerateFundMaxProfitAsync(FundOrderReadModel fundOrder, TradeTimePeriodType timePeriod);

    Task<ServiceResult<Guid>> CreateFundTransactionAsync(FundTransactionReadModel fundTransaction);
    Task<ServiceResult<Guid>> CreateFundTransactionsAsync(FundTransactionEntityId transactionsId, FundTransactionReadModel[] fundTransaction, Guid correlationId);
    Task<ServiceResult<Guid>> ProcessEndOfDayFundTransactionAsync(Guid correlationId, FundTransactionReadModel fundTransaction);
}
