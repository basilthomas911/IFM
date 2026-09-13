using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition;

public sealed class PortfolioOrderCompositionStore(IPostgresEventTransaction transactions)
{
    public Task<PortfolioOrderCompositionCompletedEvent> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request,
        Func<EvaluatePortfolioOrderCompositionCommand,FinancialBookConfiguration,long,CancellationToken,ValueTask<PortfolioOrderCompositionReceipt>> evaluate,
        CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync(async (db, token) =>
        {
            var duplicate = await PortfolioDbFinancialSupport.ReadOperationAsync<PortfolioOrderCompositionCompletedEvent>(
                db, request.PortfolioId, request.OperationId, request.InputSha256, token).ConfigureAwait(false);
            if (duplicate is not null) return duplicate;

            var authority = await PortfolioDbFinancialSupport.LockAuthorityAsync(
                db, request.PortfolioId, request.ExpectedFinancialRevision, token).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (request.ExpiresAtUtc <= now) throw new TimeoutException("Portfolio order composition expired before evaluation.");
            var receipt = await evaluate(
                request, authority.Book, checked(authority.Revision + 1), token).ConfigureAwait(false);
            var completed = new PortfolioOrderCompositionCompletedEvent
            {
                Id=Guid.NewGuid(), Subject=request.Subject, EntityId=request.EntityId, CommandId=request.CommandId,
                OperationId=request.OperationId, PortfolioId=request.PortfolioId, CorrelationId=request.CorrelationId,
                CausationId=request.CausationId, CommittedAtUtc=now, ReceivedOn=now, InputHash=request.InputSha256,
                AggregateId=request.EntityId.Format(), Receipt=receipt
            };

            await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertDecision,
                [request.OperationId,receipt.CompositionId,receipt.WorkflowId,request.PortfolioId,(short)receipt.Status,
                    receipt.FinancialRevision,request.InputSha256,PortfolioDbFinancialSupport.Json(receipt),now],token).ConfigureAwait(false);
            foreach (var decision in receipt.FundDecisions)
                await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertFundDecision,
                    [request.OperationId,decision.FundId,decision.Accepted,decision.ReasonCode,decision.OrderId],token).ConfigureAwait(false);
            foreach (var order in receipt.TradeOrders)
            {
                await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertOrder,
                    [order.Id.OrderId,request.OperationId,order.Id.PortfolioId,order.Id.FundId,(short)order.Status,
                        order.DefinitionHash,PortfolioDbFinancialSupport.Json(order),order.ValidUntilUtc],token).ConfigureAwait(false);
                var ordinal=0;
                foreach(var component in order.Components)
                    foreach(var leg in component.Legs)
                        await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertLeg,
                            [order.Id.OrderId,component.ComponentId,leg.TradeLegId,ordinal++,PortfolioDbFinancialSupport.Json(leg)],token).ConfigureAwait(false);
            }
            await PortfolioDbFinancialSupport.SaveOutcomeAsync(db,request,completed,receipt.FinancialRevision,true,token).ConfigureAwait(false);
            return completed;
        }, cancellationToken);
}
