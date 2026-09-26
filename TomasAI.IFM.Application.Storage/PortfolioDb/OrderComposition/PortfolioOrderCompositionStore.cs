using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Application.Storage.PortfolioDb.OrderComposition;

public sealed class PortfolioOrderCompositionStore(IPostgresEventTransaction transactions)
{
    public Task<PortfolioOrderCompositionCompletedEvent> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request,
        Func<EvaluatePortfolioOrderCompositionCommand,FinancialBookConfiguration,long,IReadOnlyList<PortfolioFundFinancialSnapshot>,CancellationToken,ValueTask<PortfolioOrderCompositionReceipt>> evaluate,
        CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync(async (db, token) =>
        {
            var duplicate = await PortfolioDbFinancialSupport.ReadOperationAsync<PortfolioOrderCompositionCompletedEvent>(
                db, request.PortfolioId, request.OperationId, request.InputSha256, token).ConfigureAwait(false);
            if (duplicate is not null) return duplicate;

            var authority = await PortfolioDbFinancialSupport.LockAuthorityAsync(
                db, request.PortfolioId,
                request.ExpectedFinancialRevision > 0 ? request.ExpectedFinancialRevision : null,
                token).ConfigureAwait(false);
            var now = DateTime.UtcNow;
            if (request.ExpiresAtUtc <= now) throw new TimeoutException("Portfolio order composition expired before evaluation.");
            var requestedLimits = authority.Book.Funds.SelectMany(fund => fund.Limits.Concat(
                    fund.Deployments.Where(deployment => deployment.Reference.DeploymentKey == request.Body.DeploymentKey)
                        .SelectMany(deployment => deployment.Limits)))
                .Select(limit => new { scope_kind=(int)limit.ScopeKind,scope_key=limit.ScopeKey,
                    measure=(int)limit.Measure,unit=(int)limit.Unit })
                .Distinct().ToArray();
            var usage = requestedLimits.Length == 0 ? [] : await db.QueryAsync(
                PortfolioDbSql.Financial.CapacityReservationStore.Select01,
                [request.PortfolioId,PortfolioDbFinancialSupport.Json(requestedLimits)],
                reader => new CapacityUsed((CapacityScopeKind)reader.GetInt32(0),reader.GetString(1),
                    (CapacityMeasure)reader.GetInt32(2),(CapacityUnit)reader.GetInt32(3),reader.GetDecimal(4),reader.GetDecimal(5),reader.GetDecimal(6)),token)
                .ConfigureAwait(false);
            var snapshots = new List<PortfolioFundFinancialSnapshot>(authority.Book.Funds.Length);
            foreach (var fund in authority.Book.Funds)
            {
                if (fund.CanSpend)
                    await PortfolioDbFinancialSupport.ValidateFundSourcesAsync(db,authority.Book,fund.FundId,true,token).ConfigureAwait(false);
                snapshots.Add(new(fund.FundId,
                    await GeneralLedgerStore.AvailableCash(db,authority.Book.BookId,fund.FundId,token).ConfigureAwait(false),
                    [.. usage]));
            }
            var receipt = await evaluate(
                request, authority.Book, checked(authority.Revision + 1), snapshots, token).ConfigureAwait(false);
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
            foreach (var effect in receipt.CapacityEffects)
                foreach (var exposure in effect.Exposures)
                {
                    await db.ExecuteAsync(PortfolioDbSql.OrderComposition.InsertCapacity,
                        [effect.OrderId,effect.PortfolioId,effect.FundId,(int)exposure.ScopeKind,exposure.ScopeKey,
                            (int)exposure.Measure,(int)exposure.Unit,exposure.Amount,exposure.MethodVersion,receipt.FinancialRevision],token).ConfigureAwait(false);
                    await db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Insert03,
                        [effect.PortfolioId,(int)exposure.ScopeKind,exposure.ScopeKey,(int)exposure.Measure,(int)exposure.Unit,
                            0m,Math.Abs(exposure.Amount),0m,receipt.FinancialRevision],token).ConfigureAwait(false);
                }
            await PortfolioDbFinancialSupport.SaveOutcomeAsync(db,request,completed,receipt.FinancialRevision,true,token).ConfigureAwait(false);
            return completed;
        }, cancellationToken);
}
