using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Fund.Queries;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Query.SignalRClient;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    public class FundQueryApi : IFundQueryApi
    {
        private IQueryService _queryService;

        public FundQueryApi(IQueryService queryService)
        {
            _queryService = queryService;
        }

        /// <summary>
        /// return all funds
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<FundReadModel[]>> GetFundsAsync()
             => await _queryService.ExecuteQueryAsync<FundReadModel[]>(new GetFundsQuery());

        /// <summary>
        /// return fund orders for selceted fund
        /// </summary>
        /// <param name="fundId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundOrderReadModel[]>> GetFundOrdersAsync(int fundId)
            => await _queryService.ExecuteQueryAsync<FundOrderReadModel[]>(new GetFundOrdersQuery { FundId = fundId});

        /// <summary>
        /// return trades for selected fund order
        /// </summary>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundOrderTradeReadModel[]>> GetFundOrderTradesAsync(int orderId)
            => await _queryService.ExecuteQueryAsync<FundOrderTradeReadModel[]>(new GetFundOrderTradesQuery { OrderId = orderId });

        /// <summary>
        /// return fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundBalanceReadModel>> GetFundBalanceAsync(int fundId)
             => await _queryService.ExecuteQueryAsync<FundBalanceReadModel>(new GetFundBalanceQuery { FundId = fundId });

        /// <summary>
        /// return opening fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundBalanceReadModel>> GetOpeningFundBalanceAsync(int fundId, DateTime valueDate)
             => await _queryService.ExecuteQueryAsync<FundBalanceReadModel>(new GetOpeningFundBalanceQuery { FundId = fundId , ValueDate = valueDate});

        /// <summary>
        /// return closing fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundBalanceReadModel>> GetClosingFundBalanceAsync(int fundId, DateTime valueDate)
             => await _queryService.ExecuteQueryAsync<FundBalanceReadModel>(new GetClosingFundBalanceQuery { FundId = fundId, ValueDate = valueDate });

        /// <summary>
        /// return fund transactions for selected fund by date range
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundTransactionReadModel[]>> GetFundTransactionsAsync(int fundId, DateTime startDate, DateTime endDate)
             => await _queryService.ExecuteQueryAsync<FundTransactionReadModel[]>(
                 new GetFundTransactionsQuery { FundId = fundId, StartDate = startDate, EndDate = endDate });

        /// <summary>
        /// return fund pnl report for selected fund id by date range
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FundPnlReportReadModel>> GetFundPnlReportAsync(int fundId, DateTime startDate, DateTime endDate)
            => await _queryService.ExecuteQueryAsync<FundPnlReportReadModel>(
                 new GetFundPnlReportQuery { FundId = fundId, StartDate = startDate, EndDate = endDate });
    }
}
