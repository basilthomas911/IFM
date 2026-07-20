using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.ServiceApi
{
    public interface IMarketDataCommandApi
    {
        Task<ServiceResult<Guid>> AddFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract, bool overwrite);
        Task<ServiceResult<Guid>> AddFuturesOptionContractsAsync(int year, FuturesOptionContractReadModel[] futuresOptionContracts);
        Task<ServiceResult<Guid>> RemoveFuturesOptionContractAsync(string contractId, bool overwrite);
        Task<ServiceResult<Guid>> ChangeFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel futuresOptionContract, bool overwrite);

        Task<ServiceResult<Guid>> AddFuturesContractAsync(FuturesContractV2ReadModel futuresContract, bool overwrite);
        Task<ServiceResult<Guid>> RemoveFuturesContractAsync(FuturesContractId contractId, bool overwrite);
        Task<ServiceResult<Guid>> ChangeFuturesContractAsync(FuturesContractId originalContract, FuturesContractV2ReadModel futuresContract, bool overwrite);

        Task<ServiceResult<Guid>> AddYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite);
        Task<ServiceResult<Guid>> ChangeYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite);
        Task<ServiceResult<Guid>> RemoveYieldCurveRateAsync(DateOnly valueDate, bool overwrite);
        Task<ServiceResult<Guid>> ImportYieldCurveRatesAsync(DateTime importDate, YieldCurveRateReadModel[] yieldCurveRates);
    }
}
