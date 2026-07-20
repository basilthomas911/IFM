using Newtonsoft.Json;
using System;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels
{
    public record FuturesItiSignalRSIDistributionReadModel(
        string ContractId,
        DateOnly valueDate,
        double Mean,
        double StdDev)
    {
        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    }
}
