using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Fund.ViewModels;

namespace TomasAI.IFM.Application.Storage.FundDb
{
    public interface IFundDbReadContext : IFundDbContext
    {
        Task<FundReadModel> GetFundAsync(int fundId);
        FundReadModel GetFund(int fundId);
        Task<IReadOnlyList<FundReadModel>> GetFundsAsync();
        Task<IReadOnlyList<FundOrderReadModel>> GetFundOrdersAsync();
        IReadOnlyList<FundOrderReadModel>GetFundOrders();
        Task<IReadOnlyList<FundOrderTradeReadModel>> GetFundOrderTradesAsync();
        IReadOnlyList<FundOrderTradeReadModel> GetFundOrderTrades();
        Task<IReadOnlyList<FundTransactionReadModel>> GetFundTransactionsAsync(int fundId, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FundPnlReadModel>> GetFundPnlAsync(int fundId, DateTime startDate, DateTime endDate);
        Task<decimal> GetFundBalanceAsync(int fundId);
        Task<decimal> GetFundStartingBalanceAsync(int fundId, DateTime startDate);
        Task<decimal> GetFundEndingBalanceAsync(int fundId, DateTime endDate);
        Task<decimal> GetOpeningFundBalanceAsync(int fundId, DateTime valueDate);
        Task<decimal> GetClosingFundBalanceAsync(int fundId, DateTime valueDate);
        Task<decimal> GetFundCommissionAsync(int fundId, DateTime startDate, DateTime endDate);
        Task<int> GetFundIdFromOrderIdAsync(int orderId);
        Task<IReadOnlyList<FundOrderAmountReadModel>> GetFundLossOrdersAsync(int fundId, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FundOrderAmountReadModel>> GetFundProfitOrdersAsync(int fundId, DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<FundDailyBalanceReadModel>> GetFundDailyBalancesAsync(int fundId, DateTime startDate, DateTime endDate);
        Task<FundDrawdownBalancesReadModel> GetFundDrawdownBalancesAsync(int fundId, DateTime startDate, DateTime endDate);
    }
}
