using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

public sealed record StoredStrategyCatalogDefinition(StrategyCatalogDefinition Definition, string ContentHash,
    CatalogLifecycleStatus Status, DateTime CreatedUtc, string CreatedBy,
    DateTime? EffectiveFromUtc, string? PublishedBy, DateTime? RetiredAtUtc, string? RetiredBy);
