using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Existing Portfolio/Fund/policy writers participate in the same financial admission lock.</summary>
public sealed class PortfolioAuthorityFence(IPostgresEventTransaction transactions, FinancialDevelopmentPolicy? developmentPolicy = null) : IPortfolioAuthorityFence
{
    public Task AppendAsync(int portfolioId, int? fundId, string stream, IEvent domainEvent, long expectedRevision, bool changesAuthority, CancellationToken token = default)
        => transactions.ExecuteAsync((db,ct) => AppendEnlistedAsync(db,portfolioId,fundId,stream,domainEvent,expectedRevision,changesAuthority,ct),token);

    /// <summary>Commits the supplied related source transitions and projections as one atomic operation.</summary>
    public Task AppendBatchAsync(int portfolioId, IReadOnlyList<PortfolioAuthorityAppend> operations, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0) throw new ArgumentException("At least one source transition is required.",nameof(operations));
        if (operations.Select(x=>x.Source.CommandId).Distinct().Count()!=1)
            throw new ArgumentException("A compound operation requires one CommandId.",nameof(operations));
        return transactions.ExecuteAsync(async(db,ct) => {
            foreach (var operation in operations)
                await AppendEnlistedAsync(db,portfolioId,operation.FundId,operation.Stream,operation.Source,operation.ExpectedRevision,operation.ChangesAuthority,ct).ConfigureAwait(false);
            return true;
        },token);
    }

    async Task<bool> AppendEnlistedAsync(EnlistedEventTransaction db,int portfolioId,int? fundId,string stream,IEvent domainEvent,long expectedRevision,bool changesAuthority,CancellationToken ct)
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.append");
        // Also serializes first-time book creation with configuration writes before an authority row exists.
        using (FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.advisory_lock"))
            await (db.ScalarAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Select01, [portfolioId], ct)).ConfigureAwait(false);
        using var lockTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.lock_authority");
        var rows = await (db.QueryAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Select02, [portfolioId], r => (Book: Decode<FinancialBookConfiguration>(r.GetString(0)), Epoch: r.GetInt64(1), Revision: r.GetInt64(2), State: r.GetString(3)), ct)).ConfigureAwait(false);
        lockTrace?.Stop();
        if (domainEvent is IFundRiskAuthorizedEvent { FinancialAuthorization: { } authorization })
        {
            using var validateTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.validate_authorization");
            Require(rows.Count == 1, FinancialReasons.AuthorityDenied, "Financial authority is required for Fund approval.");
            await (FundRiskAuthorizationStore.ValidateAsync(db, portfolioId, fundId, authorization, developmentPolicy?.TradingBook(rows[0].Book) ?? rows[0].Book,
                developmentPolicy?.IsDevelopmentEnvironment == true && string.Equals(rows[0].Book.Environment,"Emulator",StringComparison.Ordinal) ? "Active" : rows[0].State, ct)).ConfigureAwait(false);
        }
        if (domainEvent is IFundRiskTerminalEvent { TerminalRisk: { } terminal })
        {
            var source = await (CapacityReservationStore.ReadEvidenceAsync<IRiskWorkflowTerminalEvent>(db, terminal.SourceCommandId, ct)).ConfigureAwait(false);
            Require(source?.TerminalRisk == terminal && terminal.PortfolioId == portfolioId && terminal.FundId == fundId,
                FinancialReasons.AuthorityDenied, "Exact committed terminal workflow evidence is required.");
            var live = await (db.ScalarAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Select03,
                [portfolioId, terminal.OrderId], ct)).ConfigureAwait(false);
            Require(live is null, FinancialReasons.InvalidLifecycle, "Capacity remains reserved or consumed; terminal Fund synchronization awaits reconciliation.");
        }
        await (db.AppendAsync(stream, domainEvent.CommandId, domainEvent, expectedRevision, ct)).ConfigureAwait(false);
        await ProjectAcceptedAsync(db,domainEvent,ct).ConfigureAwait(false);
        if (rows.Count == 0) return true;
        if (domainEvent is IFundRiskTerminalEvent { TerminalRisk: not null })
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Update01, [portfolioId], ct)).ConfigureAwait(false);
        var current = rows[0];
        if (changesAuthority)
        {
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Update02, [portfolioId, checked(current.Epoch + 1), checked(current.Revision + 1)], ct)).ConfigureAwait(false);
        }
        else if (fundId is { } fund)
        {
            // Composing/accepting an order advances the Fund stream but does not change its mandate.
            // Keep the verified source fence current without silently changing economic authority.
            var book = current.Book with { Funds = current.Book.Funds.Select(x => x.FundId == fund ? x with { FundStreamVersion = checked(expectedRevision + 1) } : x).ToArray() };
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Update03, [portfolioId, Json(book)], ct)).ConfigureAwait(false);
        }
        // Record the accepted state after all effects, including revisions advanced by terminal-risk events.
        var accepted = await (db.QueryAsync(PortfolioDbSql.Financial.PortfolioAuthorityFence.Select02,[portfolioId],
            r => (Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Epoch:r.GetInt64(1),Revision:r.GetInt64(2),State:r.GetString(3)),ct)).ConfigureAwait(false);
        var value=accepted.Single();
        var migration=(string)(await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationProjection.ReadMigrationState,[portfolioId],ct)).ConfigureAwait(false))!;
        var snapshot=new FinancialAuthoritySnapshotEvent { Id=Guid.NewGuid(),CommandId=domainEvent.CommandId,
            Subject=domainEvent.Subject,AggregateId=portfolioId.ToString(System.Globalization.CultureInfo.InvariantCulture),ReceivedOn=DateTime.UtcNow,PortfolioId=portfolioId,
            SourceEventId=domainEvent.Id,Book=value.Book,PreviousFinancialRevision=current.Revision,FinancialRevision=value.Revision,
            AuthorityEpoch=value.Epoch,OperatingState=value.State,MigrationState=migration };
        var authorityStream=$"FinancialAuthoritySnapshot.{portfolioId}";
        var version=Convert.ToInt64(await (db.ScalarAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Select05,[authorityStream],ct)).ConfigureAwait(false) ?? 0L,System.Globalization.CultureInfo.InvariantCulture);
        await (db.AppendAsync(authorityStream,domainEvent.CommandId,snapshot,version,ct)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial.LedgerFinancialProjection.InsertCheckpoint,
            [snapshot.Id,portfolioId,snapshot.EventId,snapshot.FinancialRevision],ct)).ConfigureAwait(false);
        return true;
    }
    static async Task ProjectAcceptedAsync(IEnlistedPostgresTransaction db,IEvent domainEvent,CancellationToken ct)
    {
        var projectionFacts=domainEvent switch {
            TomasAI.IFM.Domain.Portfolio.Shared.Events.IPortfolioDomainEvent portfolio => portfolio.ProjectionFacts,
            TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events.IPortfolioFundDomainEvent fundEvent => fundEvent.ProjectionFacts,
            TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events.IPortfolioFinancialPolicyDomainEvent policy => policy.ProjectionFacts,
            _ => null };
        if (projectionFacts is not null)
            await PostgresPortfolioProjectionStore.ApplyAsync(db,domainEvent,projectionFacts,ct).ConfigureAwait(false);
    }
}
