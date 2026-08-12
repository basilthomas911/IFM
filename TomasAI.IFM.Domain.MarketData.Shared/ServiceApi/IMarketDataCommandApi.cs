using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi
{
    public interface IMarketDataCommandApi
    {
        Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar);
        Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId economicCalendarId, bool overwrite);
        Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, bool overwrite);
        Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync(DateTime importedDate, EconomicCalendarReadModel[] economicCalendars);
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
