using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

public record FuturesItiSignalAveragePredictedTrendDeltaDataModel(
    string ContractId,
    DateOnly ValueDate,
    double PredictedUpTrendDelta,
    double PredictedDownTrendDelta,
    double UpTrendFuturesRSI,
    double DownTrendFuturesRSI)
{
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
