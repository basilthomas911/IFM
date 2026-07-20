using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;

public interface IFuturesItiTrendCommandApi
{
    Task<ServiceResult<Guid>> BuildFuturesItiTrendModelAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<Guid>> LoadFuturesItiTrendDeltaModelDataAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<Guid>> LoadFuturesItiTrendClassModelDataAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<Guid>> TrainFuturesItiTrendDeltaModelAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, FuturesItiTrendModelDataStatistics statistics);
    Task<ServiceResult<Guid>> TrainFuturesItiTrendClassModelAsync(string symbol, DateOnly valueDate, DateOnly startDate, DateOnly endDate, FuturesItiTrendModelDataStatistics statistics);
}
