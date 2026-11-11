using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.Persistence;

/// <summary>Qualifies and publishes one complete immutable generation of option definitions.</summary>
public sealed class SecuritiesOptionDefinitionPublisher(ISecuritiesDbWriteContext database, TimeProvider? clock = null)
{
    /// <summary>Validates the selected coverage and identities before acquiring a PostgreSQL transaction.</summary>
    /// <param name="symbol">The underlying root symbol.</param>
    /// <param name="coverageFrom">The inclusive first expiry.</param>
    /// <param name="coverageThrough">The inclusive last expiry.</param>
    /// <param name="definitions">The provider definitions prepared by the caller.</param>
    /// <param name="cancellationToken">Cancels preparation or publication.</param>
    /// <returns>Completion after the generation and its pointer commit.</returns>
    public Task PublishAsync(string symbol, DateOnly coverageFrom, DateOnly coverageThrough,
        IReadOnlyCollection<CachedOptionContractDefinitionReadModel> definitions, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentOutOfRangeException.ThrowIfLessThan(coverageThrough, coverageFrom);
        var normalized = symbol.Trim().ToUpperInvariant();
        if (definitions.Any(row => row is null || row.Definition is null
            || !string.Equals(row.Symbol, normalized, StringComparison.OrdinalIgnoreCase)
            || row.ExpiryDate < coverageFrom || row.ExpiryDate > coverageThrough
            || string.IsNullOrWhiteSpace(row.UnderlyingContractId)
            || string.IsNullOrWhiteSpace(row.ProviderRoot)
            || string.IsNullOrWhiteSpace(row.Definition.ContractId)))
            throw new ArgumentException("Cached option definitions must be complete and within the published coverage.", nameof(definitions));

        var rows = definitions.Select(row => row with { ProviderRoot = row.ProviderRoot.ToUpperInvariant() })
            .DistinctBy(row => (row.ExpiryDate, row.ProviderRoot, row.Definition.ContractId)).ToArray();
        var publication = new OptionDefinitionPublication(normalized, Guid.NewGuid(), coverageFrom, coverageThrough,
            (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime, rows);
        return database.PublishOptionDefinitionsAsync(publication, cancellationToken);
    }
}
