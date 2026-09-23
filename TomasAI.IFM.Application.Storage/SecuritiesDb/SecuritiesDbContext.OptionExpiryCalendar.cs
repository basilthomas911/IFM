using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

public partial class SecuritiesDbContext
{
    sealed record OptionExpiryCalendarState(
        Guid Generation, DateOnly CoverageFrom, DateOnly CoverageThrough, DateTime RefreshedAtUtc);

    static OptionExpiryCalendarState MapOptionExpiryCalendarState<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Framework.Storage.IObjectDataRecord => new(
            row.GetGuid(0), row.GetDateOnly(1), row.GetDateOnly(2), row.GetDateTime(3));

    static OptionContractExpiryReadModel MapOptionContractExpiry<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Framework.Storage.IObjectDataRecord => new()
        {
            Symbol = row.GetString(0),
            ContractId = row.IsNull(1) ? string.Empty : row.GetString(1),
            ExpiryDate = row.GetDateOnly(2),
            ProviderRoot = row.GetString(3),
            OptionFamily = row.GetString(4),
            RefreshedAtUtc = row.GetDateTime(5)
        };

    static CachedOptionContractDefinitionReadModel? MapCachedOptionContractDefinition<TDataRecord>(TDataRecord row)
        where TDataRecord : TomasAI.IFM.Framework.Storage.IObjectDataRecord
    {
        if (row.IsNull(1) || row.IsNull(5)) return null;
        return new()
        {
            Symbol = row.GetString(0), UnderlyingContractId = row.GetString(1),
            ExpiryDate = row.GetDateOnly(2), ProviderRoot = row.GetString(3),
            OptionFamily = row.GetString(4), Definition = ReferencePayloadCodec.ReadOption(row.GetBytes(5)),
            RefreshedAtUtc = row.GetDateTime(6)
        };
    }

    public async Task<IReadOnlyList<OptionContractExpiryReadModel>> GetOptionContractExpiriesAsync(
        string symbol,
        DateOnly fromExpiry,
        DateOnly throughExpiry,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (throughExpiry < fromExpiry) throw new ArgumentOutOfRangeException(nameof(throughExpiry));
        symbol = symbol.Trim().ToUpperInvariant();
        var db = _dbFactory.SecuritiesDb;
        var state = await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiryCalendarState)}",
                SecuritiesDbCql.GetOptionContractExpiryCalendarState)
            .SetParameters(new GetOptionContractExpiryCalendarState(symbol))
            .ExecuteSingleAsync(MapOptionExpiryCalendarState!, cancellationToken);
        if (state is null || fromExpiry < state.CoverageFrom || fromExpiry > state.CoverageThrough)
            return [];
        var rows = (await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiries)}",
                SecuritiesDbCql.GetOptionContractExpiries)
            .SetParameters(new GetOptionContractExpiries(symbol, state.Generation, fromExpiry, state.CoverageThrough))
            .ExecuteQueryAsync(MapOptionContractExpiry!, cancellationToken)).ToArray();
        var nearestAfter = rows.Where(row => row.ExpiryDate > throughExpiry)
            .Select(row => row.ExpiryDate).OrderBy(date => date).FirstOrDefault();
        return rows.Where(row => !string.IsNullOrWhiteSpace(row.ContractId)
                                 && (row.ExpiryDate <= throughExpiry
                                 || nearestAfter != default && row.ExpiryDate == nearestAfter))
            .DistinctBy(row => (row.ContractId, row.ExpiryDate, row.ProviderRoot)).ToArray();
    }

    public async Task<IReadOnlyList<CachedOptionContractDefinitionReadModel>> GetCachedOptionContractDefinitionsAsync(
        string symbol, string underlyingContractId, DateOnly expiryDate,
        IReadOnlyCollection<string>? providerRoots = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingContractId);
        symbol = symbol.Trim().ToUpperInvariant();
        var db = _dbFactory.SecuritiesDb;
        var state = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiryCalendarState)}",
                SecuritiesDbCql.GetOptionContractExpiryCalendarState)
            .SetParameters(new GetOptionContractExpiryCalendarState(symbol))
            .ExecuteSingleAsync(MapOptionExpiryCalendarState!, cancellationToken);
        if (state is null || expiryDate < state.CoverageFrom || expiryDate > state.CoverageThrough) return [];
        var roots = (providerRoots ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rows = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetCachedOptionContractDefinitions)}",
                SecuritiesDbCql.GetCachedOptionContractDefinitions)
            .SetParameters(new GetCachedOptionContractDefinitions(symbol, state.Generation, expiryDate))
            .ExecuteQueryAsync(MapCachedOptionContractDefinition!, cancellationToken);
        return rows.Where(row => row is not null
                                 && string.Equals(row.UnderlyingContractId, underlyingContractId, StringComparison.OrdinalIgnoreCase)
                                 && (roots.Count == 0 || roots.Contains(row.ProviderRoot)))
            .Select(row => row!).DistinctBy(row => row.Definition.ContractId).ToArray();
    }

    public async Task ReplaceOptionContractDefinitionsAsync(
        string symbol,
        DateOnly coverageFrom,
        DateOnly coverageThrough,
        IReadOnlyCollection<CachedOptionContractDefinitionReadModel> definitions,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(definitions);
        if (coverageThrough < coverageFrom) throw new ArgumentOutOfRangeException(nameof(coverageThrough));
        symbol = symbol.Trim().ToUpperInvariant();
        if (definitions.Any(row => !string.Equals(row.Symbol, symbol, StringComparison.OrdinalIgnoreCase)
                                || row.ExpiryDate < coverageFrom || row.ExpiryDate > coverageThrough
                                || string.IsNullOrWhiteSpace(row.UnderlyingContractId)
                                || string.IsNullOrWhiteSpace(row.ProviderRoot)
                                || string.IsNullOrWhiteSpace(row.Definition.ContractId)))
            throw new ArgumentException("Cached option definitions must be complete and within the published coverage.", nameof(definitions));

        var generation = Guid.NewGuid();
        var refreshedAtUtc = DateTime.UtcNow;
        var db = _dbFactory.SecuritiesDb;
        var previous = await db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetOptionContractExpiryCalendarState)}",
                SecuritiesDbCql.GetOptionContractExpiryCalendarState)
            .SetParameters(new GetOptionContractExpiryCalendarState(symbol))
            .ExecuteSingleAsync(MapOptionExpiryCalendarState!, cancellationToken);
        var definitionCommands = definitions
            .DistinctBy(row => (row.ExpiryDate, row.ProviderRoot.ToUpperInvariant(), row.Definition.ContractId))
            .Select(row => db
                .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.InsertOptionContractExpiry)}",
                    SecuritiesDbCql.InsertOptionContractExpiry)
                .SetParameters(new InsertOptionContractExpiry(
                    symbol, generation, row.ExpiryDate, row.ProviderRoot.ToUpperInvariant(),
                    row.Definition.ContractId, row.UnderlyingContractId, row.OptionFamily,
                    ReferencePayloadCodec.Write(row.Definition), refreshedAtUtc))
                .QueueCommand())
            .Cast<object>()
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();
        // A generation may contain many thousands of definition payloads. Sending them as one
        // logged CQL batch exceeds Scylla's batch-size limit. Write the unpublished generation
        // sequentially, then publish only its small state pointer after every row succeeds.
        if (definitionCommands.Count > 0)
            await db.ExecuteQueuedCommandsAsync(definitionCommands, false);
        var publish = db
            .Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.PublishOptionContractExpiryGeneration)}",
                SecuritiesDbCql.PublishOptionContractExpiryGeneration)
            .SetParameters(new PublishOptionContractExpiryGeneration(
                symbol, generation, coverageFrom, coverageThrough, refreshedAtUtc))
            .QueueCommand();
        cancellationToken.ThrowIfCancellationRequested();
        await db.ExecuteQueuedCommandsAsync([publish], true);
        if (previous is not null && previous.Generation != generation)
        {
            await db.ExecuteQueuedCommandsAsync(
            [
                db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteOptionContractExpiryGeneration)}",
                        SecuritiesDbCql.DeleteOptionContractExpiryGeneration)
                    .SetParameters(new DeleteOptionContractExpiryGeneration(symbol, previous.Generation))
                    .QueueCommand()
            ], true);
        }
    }
}
