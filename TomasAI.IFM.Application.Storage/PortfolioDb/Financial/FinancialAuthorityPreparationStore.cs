using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Reads financial preparation inputs at one revision; external catalog/source reads happen after this transaction ends.</summary>
public sealed class FinancialAuthorityPreparationStore(IPostgresEventTransaction transactions)
{
    public Task<FinancialAuthorityPreparationSnapshot> ReadAsync(int portfolioId, CancellationToken token)
        => transactions.ExecuteAsync(async (db, ct) =>
    {
        var rows = await db.QueryAsync(PortfolioDbSql.Financial.FinancialAuthorityPreparationStore.Select01, [portfolioId], r => (Book: Decode<FinancialBookConfiguration>(r.GetString(0)), Revision: r.GetInt64(1), Epoch: r.GetInt64(2), State: r.GetString(3)), ct);
        Require(rows.Count == 1, FinancialReasons.AuthorityDenied, "A configured financial book is required.");
        var row = rows.Single(); var cash = new Dictionary<int, decimal>();
        Require(row.Book.Funds.Length <= 128, FinancialReasons.InvalidContract, "Financial membership exceeds its supported bound.");
        foreach (var fund in row.Book.Funds) cash.Add(fund.FundId, await GeneralLedgerStore.AvailableCash(db, row.Book.BookId, fund.FundId, ct));
        return new FinancialAuthorityPreparationSnapshot(row.Book, row.Revision, row.Epoch, row.State, cash);
    }, token);
}
