using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

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
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.append");
        // Also serializes first-time book creation with configuration writes before an authority row exists.
        using (FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.advisory_lock"))
            await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Select01,[portfolioId],ct);
        using var lockTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.lock_authority");
        var rows=await db.QueryAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Select02,[portfolioId],r=>(Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Epoch:r.GetInt64(1),Revision:r.GetInt64(2),State:r.GetString(3)),ct);
        lockTrace?.Stop();
        if (domainEvent is IFundRiskAuthorizedEvent { FinancialAuthorization: { } authorization })
        {
            using var validateTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.validate_authorization");
            Require(rows.Count == 1, FinancialReasons.AuthorityDenied, "Financial authority is required for Fund approval.");
            await FundRiskAuthorizationStore.ValidateAsync(db, portfolioId, fundId, authorization, rows[0].Book, rows[0].State, ct);
        }
        if (domainEvent is IFundRiskTerminalEvent { TerminalRisk: { } terminal })
        {
            var source = await CapacityReservationStore.ReadEvidenceAsync<IRiskWorkflowTerminalEvent>(db, terminal.SourceCommandId, ct);
            Require(source?.TerminalRisk == terminal && terminal.PortfolioId == portfolioId && terminal.FundId == fundId,
                FinancialReasons.AuthorityDenied, "Exact committed terminal workflow evidence is required.");
            var live = await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Select03,
                [portfolioId, terminal.OrderId], ct);
            Require(live is null, FinancialReasons.InvalidLifecycle, "Capacity remains reserved or consumed; terminal Fund synchronization awaits reconciliation.");
        }
        await db.AppendAsync(stream,domainEvent.CommandId,domainEvent,expectedRevision,ct);
        if(rows.Count==0) return true;
        if(domainEvent is IFundRiskTerminalEvent { TerminalRisk: not null })
            await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Update01,[portfolioId],ct);
        var current=rows[0];
        if(changesAuthority)
        {
            await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Update02,[portfolioId,checked(current.Epoch+1),checked(current.Revision+1)],ct);
        }
        else if(fundId is { } fund)
        {
            // Composing/accepting an order advances the Fund stream but does not change its mandate.
            // Keep the verified source fence current without silently changing economic authority.
            var book=current.Book with { Funds=current.Book.Funds.Select(x=>x.FundId==fund?x with { FundStreamVersion=checked(expectedRevision+1) }:x).ToArray() };
            await db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Update03,[portfolioId,Json(book)],ct);
        }
        return true;
    },token);
}
