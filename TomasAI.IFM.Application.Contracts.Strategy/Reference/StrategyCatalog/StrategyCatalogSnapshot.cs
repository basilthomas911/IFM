using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Catalog evidence only. It neither authorizes a Fund nor activates a workflow.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public strategy model contract.")]
public sealed record StrategyCatalogSnapshot(CatalogKey Deployment, DateTime AsOfUtc,
    StoredStrategyCatalogDefinition[] Definitions, string ContentHash);
