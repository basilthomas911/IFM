using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Application.Storage.EventSourceDb.Diagnostics;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Existing Portfolio/Fund/policy writers participate in the same financial admission lock.</summary>
public sealed class PortfolioAuthorityFence(IPostgresEventTransaction transactions, FinancialDevelopmentPolicy? developmentPolicy = null) : IPortfolioAuthorityFence
{
    public Task AppendAsync(int portfolioId, int? fundId, string stream, IEvent domainEvent, long expectedRevision, bool changesAuthority, CancellationToken token = default)
        => AppendAsync(portfolioId, new(fundId, stream, domainEvent, expectedRevision, changesAuthority), token);

    /// <inheritdoc />
    public Task AppendAsync(int portfolioId, PortfolioAuthorityAppend operation, CancellationToken token = default)
        => transactions.ExecuteAsync((db, ct) => AppendEnlistedAsync(db, portfolioId, operation, ct), token);

    /// <summary>Commits the supplied related source transitions and projections as one atomic operation.</summary>
    public Task AppendBatchAsync(int portfolioId, IReadOnlyList<PortfolioAuthorityAppend> operations, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Count == 0) throw new ArgumentException("At least one source transition is required.",nameof(operations));
        if (operations.Select(x=>x.Source.CommandId).Distinct().Count()!=1)
            throw new ArgumentException("A compound operation requires one CommandId.",nameof(operations));
        return transactions.ExecuteAsync(async(db,ct) => {
            foreach (var operation in operations)
                await AppendEnlistedAsync(db, portfolioId, operation, ct).ConfigureAwait(false);
            return true;
        },token);
    }

    async Task<bool> AppendEnlistedAsync(EnlistedEventTransaction db, int portfolioId,
        PortfolioAuthorityAppend operation, CancellationToken ct)
    {
        var (fundId, stream, domainEvent, expectedRevision, changesAuthority) = operation;
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.append");
        // Also serializes first-time book creation with configuration writes before an authority row exists.
        using (FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.advisory_lock"))
            await (db.ScalarAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_Select01, [portfolioId], ct)).ConfigureAwait(false);
        if (operation.ExpectedPortfolioRevision is { } expectedPortfolioRevision)
        {
            var portfolioRevision = await db.ScalarAsync(PortfolioDbSql.Financial_PortfolioDbFinancialSupport_Select04,
                [$"Portfolio.{portfolioId}"], ct).ConfigureAwait(false);
            Require(portfolioRevision is long currentRevision && currentRevision == expectedPortfolioRevision,
                FinancialReasons.RevisionConflict, "Portfolio changed since the Fund command was prepared.");
            if (operation.ExpectedPortfolioVersion is { } expectedVersion
                && operation.ExpectedPortfolioOperatingState is { } expectedState)
            {
                var matches = await db.ScalarAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_MatchPreparedPortfolio,
                    [portfolioId, expectedPortfolioRevision, expectedVersion, expectedState.ToString()], ct).ConfigureAwait(false);
                Require(matches is true, FinancialReasons.AuthorityRevoked,
                    "Prepared Portfolio version or operating state disagrees with the committed Portfolio.");
            }
        }
        if (operation.PreparedIdentifiers is { } identifiers)
            await PortfolioPreparedIdentifierFence.VerifyAsync(db, identifiers, ct).ConfigureAwait(false);
        if (operation.RequiredUnreferencedPolicyId is { } policyId)
        {
            var unreferenced = await db.ScalarAsync(PortfolioDbSql.Financial_CheckPolicyUnreferenced,
                [portfolioId, $"Portfolio.{portfolioId}", policyId], ct).ConfigureAwait(false);
            Require(unreferenced is true, FinancialReasons.AuthorityDenied,
                "The current Portfolio references this policy or its projection is behind the committed revision.");
        }
        if (operation.DraftPortfolioFundIds is { } fundIds)
        {
            var canDelete = await db.ScalarAsync(PortfolioDbSql.Financial_CheckDraftPortfolioDeletion,
                [portfolioId, fundIds.ToArray()], ct).ConfigureAwait(false);
            Require(canDelete is true, FinancialReasons.AuthorityDenied,
                "A Draft Portfolio with composition history or stale Fund projections cannot be deleted.");
        }
        using var lockTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.portfolio_fence.lock_authority");
        var rows = await (db.QueryAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_Select02, [portfolioId], r => (Book: Decode<FinancialBookConfiguration>(r.GetString(0)), Epoch: r.GetInt64(1), Revision: r.GetInt64(2), State: r.GetString(3)), ct)).ConfigureAwait(false);
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
            var live = await (db.ScalarAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_Select03,
                [portfolioId, terminal.OrderId], ct)).ConfigureAwait(false);
            Require(live is null, FinancialReasons.InvalidLifecycle, "Capacity remains reserved or consumed; terminal Fund synchronization awaits reconciliation.");
        }
        await (db.AppendAsync(new EnlistedEventAppend(stream, domainEvent.CommandId, domainEvent, expectedRevision), ct)).ConfigureAwait(false);
        if (operation.Checkpoint is { } checkpoint)
            await db.AppendAggregateCheckpointAsync(stream, $"{stream}.Snapshot", checked(expectedRevision + 1), checkpoint, ct)
                .ConfigureAwait(false);
        await ProjectAcceptedAsync(db,domainEvent,ct).ConfigureAwait(false);
        if (rows.Count == 0) return true;
        if (domainEvent is IFundRiskTerminalEvent { TerminalRisk: not null })
            await (db.ExecuteAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_Update01, [portfolioId], ct)).ConfigureAwait(false);
        var current = rows[0];
        if (changesAuthority)
        {
            await (db.ExecuteAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_Update02, [portfolioId, checked(current.Epoch + 1), checked(current.Revision + 1)], ct)).ConfigureAwait(false);
        }
        else if (fundId is { } fund)
        {
            // Composing/accepting an order advances the Fund stream but does not change its mandate.
            // Keep the verified source fence current without silently changing economic authority.
            var book = current.Book with { Funds = current.Book.Funds.Select(x => x.FundId == fund ? x with { FundStreamVersion = checked(expectedRevision + 1) } : x).ToArray() };
            await (db.ExecuteAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_Update03, [portfolioId, Json(book)], ct)).ConfigureAwait(false);
        }
        // Record the accepted state after all effects, including revisions advanced by terminal-risk events.
        var accepted = await (db.QueryAsync(PortfolioDbSql.Financial_PortfolioAuthorityFence_Select02,[portfolioId],
            r => (Book:Decode<FinancialBookConfiguration>(r.GetString(0)),Epoch:r.GetInt64(1),Revision:r.GetInt64(2),State:r.GetString(3)),ct)).ConfigureAwait(false);
        var value=accepted.Single();
        var migration=(string)(await (db.ScalarAsync(PortfolioDbSql.Financial_LedgerConfigurationProjection_ReadMigrationState,[portfolioId],ct)).ConfigureAwait(false))!;
        var snapshot=new FinancialAuthoritySnapshotEvent { Id=Guid.NewGuid(),CommandId=domainEvent.CommandId,
            Subject=domainEvent.Subject,AggregateId=portfolioId.ToString(System.Globalization.CultureInfo.InvariantCulture),ReceivedOn=db.BusinessTime.GetUtcNow().UtcDateTime,PortfolioId=portfolioId,
            SourceEventId=domainEvent.Id,Book=value.Book,PreviousFinancialRevision=current.Revision,FinancialRevision=value.Revision,
            AuthorityEpoch=value.Epoch,OperatingState=value.State,MigrationState=migration };
        var authorityStream=$"FinancialAuthoritySnapshot.{portfolioId}";
        var version=Convert.ToInt64(await (db.ScalarAsync(PortfolioDbSql.Financial_PortfolioDbFinancialSupport_Select05,[authorityStream],ct)).ConfigureAwait(false) ?? 0L,System.Globalization.CultureInfo.InvariantCulture);
        await (db.AppendAsync(new EnlistedEventAppend(authorityStream,domainEvent.CommandId,snapshot,version),ct)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial_LedgerFinancialProjection_InsertCheckpoint,
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
            await AcceptedPortfolioProjection.PersistAcceptedPortfolioAsync(db,domainEvent,projectionFacts,ct).ConfigureAwait(false);
        else
            await PortfolioDbContextExtensions.ProjectFundRiskAuthorizationAsync(db, domainEvent, ct).ConfigureAwait(false);
    }
}
