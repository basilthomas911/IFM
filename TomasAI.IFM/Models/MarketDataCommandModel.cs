using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;

namespace TomasAI.IFM.Models
{
    public class MarketDataCommandModel : BaseModel<MarketDataCommandModel>
    {
        readonly IMarketDataCommandApi _commandApi;

        /// <summary>
        /// create marketdata controller
        /// </summary>
        /// <param name="appRoot"></param>
        public MarketDataCommandModel(IMarketDataCommandApi commandApi)
        {
            _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
        }

        /// <summary>
        /// add futures contract
        /// </summary>
        /// <param name="futuresContract"></param>
        /// <param name="setCommandId"></param>
        public async Task AddFuturesContractAsync(FuturesContractViewModel futuresContract, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.AddFuturesContractAsync(futuresContract, setCommandId));

        /// <summary>
        /// change futures contract
        /// </summary>
        /// <param name="originalContractId"></param>
        /// <param name="changedFuturesContract"></param>
        /// <param name="setCommandId"></param>
        public async Task ChangeFuturesContractAsync(string originalContractId, FuturesContractViewModel changedFuturesContract, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.ChangeFuturesContractAsync(originalContractId, changedFuturesContract, setCommandId));

        /// <summary>
        /// remove futures contract
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="setCommandId"></param>
        public async Task RemoveFuturesContractAsync(string contractId, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.RemoveFuturesContractAsync(contractId, setCommandId));

        /// <summary>
        /// add futures option contract
        /// </summary>
        /// <param name="futuresOptionContract"></param>
        /// <param name="setCommandId"></param>
        public async Task AddFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.AddFuturesOptionContractAsync(futuresOptionContract, setCommandId));

        /// <summary>
        /// add futures option contracts
        /// </summary>
        /// <param name="futuresOptionContracts"></param>
        /// <param name="onCompleted"></param>
        public async Task AddFuturesOptionContractsAsync(FuturesOptionContractReadModel[] futuresOptionContracts, Action onCompleted)
            => await ExecuteCommandAsync(() => _commandApi.AddFuturesOptionContractsAsync(futuresOptionContracts), onCompleted);

        /// <summary>
        /// change futures option contract
        /// </summary>
        /// <param name="originalContractId"></param>
        /// <param name="changedFuturesOptionContract"></param>
        /// <param name="setCommandId"></param>
        public async Task ChangeFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel changedFuturesOptionContract, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.ChangeFuturesOptionContractAsync(originalContractId, changedFuturesOptionContract, setCommandId));

        /// <summary>
        /// remove futures option contract
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="setCommandId"></param>
        public async Task RemoveFuturesOptionContractAsync(string contractId, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.RemoveFuturesOptionContractAsync(contractId, setCommandId));

        /// <summary>
        /// add yield curve rate
        /// </summary>
        /// <param name="yieldCurveRate"></param>
        /// <param name="setCommandId"></param>
        public async Task AddYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.AddYieldCurveRateAsync(yieldCurveRate, setCommandId));

        /// <summary>
        /// change yield curve rate
        /// </summary>
        /// <param name="yieldCurveRate"></param>
        /// <param name="setCommandId"></param>
        public async Task ChangeYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.ChangeYieldCurveRateAsync(yieldCurveRate, setCommandId));

        /// <summary>
        /// remove yield curve rate
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="setCommandId"></param>
        public async Task RemoveYieldCurveRateAsync(DateTime valueDate, Action<Guid> setCommandId)
            => await ExecuteCommandAsync(() => _commandApi.RemoveYieldCurveRateAsync(valueDate, setCommandId));

        /// <summary>
        /// import yield curve rates
        /// </summary>
        /// <param name="importDate"></param>
        /// <param name="yieldCurveRates"></param>
        /// <param name="setCommandId"></param>
        public async Task ImportYieldCurveRatesAsync(DateTime importDate, YieldCurveRateReadModel[] yieldCurveRates, Action<Guid> setCommandId = null)
            => await ExecuteCommandAsync(() => _commandApi.ImportYieldCurveRatesAsync(importDate, yieldCurveRates, setCommandId));

    }
}
