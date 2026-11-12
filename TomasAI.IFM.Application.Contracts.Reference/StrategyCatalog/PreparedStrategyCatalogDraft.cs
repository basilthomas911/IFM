namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>A canonical draft and the exact parameter schema content validated by the caller.</summary>
public sealed record PreparedStrategyCatalogDraft(StrategyCatalogDefinition Definition, string? ParameterSchemaContentHash);
