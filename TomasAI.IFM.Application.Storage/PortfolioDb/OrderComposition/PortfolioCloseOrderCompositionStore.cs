using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;

namespace TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition;

/// <summary>Atomically accepts one reduce-only order and its canonical completed event.</summary>
public sealed class PortfolioCloseOrderCompositionStore(IPostgresEventTransaction transactions)
{
    public Task<PortfolioCloseOrderCompositionCompletedEvent> EvaluateAsync(
        EvaluatePortfolioCloseOrderCompositionCommand request,
        Func<EvaluatePortfolioCloseOrderCompositionCommand, FinancialBookConfiguration, long,
            PortfolioExecutionOrderInstruction, CancellationToken, ValueTask<PortfolioCloseOrderCompositionReceipt>> evaluate,
        CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync(async (db, token) =>
        {
            var duplicate = await PortfolioDbFinancialSupport
                .ReadOperationAsync<PortfolioCloseOrderCompositionCompletedEvent>(
                    db, request.PortfolioId, request.OperationId, request.InputSha256, token)
                .ConfigureAwait(false);
            if (duplicate is not null)
                return duplicate;

            var authority = await PortfolioDbFinancialSupport.LockAuthorityAsync(
                db, request.PortfolioId,
                request.ExpectedFinancialRevision > 0 ? request.ExpectedFinancialRevision : null,
                token).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (request.ExpiresAtUtc <= now)
                throw new TimeoutException("Portfolio close-order composition expired before evaluation.");

            var positionId = request.Body.Position.Id;
            await PortfolioDbFinancialSupport.ValidateFundSourcesAsync(
                db, authority.Book, positionId.FundId, false, token).ConfigureAwait(false);

            var priorClose = await db.ScalarAsync(
                PortfolioDbSql.OrderComposition.SelectAcceptedClose,
                [positionId.Format()], token).ConfigureAwait(false);
            if (priorClose is Guid priorOperation && priorOperation != request.OperationId)
                throw new InvalidOperationException("A close Trade Order has already been accepted for this position.");

            var openingJson = await db.ScalarAsync(
                PortfolioDbSql.OrderComposition.SelectOpeningOrder,
                [positionId.OrderId, positionId.PortfolioId, positionId.FundId], token)
                .ConfigureAwait(false) as string
                ?? throw new InvalidOperationException("The accepted opening Trade Order was not found.");
            var openingTradeOrder = PortfolioDbFinancialSupport.Decode<TradeOrderDefinition>(openingJson);
            if (openingTradeOrder.SchemaVersion <= 2 && openingTradeOrder.PositionType == TradeOrderPositionType.Unknown)
                openingTradeOrder = openingTradeOrder with { PositionType = TradeOrderPositionType.Opening };
            var openingOrder = openingTradeOrder.ToPortfolioInstruction();

            var receipt = await evaluate(
                request, authority.Book, checked(authority.Revision + 1), openingOrder, token)
                .ConfigureAwait(false);
            var order = receipt.TradeOrder
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
                Receipt = receipt
            };

            await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertDecision,
                [request.OperationId, receipt.CompositionId, receipt.WorkflowId.ExitDecisionId,
                    request.PortfolioId, (short)receipt.Status, receipt.FinancialRevision,
                    request.InputSha256, PortfolioDbFinancialSupport.Json(receipt), now], token)
                .ConfigureAwait(false);
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertFundDecision,
                [request.OperationId, positionId.FundId, true, receipt.ReasonCode, order.Id.OrderId], token)
                .ConfigureAwait(false);
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertOrder,
                [order.Id.OrderId, request.OperationId, order.Id.PortfolioId, order.Id.FundId,
                    (short)order.Status, order.DefinitionHash, PortfolioDbFinancialSupport.Json(order),
                    order.ValidUntilUtc], token).ConfigureAwait(false);
            var ordinal = 0;
            foreach (var component in order.Components)
                foreach (var leg in component.Legs)
                    await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertLeg,
                        [order.Id.OrderId, component.ComponentId, leg.TradeLegId, ordinal++,
                            PortfolioDbFinancialSupport.Json(leg)], token).ConfigureAwait(false);
            await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertAcceptedClose,
                [request.OperationId, positionId.Format(), positionId.OrderId,
                    positionId.TradeId, order.Id.OrderId, now], token).ConfigureAwait(false);
            await PortfolioDbFinancialSupport.SaveOutcomeAsync(
                db, request, completed, receipt.FinancialRevision, true, token).ConfigureAwait(false);
            return completed;
        }, cancellationToken);
}
