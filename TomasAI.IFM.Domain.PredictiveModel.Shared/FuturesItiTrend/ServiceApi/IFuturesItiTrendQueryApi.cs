using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend.ServiceApi;

public interface IFuturesItiTrendQueryApi
{
    Task<ServiceResult<ScalarValue<double>>> GetPredictedTrendDeltaAsync(FuturesItiTrendDeltaDataReadModel trendData);
    Task<ServiceResult<FuturesItiTrendCoastLineCountersReadModel>> GetFuturesItiTrendCoastLineCountersAsync(
        string contractId, DateOnly valueDate, string symbol, double predictedTrendDelta);
}
