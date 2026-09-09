using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public sealed record FinancialAuthorityPreparationSnapshot(FinancialBookConfiguration Book,long Revision,long Epoch,
    string State,IReadOnlyDictionary<int,decimal> AvailableCash);

/// <summary>Reads financial preparation inputs at one revision; external catalog/source reads happen after this transaction ends.</summary>
public sealed class FinancialAuthorityPreparationStore(IPostgresEventTransaction transactions)
{
    public Task<FinancialAuthorityPreparationSnapshot> ReadAsync(int portfolioId,CancellationToken token)
        =>transactions.ExecuteAsync(async(db,ct)=>
    {
        var rows=await db.QueryAsync("""
            SELECT policy_source_versions::text,financial_revision,authority_epoch,operating_state
            FROM portfolio_financial.financial_authority WHERE portfolio_id=$1 FOR SHARE;
            """,[portfolioId],r=>(Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Revision:r.GetInt64(1),Epoch:r.GetInt64(2),State:r.GetString(3)),ct);
        Require(rows.Count==1,FinancialReasons.AuthorityDenied,"A configured financial book is required.");
        var row=rows.Single();var cash=new Dictionary<int,decimal>();
        Require(row.Book.Funds.Length<=128,FinancialReasons.InvalidContract,"Financial membership exceeds its supported bound.");
        foreach(var fund in row.Book.Funds) cash.Add(fund.FundId,await GeneralLedgerStore.AvailableCash(db,row.Book.BookId,fund.FundId,ct));
        return new FinancialAuthorityPreparationSnapshot(row.Book,row.Revision,row.Epoch,row.State,cash);
    },token);
}
