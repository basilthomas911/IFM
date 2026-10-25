using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>Exact external catalog validation, supplied by the owning domain adapter; never a caller approval flag.</summary>
public interface IStrategyCatalogReferences
{
    Task ValidateProductAsync(CatalogProduct product, CancellationToken cancellationToken);
    Task ValidateLegacyFamilyAsync(CatalogLegacyFamily family, StrategyCatalogDefinition deployment,
        CancellationToken cancellationToken);
}
