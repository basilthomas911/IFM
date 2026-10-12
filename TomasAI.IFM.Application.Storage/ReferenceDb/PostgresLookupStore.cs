using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Materializes accepted lookup facts on the source transaction without notifications or timestamp generation.</summary>
public sealed class PostgresLookupStore
{
    /// <summary>Serializes mutations for a lookup name, including empty partitions.</summary>
    static async Task LockAsync(IEnlistedPostgresTransaction transaction, string name, CancellationToken token)
        => await (transaction.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended($1,0));", ["reference.lookup_type:" + name], token)).ConfigureAwait(false);

    /// <summary>Upserts the accepted row, preserving its creation metadata during rebuilding.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task InsertAsync(IEnlistedPostgresTransaction transaction, LookupTypeReadModel row, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(transaction);
        await (LockAsync(transaction, row.LookupTypeName, token)).ConfigureAwait(false);
        await (transaction.ExecuteAsync("""
            INSERT INTO reference.lookup_type (lookupTypeName,shortCode,orderId,description,createdOn,createdBy)
            VALUES ($1,$2,$3,$4,$5,$6) ON CONFLICT (lookupTypeName,orderId,shortCode)
            DO UPDATE SET description=EXCLUDED.description,createdOn=EXCLUDED.createdOn,createdBy=EXCLUDED.createdBy;
            """, [row.LookupTypeName,row.ShortCode,row.OrderId,row.Description,row.CreatedOn,row.CreatedBy], token)).ConfigureAwait(false);
    }

    /// <summary>Atomically replaces the old identifier with the accepted row. Locks names in stable order.</summary>
    public async Task UpdateAsync(IEnlistedPostgresTransaction transaction, LookupTypeId oldId, LookupTypeReadModel row, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(oldId);
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(transaction);
        foreach (var name in new[] {oldId.LookupTypeName,row.LookupTypeName}.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            await (LockAsync(transaction,name,token)).ConfigureAwait(false);
        await (transaction.ExecuteAsync("DELETE FROM reference.lookup_type WHERE lookupTypeName=$1 AND orderId=$2;",[oldId.LookupTypeName,oldId.OrderId],token)).ConfigureAwait(false);
        await (InsertAsync(transaction,row,token)).ConfigureAwait(false);
    }

    /// <summary>Deletes and renumbers the remaining ordered rows in one transaction, without changing creation metadata.</summary>
    /// <remarks>The source-event checkpoint must prevent repeated application of this positional deletion.</remarks>
    public async Task DeleteAsync(IEnlistedPostgresTransaction transaction, LookupTypeId id, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(transaction);
        await (LockAsync(transaction,id.LookupTypeName,token)).ConfigureAwait(false);
        var survivors=await (transaction.QueryAsync("""
            SELECT lookupTypeName,shortCode,orderId,description,createdOn,createdBy FROM reference.lookup_type
            WHERE lookupTypeName=$1 AND orderId<>$2 ORDER BY orderId,shortCode;
            """,[id.LookupTypeName,id.OrderId],reader=>new LookupTypeReadModel(reader.GetString(0),reader.GetString(1),reader.GetInt32(2),reader.GetString(3),reader.GetDateTime(4),reader.GetString(5)),token)).ConfigureAwait(false);
        await (transaction.ExecuteAsync("DELETE FROM reference.lookup_type WHERE lookupTypeName=$1;",[id.LookupTypeName],token)).ConfigureAwait(false);
        var ordinal=0;
        foreach(var row in survivors) await (InsertAsync(transaction,row with {OrderId=ordinal++},token)).ConfigureAwait(false);
    }
}
