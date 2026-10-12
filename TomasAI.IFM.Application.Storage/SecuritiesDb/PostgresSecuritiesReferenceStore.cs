using TomasAI.IFM.Application.Storage.EventSourceDb;

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
        var saved = await (db.ScalarAsync("""
            INSERT INTO securities.option_pricing_reference_bundle(bundle_id,payload) VALUES($1,$2)
            ON CONFLICT(bundle_id) DO UPDATE SET payload=EXCLUDED.payload
            WHERE securities.option_pricing_reference_bundle.payload=EXCLUDED.payload RETURNING bundle_id;
            """, [id,payload], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Pricing bundle identifier contains different content.");
    }

    /// <summary>Reserves the exact immutable IFM/provider identity; conflicting claims roll back the caller's transaction.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ClaimIdentityAsync(IEnlistedPostgresTransaction db, string key, string binding, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync("""
            INSERT INTO securities.securities_reference_identity(identity_key,binding) VALUES($1,$2)
            ON CONFLICT(identity_key) DO UPDATE SET binding=EXCLUDED.binding
            WHERE securities.securities_reference_identity.binding=EXCLUDED.binding RETURNING identity_key;
            """, [key,binding], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Provider/IFM identity collision. Existing contract identities cannot be overwritten.");
    }

    /// <summary>Atomically claims both identities and stages one immutable mapping version without publishing it.</summary>
    public async Task StageAsync(IEnlistedPostgresTransaction db, string contractId, string providerKey, string identity,
        string version, string digest, byte[] payload, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        foreach(var key in new[]{"ifm:"+contractId,providerKey}.Order(StringComparer.Ordinal))
            await (ClaimIdentityAsync(db,key,identity,token))
            .ConfigureAwait(false);
        var saved = await (db.ScalarAsync("""
            INSERT INTO securities.securities_reference_version(contract_id,version,digest,payload,published)
            VALUES($1,$2,$3,$4,false) ON CONFLICT(contract_id,version) DO UPDATE SET digest=EXCLUDED.digest
            WHERE securities.securities_reference_version.digest=EXCLUDED.digest
              AND securities.securities_reference_version.payload=EXCLUDED.payload RETURNING version;
            """, [contractId,version,digest,payload], token))
            .ConfigureAwait(false);
        if(saved is null) throw new InvalidOperationException("Mapping version already contains different content. Publish a new reviewed version.");
    }

    /// <summary>Publishes only the exact previously accepted mapping digest; missing or different versions are refused.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task PublishAsync(IEnlistedPostgresTransaction db, string contractId, string version, string digest, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var saved = await (db.ScalarAsync("""
            UPDATE securities.securities_reference_version SET published=true
            WHERE contract_id=$1 AND version=$2 AND digest=$3 RETURNING version;
            """, [contractId,version,digest], token))
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
        ArgumentNullException.ThrowIfNull(rows);
        await (db.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));",["securities.option-definitions:"+symbol],token))
            .ConfigureAwait(false);
        foreach(var row in rows)
            await (db.ExecuteAsync("""
                INSERT INTO securities.option_contract_expiry_calendar
                (symbol,generation,expiryDate,providerRoot,contractId,underlyingContractId,optionFamily,definitionPayload,refreshedAtUtc)
                VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9);
                """,[symbol,generation,row.Expiry,row.Root,row.ContractId,row.UnderlyingId,row.Family,row.Payload,refreshedAtUtc],token))
            .ConfigureAwait(false);
        await (db.ExecuteAsync("""
            INSERT INTO securities.option_contract_expiry_calendar_state(symbol,generation,coverageFrom,coverageThrough,refreshedAtUtc)
            VALUES($1,$2,$3,$4,$5) ON CONFLICT(symbol) DO UPDATE SET generation=EXCLUDED.generation,
            coverageFrom=EXCLUDED.coverageFrom,coverageThrough=EXCLUDED.coverageThrough,refreshedAtUtc=EXCLUDED.refreshedAtUtc;
            """,[symbol,generation,coverageFrom,coverageThrough,refreshedAtUtc],token))
            .ConfigureAwait(false);
    }

}
