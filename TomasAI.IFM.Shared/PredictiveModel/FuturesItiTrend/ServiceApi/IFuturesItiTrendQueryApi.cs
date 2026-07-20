using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ServiceApi;

public interface IFuturesItiTrendQueryApi
{
    Task<ServiceResult<ScalarValue<double>>> GetPredictedTrendDeltaAsync(FuturesItiTrendDeltaDataReadModel trendData);
    Task<ServiceResult<FuturesItiTrendCoastLineCountersReadModel>> GetFuturesItiTrendCoastLineCountersAsync(
        string contractId, DateOnly valueDate, string symbol, double predictedTrendDelta);
}
