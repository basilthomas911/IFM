using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

namespace TomasAI.IFM.Models
{
    public class FundQueryModel : BaseModel<FundQueryModel>
    {
        readonly IFundQueryApi _queryApi;

        /// <summary>
        /// create fund controller
        /// </summary>
        /// <param name="queryApi"></param>
        public FundQueryModel(IFundQueryApi queryApi)
        {
            _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
        }

        /// <summary>
        /// get funds
        /// </summary>
        /// <param name="onCompleted"></param>
        public async Task GetFundsAsync(Action<FundReadModel[]> onCompleted) 
            => await ExecuteAsync(_queryApi.GetFundsAsync, onCompleted);

        /// <summary>
        /// get fund orders 
        /// </summary>
        public async Task<FundOrderReadModel[]> GetFundOrdersAsync()
        { 
            var serviceResut = await _queryApi.GetFundOrdersAsync();
            if (serviceResut is not null && serviceResut.Success)
                return serviceResut.Value!;
            RaiseError(serviceResut!.ErrorCode, serviceResut.ErrorMessage);
            return [];
        }

        /// <summary>
        /// get fund order trades 
        /// </summary>
        public async Task<FundOrderTradeReadModel[]> GetFundOrderTradesAsync()
        {
            var serviceResut = await _queryApi.GetFundOrderTradesAsync();
            if (serviceResut is not null && serviceResut.Success)
                return serviceResut.Value!;
            RaiseError(serviceResut!.ErrorCode, serviceResut.ErrorMessage);
            return [];
        }

        /// <summary>
        /// get fund transactions
        /// </summary>
        /// <param name="onCompleted"></param>
        public async Task GetFundTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate, Action<FundTransactionReadModel[]> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFundTransactionsAsync(fundId, startDate, endDate), onCompleted);

        /// <summary>
        /// get selcted fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFundBalanceAsync(int fundId, Action<decimal> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFundBalanceAsync(fundId), fb => onCompleted(fb.Value));

        /// <summary>
        /// get fund pnl report
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFundPnlReportAsync(int fundId, DateOnly startDate, DateOnly endDate, Action<FundPnlReportReadModel> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFundPnlReportAsync(fundId, startDate, endDate), onCompleted);

        /// <summary>
        /// get fund win loss ratio
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFundWinLossRatioAsync(int fundId, DateOnly startDate, DateOnly endDate, Action<FundWinLossRatioReadModel> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFundWinLossRatioAsync(fundId, startDate, endDate), onCompleted);

    }
}
