using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public sealed record CatalogPipelineParameter(string Role, CatalogPipelineParameterKind Kind, Guid Id, int Version, string Hash);
