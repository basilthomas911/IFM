using Microsoft.ML.Data;

namespace TomasAI.IFM.Application.PredictiveModel;

public class FuturesItiTrendClassPrediction
{
    [ColumnName("PredictedLabel")]
    public bool TrendClass { get; set; }
}
