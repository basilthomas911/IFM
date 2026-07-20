using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

public record FuturesItiSignalAveragePredictedTrendDeltaRangeReadModel(
    string Symbol,
    DateOnly StartDate,
    DateOnly EndDate,
    double PredictedUpTrendDelta,
    double PredictedDownTrendDelta)
{
    public override string ToString() =>  JsonConvert.SerializeObject(this, Formatting.None); 
}
