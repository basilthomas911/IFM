using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

public partial interface IConfigurationDbContext
{
    /// <summary>Atomically inserts a complete immutable Draft and its normalized children. Version must equal expectedPreviousVersion + 1.</summary>
    Task<string> InsertStrategyCatalogDraftAsync(StrategyCatalogDefinition definition, int expectedPreviousVersion,
        string createdBy, CancellationToken cancellationToken = default);

    Task<StoredStrategyCatalogDefinition?> GetStrategyCatalogAsync(CatalogKey key, CancellationToken cancellationToken = default);

    /// <summary>Latest version per identity, ordered by stable code. Use the last returned Code as the next cursor.</summary>
    Task<IReadOnlyList<StrategyCatalogSummary>> ListStrategyCatalogAsync(StrategyCatalogKind kind, int limit = 50,
        string? afterCode = null, CancellationToken cancellationToken = default);

    /// <summary>Validates the exact dependency graph and trusted capabilities before publishing one Draft.</summary>
    Task PublishStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime effectiveFromUtc,
        string publishedBy, CancellationToken cancellationToken = default);

    Task RetireStrategyCatalogAsync(CatalogKey key, string expectedContentHash, DateTime retiredAtUtc,
        string retiredBy, CancellationToken cancellationToken = default);

    /// <summary>Returns validated catalog evidence only. Portfolio permission and workflow activation are separate.</summary>
    Task<StrategyCatalogSnapshot> GetPublishedStrategyDeploymentAsync(CatalogKey deployment, DateTime asOfUtc,
        CancellationToken cancellationToken = default);
}
