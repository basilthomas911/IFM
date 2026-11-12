
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Repository;

/// <summary>Atomically accepts a reduce-only order and its canonical completed event.</summary>
public sealed class PortfolioCloseOrderCompositionStore(IPostgresEventTransaction transactions, FinancialDevelopmentPolicy? developmentPolicy = null, TimeProvider? timeProvider = null)
{
    readonly TimeProvider businessClock = timeProvider ?? TimeProvider.System;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task<PortfolioCloseOrderCompositionCompletedEvent> EvaluateAsync(
        EvaluatePortfolioCloseOrderCompositionCommand request,
        Func<EvaluatePortfolioCloseOrderCompositionCommand, FinancialBookConfiguration, long,
            PortfolioExecutionOrderInstruction, CancellationToken, ValueTask<PortfolioCloseOrderCompositionReceipt>> evaluate,
        CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync(async (db, token) =>
        {
            var duplicate = await FinancialRepositorySupport
                .ReadOperationAsync<PortfolioCloseOrderCompositionCompletedEvent>(
                    db, request.PortfolioId, request.OperationId, request.InputSha256, token)
                .ConfigureAwait(false);
            if (duplicate is not null)
                return duplicate;

            var authority = await FinancialRepositorySupport.LockAuthorityAsync(
                db, request.PortfolioId,
                request.ExpectedFinancialRevision > 0 ? request.ExpectedFinancialRevision : null,
                token, developmentPolicy).ConfigureAwait(false);
            var now = businessClock.GetUtcNow().UtcDateTime;
            if (request.ExpiresAtUtc <= now)
                throw new TimeoutException("Portfolio close-order composition expired before evaluation.");

            var positionId = request.Body.Position.Id;
            await FinancialRepositorySupport.ValidateFundSourcesAsync(
                db, authority.Book, positionId.FundId, false, token).ConfigureAwait(false);

            var priorClose = await db.ScalarAsync(
                PortfolioDbSql.OrderComposition_SelectAcceptedClose,
                [positionId.Format()], token).ConfigureAwait(false);
            if (priorClose is int priorOrder)
                await RequireSettledPartialCloseAsync(db, request.Body.Position, priorOrder, token).ConfigureAwait(false);

            var openingJson = await db.ScalarAsync(
                PortfolioDbSql.OrderComposition_SelectOpeningOrder,
                [positionId.OrderId, positionId.PortfolioId, positionId.FundId], token)
                .ConfigureAwait(false) as string
                ?? throw new InvalidOperationException("The accepted opening Trade Order was not found.");
            var openingTradeOrder = FinancialRepositorySupport.Decode<TradeOrderDefinition>(openingJson);
            if (openingTradeOrder.SchemaVersion <= 2 && openingTradeOrder.PositionType == TradeOrderPositionType.Unknown)
                openingTradeOrder = openingTradeOrder with { PositionType = TradeOrderPositionType.Opening };
            var openingOrder = openingTradeOrder.ToPortfolioInstruction();

            var receipt = await evaluate(
                request, authority.Book, checked(authority.Revision + 1), openingOrder, token)
                .ConfigureAwait(false);
            _ = receipt.TradeOrder
                ?? throw new InvalidOperationException("An accepted close decision requires a Trade Order.");
            var completed = new PortfolioCloseOrderCompositionCompletedEvent
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
                Receipt = receipt,
                SchemaVersion = 2,
                ProjectionFacts = new(request.Subject.Name,positionId.Format(),positionId.OrderId,positionId.TradeId,positionId.FundId)
            };

            await (ProjectMaterialAsync(db,completed,token)).ConfigureAwait(false);
            await FinancialRepositorySupport.SaveOutcomeAsync(
                db, request, completed, receipt.FinancialRevision, true, token).ConfigureAwait(false);
            await (LedgerFinancialProjection.RecordAppliedAsync(db,completed,receipt.FinancialRevision,token)).ConfigureAwait(false);
            return completed;
        }, cancellationToken);

    /// <summary>Requires terminal execution and its applied fills before accepting a new close.</summary>
    private static async Task RequireSettledPartialCloseAsync(EnlistedEventTransaction db,
        PortfolioPositionSnapshot requested, int priorOrder, CancellationToken token)
    {
        var id = requested.Id;
        var target = new StrategyPositionId(new TradeEntityId(id.PortfolioId, id.FundId, id.OrderId, id.TradeId), id.PositionId);
        var priorPayload = await db.ScalarAsync(PortfolioDbSql.OrderComposition_SelectPriorCloseExecution,
            [id.PortfolioId, id.FundId, priorOrder], token).ConfigureAwait(false);
        var prior = priorPayload is byte[] bytes
            ? MessagePackBinarySerializer.Shared.Deserialize<OrderExecutionDefinition>(bytes)
            : null;
        var unfilledTerminal = prior is not null
            && prior.Status is OrderExecutionStatus.Rejected or OrderExecutionStatus.Cancelled
            && prior.CumulativeFilledQuantity == 0 && prior.Fills.Length == 0;
        var cancelledPartial = prior is not null && prior.Status == OrderExecutionStatus.Cancelled
            && prior.CumulativeFilledQuantity > 0 && prior.CumulativeFilledQuantity < prior.OrderQuantity
            && prior.Fills.Length > 0;
        if (prior is null || prior.TargetPositionId != target || (!unfilledTerminal && !cancelledPartial))
            throw new InvalidOperationException("The previous close must be terminal with zero fills or a settled partial cancellation.");

        var positionPayload = await db.ScalarAsync(PortfolioDbSql.OrderComposition_SelectCurrentClosePosition,
            [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, id.PositionId], token).ConfigureAwait(false);
        var current = positionPayload is byte[] positionBytes
            ? MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(positionBytes)
            : null;
        if (current is null || current.Id != target || !current.IsOpen ||
            prior.Fills.Any(fill => !current.ClosingFills.Any(applied => applied.ExecutionFillId == fill.ExecutionFillId)) ||
            current.Legs.Length != requested.Legs.Length ||
            current.Legs.Any(leg => !requested.Legs.Any(candidate => candidate.TradeLegId == leg.TradeLegId &&
                string.Equals(candidate.ContractId, leg.ContractId, StringComparison.Ordinal) &&
                candidate.SignedQuantity == leg.SignedQuantity)))
            throw new InvalidOperationException("The previous close fills must be applied to the current remaining position.");
    }

    /// <summary>Restores the recorded reduce-only order and exact close claim without invoking execution.</summary>
    internal static async Task ProjectMaterialAsync(EnlistedEventTransaction db,PortfolioCloseOrderCompositionCompletedEvent completed,CancellationToken token)
    {
        var receipt=completed.Receipt;
        var facts=completed.ProjectionFacts ?? throw new InvalidDataException("Close source lacks accepted target-position facts.");
        var order=receipt.TradeOrder ?? throw new InvalidDataException("Accepted close source lacks its order.");
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition_InsertDecision,
                [completed.OperationId, receipt.CompositionId, receipt.WorkflowId.ExitDecisionId,
                    completed.PortfolioId, (short)receipt.Status, receipt.FinancialRevision,
                    completed.InputHash, FinancialRepositorySupport.Json(receipt), completed.CommittedAtUtc], token)
                .ConfigureAwait(false);
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition_InsertFundDecision,
                [completed.OperationId, facts.TargetFundId, true, receipt.ReasonCode, order.Id.OrderId], token)
                .ConfigureAwait(false);
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition_InsertOrder,
                [order.Id.OrderId, completed.OperationId, order.Id.PortfolioId, order.Id.FundId,
                    (short)order.Status, order.DefinitionHash, FinancialRepositorySupport.Json(order),
                    order.ValidUntilUtc], token).ConfigureAwait(false);
            var ordinal = 0;
            foreach (var component in order.Components)
                foreach (var leg in component.Legs)
                    await db.ExecuteAsync(PortfolioDbSql.OrderComposition_InsertLeg,
                        [order.Id.OrderId, component.ComponentId, leg.TradeLegId, ordinal++,
                            FinancialRepositorySupport.Json(leg)], token).ConfigureAwait(false);
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition_InsertAcceptedClose,
                [completed.OperationId, facts.TargetPositionId, facts.TargetOrderId,
                    facts.TargetTradeId, order.Id.OrderId, completed.CommittedAtUtc], token).ConfigureAwait(false);
    }
}
