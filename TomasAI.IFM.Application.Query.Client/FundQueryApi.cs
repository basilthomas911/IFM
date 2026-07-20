using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Fund.Queries;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client
{
    public class FundQueryApi : IFundQueryApi
    {
        readonly IQueryService _queryService;
        readonly string _controller;

        public FundQueryApi(IQueryService queryService)
        {
            _queryService = IsArgumentNull.Set(queryService);
            _controller = "Fund";
         }

        /// <summary>
        /// return all funds
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync() 
            => await _queryService.ExecuteApiQueryAsync(new GetFundsQuery(), _controller);

        /// <summary>
        /// return fund orders 
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync()
            => await _queryService.ExecuteApiQueryAsync(new GetFundOrdersQuery {}, _controller);

        /// <summary>
        /// return fund order trades 
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync()
            => await _queryService.ExecuteApiQueryAsync(new GetFundOrderTradesQuery {}, _controller);

        /// <summary>
        /// return fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
             => await _queryService.ExecuteApiQueryAsync(new GetFundBalanceQuery { FundId = fundId }, _controller);

        /// <summary>
        /// return opening fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(int fundId, DateTime valueDate)
             => await _queryService.ExecuteApiQueryAsync( new GetOpeningFundBalanceQuery { FundId = fundId , ValueDate = valueDate}, _controller);

        /// <summary>
        /// return closing fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(int fundId, DateTime valueDate)
             => await _queryService.ExecuteApiQueryAsync( new GetClosingFundBalanceQuery { FundId = fundId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return fund transactions for selected fund by date range
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(int fundId, DateTime startDate, DateTime endDate)
             => await _queryService.ExecuteApiQueryAsync( new GetFundTransactionsQuery { FundId = fundId, StartDate = startDate, EndDate = endDate }, _controller);

        /// <summary>
        /// return fund pnl report for selected fund id by date range
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(int fundId, DateTime startDate, DateTime endDate)
            => await _queryService.ExecuteApiQueryAsync( new GetFundPnlReportQuery { FundId = fundId, StartDate = startDate, EndDate = endDate }, _controller);

        /// <summary>
        /// return fund id from order id
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<ScalarReadModel<int>>> GetFundIdFromOrderIdAsync(int orderId)
            => await _queryService.ExecuteApiQueryAsync( new GetFundIdFromOrderIdQuery { OrderId = orderId }, _controller);

        /// <summary>
        /// return fund win loss ratio for selected fund id by date range
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundWinLossRatioReadModel>> GetFundWinLossRatioAsync(int fundId, DateTime startDate, DateTime endDate)
            => await _queryService.ExecuteApiQueryAsync(new GetFundWinLossRatioQuery { FundId = fundId, StartDate = startDate, EndDate = endDate }, _controller);

        /// <summary>
        /// return fund drawdown balances by date range
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundDrawdownBalancesReadModel>> GetFundDrawdownBalancesAsync(int fundId, DateTime startDate, DateTime endDate)
            => await _queryService.ExecuteApiQueryAsync(new GetFundDrawdownBalancesQuery { FundId = fundId, StartDate = startDate, EndDate = endDate }, _controller);

    }
}
