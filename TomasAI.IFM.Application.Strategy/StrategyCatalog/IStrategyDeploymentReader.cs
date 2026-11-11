using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Application.Strategy.StrategyCatalog;

/// <summary>Qualifies an exact published deployment after reading a committed catalog snapshot.</summary>
public interface IStrategyDeploymentReader
{
    /// <summary>Reads and qualifies a published deployment at the requested effective time.</summary>
    Task<StrategyCatalogSnapshot> ReadPublishedAsync(IConfigurationDbReadContext database, CatalogKey key,
        DateTime asOfUtc, CancellationToken token = default);
}
