using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;
namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Restores recorded authority effects without revalidating mandates or invoking Fund commands.</summary>
public sealed class FinancialAuthorityProjection(IPostgresEventTransaction transactions)
{
    public Task ReplayAsync(FinancialAuthoritySnapshotEvent source,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.SchemaVersion!=1 || source.EventId<=0 || source.Id==Guid.Empty)
            throw new InvalidDataException("Committed authority facts required.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01,[source.PortfolioId],ct)).ConfigureAwait(false);
            if(await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerFinancialProjection.InsertCheckpoint,
                [source.Id,source.PortfolioId,source.EventId,source.FinancialRevision],ct)).ConfigureAwait(false)==0) return false;
            var current=await (LockAuthorityAsync(db,source.PortfolioId,null,ct)).ConfigureAwait(false);
            if(current.Revision!=source.PreviousFinancialRevision) throw new InvalidDataException("Authority source events must be rebuilt in financial revision order.");
            await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerConfigurationProjection.SetAuthority,
                [source.PortfolioId,Json(source.Book),source.MigrationState,source.OperatingState,source.Book.SourceWatermark,source.Book.ValuationWatermark,source.AuthorityEpoch],ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01,[source.PortfolioId,source.FinancialRevision],ct)).ConfigureAwait(false);
            return true;
        },token);
    }
}
