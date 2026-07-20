using Microsoft.ML.Data;

namespace TomasAI.IFM.Application.PredictiveModel
{
    public class FuturesItiTrendDeltaPrediction
    {
        [ColumnName("Score")]
        public float TrendDelta { get; set; }

    }
}
