using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

public interface IPortfolioAuthorityFence
{
    Task AppendAsync(int portfolioId,int? fundId,string stream,IEvent domainEvent,long expectedRevision,bool changesAuthority,CancellationToken token=default);
}

/// <summary>Existing Portfolio/Fund/policy writers participate in the same financial admission lock.</summary>
public sealed class PortfolioAuthorityFence(IPostgresEventTransaction transactions):IPortfolioAuthorityFence
{
    public Task AppendAsync(int portfolioId,int? fundId,string stream,IEvent domainEvent,long expectedRevision,bool changesAuthority,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,ct)=>
    {
        // Also serializes first-time book creation with configuration writes before an authority row exists.
        await db.ScalarAsync("SELECT pg_advisory_xact_lock(34100,$1);",[portfolioId],ct);
        var rows=await db.QueryAsync("""
            SELECT policy_source_versions::text,authority_epoch,financial_revision,operating_state FROM portfolio_financial.financial_authority
            WHERE portfolio_id=$1 FOR UPDATE;
            """,[portfolioId],r=>(Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Epoch:r.GetInt64(1),Revision:r.GetInt64(2),State:r.GetString(3)),ct);
        if (domainEvent is IFundRiskAuthorizedEvent { FinancialAuthorization: { } authorization })
        {
            Require(rows.Count == 1, FinancialReasons.AuthorityDenied, "Financial authority is required for Fund approval.");
            await FundRiskAuthorizationStore.ValidateAsync(db, portfolioId, fundId, authorization, rows[0].Book, rows[0].State, ct);
        }
        if (domainEvent is IFundRiskTerminalEvent { TerminalRisk: { } terminal })
        {
            var source = await CapacityReservationStore.ReadEvidenceAsync<IRiskWorkflowTerminalEvent>(db, terminal.SourceCommandId, ct);
            Require(source?.TerminalRisk == terminal && terminal.PortfolioId == portfolioId && terminal.FundId == fundId,
                FinancialReasons.AuthorityDenied, "Exact committed terminal workflow evidence is required.");
            var live = await db.ScalarAsync("SELECT reservation_id FROM portfolio_financial.capacity_reservation WHERE portfolio_id=$1 AND order_id=$2 AND status NOT IN (8,9) LIMIT 1;",
                [portfolioId, terminal.OrderId], ct);
            Require(live is null, FinancialReasons.InvalidLifecycle, "Capacity remains reserved or consumed; terminal Fund synchronization awaits reconciliation.");
        }
        await db.AppendAsync(stream,domainEvent.CommandId,domainEvent,expectedRevision,ct);
        if(rows.Count==0) return true;
        if(domainEvent is IFundRiskTerminalEvent { TerminalRisk: not null })
            await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET financial_revision=financial_revision+1 WHERE portfolio_id=$1;",[portfolioId],ct);
        var current=rows[0];
        if(changesAuthority)
        {
            await db.ExecuteAsync("""
                UPDATE portfolio_financial.financial_authority SET authority_epoch=$2,financial_revision=$3,operating_state='NeedsRefresh'
                WHERE portfolio_id=$1;
                """,[portfolioId,checked(current.Epoch+1),checked(current.Revision+1)],ct);
        }
        else if(fundId is { } fund)
        {
            // Composing/accepting an order advances the Fund stream but does not change its mandate.
            // Keep the verified source fence current without silently changing economic authority.
            var book=current.Book with { Funds=current.Book.Funds.Select(x=>x.FundId==fund?x with { FundStreamVersion=checked(expectedRevision+1) }:x).ToArray() };
            await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET policy_source_versions=$2 WHERE portfolio_id=$1;",[portfolioId,Json(book)],ct);
        }
        return true;
    },token);
}
