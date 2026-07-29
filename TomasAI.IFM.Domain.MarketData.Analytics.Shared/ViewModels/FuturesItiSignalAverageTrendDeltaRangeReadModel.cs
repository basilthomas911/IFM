using System;
using Newtonsoft.Json;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels
{
    public record FuturesItiSignalAverageTrendDeltaRangeReadModel(
        string Symbol,
        DateTime StartDate,
        DateTime EndDate,
        double UpTrendDelta,
        double DownTrendDelta)
    {
        public override string ToString() =>  JsonConvert.SerializeObject(this, Formatting.None); 
    }
}
