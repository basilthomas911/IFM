namespace TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend;

public record FuturesItiTrendClass(
    string Symbol,
    DateOnly ValueDate,
    DateTime Timestamp,
    bool TrendClass,
    float TrendDelta,
    float TrendDirection,
    float TrendDirectionMode,
    float FuturesRSI);
