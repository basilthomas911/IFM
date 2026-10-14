
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository;

public sealed class PortfolioOrderCompositionStore(IPostgresEventTransaction transactions, FinancialDevelopmentPolicy? developmentPolicy = null)
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task<PortfolioOrderCompositionCompletedEvent> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request,
        Func<EvaluatePortfolioOrderCompositionCommand, FinancialBookConfiguration, long, IReadOnlyList<PortfolioFundFinancialSnapshot>, CancellationToken, ValueTask<PortfolioOrderCompositionReceipt>> evaluate,
        CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync(async (db, token) =>
        {
            var duplicate = await FinancialRepositorySupport.ReadOperationAsync<PortfolioOrderCompositionCompletedEvent>(
                db, request.PortfolioId, request.OperationId, request.InputSha256, token).ConfigureAwait(false);
            if (duplicate is not null) return duplicate;

            var authority = await FinancialRepositorySupport.LockAuthorityAsync(
                db, request.PortfolioId,
                request.ExpectedFinancialRevision > 0 ? request.ExpectedFinancialRevision : null,
                token, developmentPolicy).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (request.ExpiresAtUtc <= now) throw new TimeoutException("Portfolio order composition expired before evaluation.");
            var requestedLimits = authority.Book.Funds.SelectMany(fund => fund.Limits.Concat(
                    fund.Deployments.Where(deployment => deployment.Reference.DeploymentKey == request.Body.DeploymentKey)
                        .SelectMany(deployment => deployment.Limits)))
                .Select(limit => new
                {
                    scope_kind = (int)limit.ScopeKind,
                    scope_key = limit.ScopeKey,
                    measure = (int)limit.Measure,
                    unit = (int)limit.Unit
                })
                .Distinct().ToArray();
            var usage = requestedLimits.Length == 0 ? [] : await db.QueryAsync(
                PortfolioDbSql.Financial.CapacityReservationStore.Select01,
                [request.PortfolioId, FinancialRepositorySupport.Json(requestedLimits)],
                reader => new CapacityUsed((CapacityScopeKind)reader.GetInt32(0), reader.GetString(1),
                    (CapacityMeasure)reader.GetInt32(2), (CapacityUnit)reader.GetInt32(3), reader.GetDecimal(4), reader.GetDecimal(5), reader.GetDecimal(6)), token)
                .ConfigureAwait(false);
            var snapshots = new List<PortfolioFundFinancialSnapshot>(authority.Book.Funds.Length);
            foreach (var fund in authority.Book.Funds)
            {
                if (fund.CanSpend)
                    await FinancialRepositorySupport.ValidateFundSourcesAsync(db, authority.Book, fund.FundId, true, token).ConfigureAwait(false);
                snapshots.Add(new(fund.FundId,
                    await GeneralLedgerStore.AvailableCash(db, authority.Book.BookId, fund.FundId, token).ConfigureAwait(false),
                    [.. usage]));
            }
            var receipt = await evaluate(
                request, authority.Book, checked(authority.Revision + 1), snapshots, token).ConfigureAwait(false);
            var completed = new PortfolioOrderCompositionCompletedEvent
            {
                Id = Guid.NewGuid(),
                Subject = request.Subject,
                EntityId = request.EntityId,
                CommandId = request.CommandId,
                OperationId = request.OperationId,
                PortfolioId = request.PortfolioId,
                CorrelationId = request.CorrelationId,
                CausationId = request.CausationId,
                CommittedAtUtc = now,
                ReceivedOn = now,
                InputHash = request.InputSha256,
                AggregateId = request.EntityId.Format(),
                Receipt = receipt
            };

            await (ProjectMaterialAsync(db,completed,token)).ConfigureAwait(false);
            await FinancialRepositorySupport.SaveOutcomeAsync(db, request, completed, receipt.FinancialRevision, true, token).ConfigureAwait(false);
            await (LedgerFinancialProjection.RecordAppliedAsync(db,completed,receipt.FinancialRevision,token)).ConfigureAwait(false);
            return completed;
        }, cancellationToken);

    /// <summary>Materializes the accepted decision, orders, legs and capacity effects. No price/risk/admission calculation.</summary>
    internal static async Task ProjectMaterialAsync(EnlistedEventTransaction db,PortfolioOrderCompositionCompletedEvent completed,CancellationToken token)
    {
        var receipt=completed.Receipt;
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertDecision,
                [completed.OperationId,receipt.CompositionId,receipt.WorkflowId,completed.PortfolioId,(short)receipt.Status,
                    receipt.FinancialRevision,completed.InputHash,FinancialRepositorySupport.Json(receipt),completed.CommittedAtUtc], token).ConfigureAwait(false);
            foreach (var decision in receipt.FundDecisions)
                await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertFundDecision,
                    [completed.OperationId, decision.FundId, decision.Accepted, decision.ReasonCode, decision.OrderId], token).ConfigureAwait(false);
            foreach (var order in receipt.TradeOrders)
            {
                await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertOrder,
                    [order.Id.OrderId,completed.OperationId,order.Id.PortfolioId,order.Id.FundId,(short)order.Status,
                        order.DefinitionHash,FinancialRepositorySupport.Json(order),order.ValidUntilUtc], token).ConfigureAwait(false);
                var ordinal = 0;
                foreach (var component in order.Components)
                    foreach (var leg in component.Legs)
                        await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertLeg,
                            [order.Id.OrderId, component.ComponentId, leg.TradeLegId, ordinal++, FinancialRepositorySupport.Json(leg)], token).ConfigureAwait(false);
            }
            foreach (var effect in receipt.CapacityEffects)
                foreach (var exposure in effect.Exposures)
                {
                    await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertCapacity,
                        [effect.OrderId,effect.PortfolioId,effect.FundId,(int)exposure.ScopeKind,exposure.ScopeKey,
                            (int)exposure.Measure,(int)exposure.Unit,exposure.Amount,exposure.MethodVersion,receipt.FinancialRevision], token).ConfigureAwait(false);
                    await db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Insert03,
                        [effect.PortfolioId,(int)exposure.ScopeKind,exposure.ScopeKey,(int)exposure.Measure,(int)exposure.Unit,
                            0m,Math.Abs(exposure.Amount),0m,receipt.FinancialRevision], token).ConfigureAwait(false);
                }
    }
}
