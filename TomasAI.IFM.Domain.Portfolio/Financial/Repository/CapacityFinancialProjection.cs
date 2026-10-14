
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;
namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Reconstructs recorded capacity decisions and usage without risk reassessment or dispatch.</summary>
public sealed class CapacityFinancialProjection(IPostgresEventTransaction transactions)
{
    public Task ReplayAsync(IFinancialCompletedEvent source,CancellationToken token=default)
    {
        var revision=source switch
        {
            CapacityReservationCompletedEvent value=>value.Receipt.FinancialRevision,
            CapacityConsumptionCompletedEvent value=>value.Receipt.FinancialRevision,
            CapacityLifecycleCompletedEvent value=>value.Receipt.FinancialRevision,
            _=>throw new InvalidDataException("Unsupported capacity source event.")
        };
        if(source.EventId<=0 || source.Id==Guid.Empty) throw new InvalidDataException("Committed capacity source required.");
        return transactions.ExecuteAsync(async (db,ct)=>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01,[source.PortfolioId],ct)).ConfigureAwait(false);
            if(!await (LedgerFinancialProjection.RecordAppliedAsync(db,source,revision,ct)).ConfigureAwait(false)) return false;
            var authority=await (LockAuthorityAsync(db,source.PortfolioId,null,ct)).ConfigureAwait(false);
            if(revision!=authority.Revision+1) throw new InvalidDataException("Financial capacity events must be rebuilt in revision order.");
            string actor;
            AcceptedCapacityUsage[] usage;
            if(source is CapacityReservationCompletedEvent reserved)
            {
                var facts=reserved.ProjectionFacts;
                if(facts is not {Version:1}) throw new InvalidDataException("Accepted reservation facts are missing.");
                var body=facts.Request;var receipt=reserved.Receipt; actor=facts.ActorName;usage=facts.Usage;
                await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Insert01,
                    [body.ReservationId,source.PortfolioId,source.OperationId,body.FundId,body.BookId,body.OrderId,
                     body.UnitCandidateHash,body.RiskAssessmentHash,body.SizedOrderHash,body.StrategyUnits,Json(body.Requirements),
                     body.ExecutionEnvironment,body.ValidUntilUtc,receipt.CompletedEventId,LedgerFinancialProjection.PayloadJson(body),LedgerFinancialProjection.PayloadJson(receipt),source.InputHash],ct)).ConfigureAwait(false);
            }
            else
            {
                var (facts,receipt)=source switch
                {
                    CapacityConsumptionCompletedEvent value=>(value.ProjectionFacts,value.Receipt),
                    CapacityLifecycleCompletedEvent value=>(value.ProjectionFacts,value.Receipt),
                    _=>throw new InvalidDataException("Unsupported lifecycle source.")
                };
                if(facts is not {Version:1}) throw new InvalidDataException("Accepted lifecycle facts are missing.");
                var body=facts.Request;var next=facts.Transition; actor=facts.ActorName;usage=facts.Usage;
                Require(await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Update01,
                    [body.ReservationId,source.PortfolioId,(int)next.Status,receipt.ReservationVersion,
                     body.ExecutionId==Guid.Empty?null:body.ExecutionId,next.FilledUnits,next.CancelledUnits,next.RemainingUnits,next.ClosedUnits],ct)).ConfigureAwait(false)==1,
                    FinancialReasons.RevisionConflict,"Required preceding reservation fact is missing.");
                await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Insert02,
                    [body.ReservationId,receipt.ReservationVersion,$"{body.Source.System}:{body.Source.SourceEventId:N}",source.OperationId,
                     (int)facts.PreviousStatus,(int)next.Status,Json(next),next.FilledUnits,next.CancelledUnits,next.RemainingUnits,
                     body.ExecutionId==Guid.Empty?null:body.ExecutionId,body.ExecutionRevision,source.CommittedAtUtc,receipt.CompletedEventId,source.InputHash,LedgerFinancialProjection.PayloadJson(receipt)],ct)).ConfigureAwait(false);
            }
            foreach(var delta in usage) await (CapacityReservationStore.ApplyUsageAsync(db,source.PortfolioId,delta,revision,ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert06,
                [source.PortfolioId,source.OperationId,actor,source.InputHash,source.EventId,revision],ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01,[source.PortfolioId,revision],ct)).ConfigureAwait(false);
            return true;
        },token);
    }
}
