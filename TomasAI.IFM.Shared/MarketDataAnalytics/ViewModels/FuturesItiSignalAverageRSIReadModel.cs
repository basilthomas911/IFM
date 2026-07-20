using Newtonsoft.Json;
using System;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels
{
    public record FuturesItiSignalAverageRSIReadModel(
        string ContractId,
        DateOnly valueDate,
        double UpTrendRSI,
        double DownTrendRSI)
    {
        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    }
}
