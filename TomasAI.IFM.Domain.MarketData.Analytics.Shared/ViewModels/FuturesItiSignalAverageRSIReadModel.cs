using Newtonsoft.Json;
using System;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
    public record FuturesItiSignalAverageRSIReadModel(
        string ContractId,
        DateOnly valueDate,
        double UpTrendRSI,
        double DownTrendRSI)
    {
        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    }
}
