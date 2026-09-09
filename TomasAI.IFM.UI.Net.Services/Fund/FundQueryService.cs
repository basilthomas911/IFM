using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;

namespace TomasAI.IFM.UI.Net.Services.Fund
{
    /// <summary>Provides the FundQueryService UI service boundary.</summary>
    public class FundQueryService : UiServiceBase<FundQueryService>
    {
        readonly IFundQueryApi _queryApi;

        /// <summary>
        /// create fund controller
        /// </summary>
        /// <param name="queryApi"></param>
        public FundQueryService(IFundQueryApi queryApi)
        {
            _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
        }

        /// <summary>
        /// get funds
        /// </summary>
        /// <param name="onCompleted"></param>
        public async Task GetFundsAsync(Action<FundReadModel[]> onCompleted)
            => await ExecuteAsync(_queryApi.GetFundsAsync, onCompleted);

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public Task GetFundsAsync(Func<FundReadModel[], Task> onCompleted)
            => ExecuteAsync(_queryApi.GetFundsAsync, onCompleted);

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

        /// <summary>Reads original legacy records without translating amounts into financial authority.</summary>
        public async Task<FundTransactionReadModel[]> GetLegacyTransactionsAsync(int fundId, DateOnly startDate, DateOnly endDate, CancellationToken token)
        {
            if(fundId<0 || startDate==default || endDate<startDate || endDate.DayNumber-startDate.DayNumber>366)
                throw new ArgumentException("Select a legacy Fund and a date range of at most one year.");
            var result=await _queryApi.GetFundTransactionsAsync(fundId,startDate,endDate).WaitAsync(token).ConfigureAwait(false);
            if(result is null || !result.Success || result.Value is null)
                throw new InvalidOperationException(result?.ErrorMessage??"Legacy history is unavailable.");
            if(result.Value.Any(x=>x.FundId!=fundId || x.ValueDate<startDate || x.ValueDate>endDate))
                throw new InvalidOperationException("Legacy history returned records outside the selected scope.");
            return result.Value;
        }

        /// <summary>
        /// get selcted fund balance
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFundBalanceAsync(int fundId, Action<decimal> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFundBalanceAsync(fundId), fb => onCompleted(fb.Value));

        /// <summary>Executes or exposes a documented UI service operation.</summary>
        public Task GetFundBalanceAsync(int fundId, Func<decimal, Task> onCompleted)
            => ExecuteAsync(() => _queryApi.GetFundBalanceAsync(fundId), fb => onCompleted(fb.Value));

        /// <summary>
        /// get fund pnl report
        /// </summary>
        /// <param name="fundId"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="onCompleted"></param>
        public async Task GetFundPnlReportAsync(int fundId, DateOnly startDate, DateOnly endDate, Action<FundPnlReportReadModel> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFundPnlReportAsync(fundId, startDate, endDate), onCompleted);

        /// <summary>Loads an inclusive-date report without shared callback state; cancellation stops local observation.</summary>
        public async Task<FundPnlReportReadModel?> GetFundPnlReportAsync(
            int fundId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken)
        {
            var result = await _queryApi.GetFundPnlReportAsync(fundId, startDate, endDate)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException($"Fund report ({result.ErrorCode}): {result.ErrorMessage}");
            return result.Value;
        }

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
