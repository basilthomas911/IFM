namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>The exact before and after projections prepared under the catalog transaction's version fence.</summary>
public sealed record PreparedStrategyCatalogTransition(long StreamVersion, string RequestJson, StoredStrategyCatalogDefinition? BeforeVersion, StoredStrategyCatalogDefinition AfterVersion);
