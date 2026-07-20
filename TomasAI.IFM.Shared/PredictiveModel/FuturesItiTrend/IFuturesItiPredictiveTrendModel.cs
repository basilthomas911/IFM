using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;

public interface IFuturesItiPredictiveTrendModel
{
    Task<(IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Model, 
        IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Training,
        IReadOnlyList<FuturesItiTrendDeltaDataReadModel> Test)> LoadTrendDeltaDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task SaveTrendDeltaModelAsync(FuturesItiTrendDeltaModelReadModel trendModel);

    Task<(IReadOnlyList<FuturesItiTrendClassDataReadModel> Model,
        IReadOnlyList<FuturesItiTrendClassDataReadModel> Training,
        IReadOnlyList<FuturesItiTrendClassDataReadModel> Test)> LoadTrendClassDataAsync(string symbol, DateOnly startDate, DateOnly endDate);
    Task SaveTrendClassModelAsync(FuturesItiTrendClassModelReadModel trendModel);

    double PredictedUpTrendDelta(FuturesItiSignalV2ReadModel e);
    double PredictedDownTrendDelta(FuturesItiSignalV2ReadModel e);
    double AverageUpTrendFuturesRSI(FuturesItiSignalV2ReadModel e);
    double AverageDownTrendFuturesRSI(FuturesItiSignalV2ReadModel e);

    int SetUpTrendCoastLineCounter(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta);
    int SetDownTrendCoastLineCounter(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta);
    IntrinsicTimePredictedTrendStrengthType PredictedUpTrendStrength(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta);
    IntrinsicTimePredictedTrendStrengthType PredictedDownTrendStrength(FuturesItiSignalV2ReadModel e, string symbol, double predictedTrendDelta);
}
