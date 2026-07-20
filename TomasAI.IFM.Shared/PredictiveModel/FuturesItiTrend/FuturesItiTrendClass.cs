namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;

public record FuturesItiTrendClass(
    string Symbol,
    DateOnly ValueDate,
    DateTime Timestamp,
    bool TrendClass,
    float TrendDelta,
    float TrendDirection,
    float TrendDirectionMode,
    float FuturesRSI);
