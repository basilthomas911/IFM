using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

internal static class FinancialOperationalEventPersistence
{
    // The caller holds the corresponding operation/row lock; source and materialization share its transaction.
    internal static async Task AppendAsync(EnlistedEventTransaction db,string stream,IEvent source,int portfolioId,CancellationToken ct)
    {
        var revision=Convert.ToInt64(await (db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select05,[stream],ct)).ConfigureAwait(false) ?? 0L,System.Globalization.CultureInfo.InvariantCulture);
        await (db.AppendAsync(stream,source.CommandId,source,revision,ct)).ConfigureAwait(false);
        await (ClaimAsync(db,source,portfolioId,ct)).ConfigureAwait(false);
    }
    internal static async Task<bool> ClaimAsync(EnlistedEventTransaction db,IEvent source,int portfolioId,CancellationToken ct)
        => await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerFinancialProjection.InsertCheckpoint,[source.Id,portfolioId,source.EventId,0L],ct)).ConfigureAwait(false)==1;
}
