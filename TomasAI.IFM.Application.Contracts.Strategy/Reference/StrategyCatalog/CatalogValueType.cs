using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero is intentionally invalid; preserve the published enum values.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1720", Justification = "These enum members describe the actual catalog parameter data types in persisted schemas.")]
public enum CatalogValueType { Object = 1, Array = 2, String = 3, Decimal = 4, Integer = 5, Boolean = 6 }
