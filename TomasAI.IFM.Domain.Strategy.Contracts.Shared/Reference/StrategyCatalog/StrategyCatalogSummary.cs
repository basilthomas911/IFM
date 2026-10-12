using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public sealed record StrategyCatalogSummary(CatalogKey Key, string Code, string Name,
    CatalogLifecycleStatus Status, string ContentHash);
