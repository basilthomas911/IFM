namespace TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend;

public record FuturesItiTrendDelta (
    string Symbol,
    DateOnly ValueDate,
    DateTime Timestamp,
    float TrendDelta,
    float TrendDirection,
    float TrendDirectionMode,
    float FuturesPrice,
    float TrendExtreme,
    float FuturesRSI );
