using Newtonsoft.Json;
using System;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels
{
    public record FuturesItiSignalRSIReadModel(
        string ContractId,
        DateOnly valueDate,
        DateTime IntrinsicTime,
        double IntrinsicPrice,
        IntrinsicTimeTrendType IntrinsicTimeTrend,
        IntrinsicTimeModeType IntrinsicTimeMode,
        double FuturesRSI)
    {
        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    }
}
