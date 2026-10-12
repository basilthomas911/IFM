using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

// These are persistence entity kinds, not a closed list of trading strategies.
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is intentionally invalid; preserve the published enum values.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum StrategyCatalogKind : short
{
    Family = 1, Strategy = 2, Structure = 3, Variant = 4,
    ParameterSchema = 5, ParameterSet = 6, Deployment = 7
}
