using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

public interface IFundQueryApi
{
    Task<ServiceResult<FundReadModel[]>> GetFundsAsync();
    Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync();
    Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync();
    Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId);
    Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(int fundId, DateOnly valueDate);
    Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(int fundId, DateOnly valueDate);
    Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId);
    Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(int fundId, DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(int fundId, DateOnly startDate, DateOnly endDate);
}
