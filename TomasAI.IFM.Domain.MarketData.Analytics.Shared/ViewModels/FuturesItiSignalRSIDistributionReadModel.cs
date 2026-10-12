using Newtonsoft.Json;
using System;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
    public record FuturesItiSignalRSIDistributionReadModel(
        string ContractId,
        DateOnly valueDate,
        double Mean,
        double StdDev)
    {
        public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
    }
}
