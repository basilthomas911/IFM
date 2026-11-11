using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

/// <summary>Persists immutable pricing and contract reference facts on a supplied PostgreSQL transaction.</summary>
public sealed class PostgresSecuritiesReferenceStore
{
    /// <summary>Stores a content-addressed bundle; the same identifier cannot acquire different content.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task InsertBundleAsync(IEnlistedPostgresTransaction db, string id, string payload, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_InsertBundleAsync_1, [id, payload], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Pricing bundle identifier contains different content.");
    }

    /// <summary>Reserves the exact immutable IFM/provider identity; conflicting claims roll back the caller's transaction.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ClaimIdentityAsync(IEnlistedPostgresTransaction db, string key, string binding, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_ClaimIdentityAsync_1, [key, binding], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException($"Provider/IFM identity collision. Existing contract identities cannot be overwritten. IdentityKey={key}");
    }

    /// <summary>Atomically claims both identities and stages one immutable mapping version without publishing it.</summary>
    public async Task StageAsync(IEnlistedPostgresTransaction db, string contractId, string providerKey, string identity,
        string version, string digest, byte[] payload, CancellationToken token = default, FuturesOptionContractReadModel? option = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (option is not null)
        {
            var legacyKey = OptionReferenceIdentity.LegacyProviderKey(option);
            var legacy = await db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_StageAsync_1, [legacyKey], token)
            .ConfigureAwait(false);
            if (legacy is string binding)
            {
                var existing = OptionReferenceIdentity.ExistingProviderBinding(option, new Dictionary<string, string> { [legacyKey] = binding });
                if (existing is not null && existing != identity)
                    throw new InvalidOperationException($"Provider/IFM identity collision. Existing contract identities cannot be overwritten. IdentityKey={legacyKey}");
            }
        }
        foreach(var key in new[]{"ifm:"+contractId,providerKey}.Order(StringComparer.Ordinal))
            await (ClaimIdentityAsync(db,key,identity,token))
            .ConfigureAwait(false);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_StageAsync_2, [contractId, version, digest, payload], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Mapping version already contains different content. Publish a new reviewed version.");
    }

    /// <summary>Publishes only the exact previously accepted mapping digest; missing or different versions are refused.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task PublishAsync(IEnlistedPostgresTransaction db, string contractId, string version, string digest, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync(SecuritiesDbSql.PostgresReferenceStore_PublishAsync_1, [contractId, version, digest], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Reference publication did not complete.");
    }
    /// <summary>Writes a complete immutable option definition generation and its published pointer in one transaction.</summary>
    /// <remarks>Previous generations remain readable by requests that already captured their pointer; cleanup is separate maintenance.</remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task PublishDefinitionsAsync(IEnlistedPostgresTransaction db, string symbol, Guid generation,
        DateOnly coverageFrom, DateOnly coverageThrough, DateTime refreshedAtUtc,
        IReadOnlyList<(DateOnly Expiry, string Root, string ContractId, string UnderlyingId, string Family, byte[] Payload)> rows,
        CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(db);
        await (db.ExecuteAsync(SecuritiesDbSql.PostgresReferenceStore_PublishDefinitionsAsync_1,["securities.option-definitions:"+symbol],token))
            .ConfigureAwait(false);
        foreach(var row in rows)
            await (db.ExecuteAsync(SecuritiesDbSql.PostgresReferenceStore_PublishDefinitionsAsync_2,[symbol,generation,row.Expiry,row.Root,row.ContractId,row.UnderlyingId,row.Family,row.Payload,refreshedAtUtc],token))
            .ConfigureAwait(false);
        await (db.ExecuteAsync(SecuritiesDbSql.PostgresReferenceStore_PublishDefinitionsAsync_3,[symbol,generation,coverageFrom,coverageThrough,refreshedAtUtc],token))
            .ConfigureAwait(false);
    }

}
