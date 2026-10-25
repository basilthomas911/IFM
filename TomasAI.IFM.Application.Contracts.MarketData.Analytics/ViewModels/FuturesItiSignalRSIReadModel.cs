using Newtonsoft.Json;
using System;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
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
