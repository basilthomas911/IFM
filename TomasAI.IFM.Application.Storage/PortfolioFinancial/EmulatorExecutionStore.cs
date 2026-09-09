using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioFinancial.PortfolioFinancialDbContext;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Durable internal emulator admission. Same-identity retries observe one broker order even after acknowledgement loss.</summary>
public sealed class EmulatorExecutionStore(IPostgresEventTransaction transactions)
{
    public Task<EmulatorOrderSubmittedEvent> SubmitAsync(SubmitEmulatorOrderCommand request,CancellationToken token=default)
        =>transactions.ExecuteAsync(async(db,ct)=>
    {
        var authority=await LockAuthorityAsync(db,request.PortfolioId,null,ct);
        var replay=await ReadOperationAsync<EmulatorOrderSubmittedEvent>(db,request.PortfolioId,request.OperationId,request.InputSha256,ct);
        if(replay is not null) return replay;
        var order=request.Body.Order; var now=DateTime.UtcNow;
        Require(request.InputSha256==FinancialCanonicalHash.Request(request) && order.ContentHash==order.Hash() &&
            order.PortfolioId==request.PortfolioId && order.Environment=="Emulator" && authority.Book.Environment=="Emulator" &&
            authority.State=="Active" && now<order.ValidUntilUtc && now<request.ExpiresAtUtc,
            FinancialReasons.AuthorityDenied,"Current exact emulator order and financial authority are required.");
        await ValidateFundSourcesAsync(db,authority.Book,order.FundId,true,ct);
        var existing=await db.ScalarAsync("SELECT operation_id FROM portfolio_financial.emulator_order WHERE execution_id=$1;",[order.ExecutionId],ct);
        if(existing is Guid prior) throw new FinancialOperationException(FinancialReasons.SourceConflict,
            "This execution already has an emulator order; reconcile its original operation.",FinancialCommitDisposition.NoNewMutation,prior);
        var rows=await db.QueryAsync("""
            SELECT status,version,execution_id,request::text FROM portfolio_financial.capacity_reservation
            WHERE portfolio_id=$1 AND reservation_id=$2;
            """,[request.PortfolioId,order.ReservationId],r=>(Status:(ReservationStatus)r.GetInt32(0),Version:r.GetInt64(1),
                Execution:r.IsDBNull(2)?Guid.Empty:r.GetGuid(2),Request:Decode<CapacityReservationRequest>(r.GetString(3))),ct);
        Require(rows.Count==1,FinancialReasons.AuthorityDenied,"Consumed reservation is required.");
        var row=rows[0]; var original=row.Request;
        var fund=authority.Book.Funds.Single(x=>x.FundId==order.FundId);
        var reference=fund.Deployments.Length==0 ? fund.Reference : fund.Deployments.SingleOrDefault(x=>x.Reference.DeploymentKey==original.Authority.DeploymentKey)?.Reference;
        Require(row.Status==ReservationStatus.Consumed && row.Version==2 && row.Execution==order.ExecutionId &&
            original.Authority==reference && original.FundId==order.FundId && original.BookId==order.BookId && original.OrderId==order.OrderId &&
            original.StrategyUnits==order.StrategyUnits && original.SizedOrderHash==order.SizedOrderHash &&
            original.ValidUntilUtc==order.ValidUntilUtc && original.TradeIds.Order().SequenceEqual(order.Legs.Select(x=>x.TradeId).Distinct().Order()),
            FinancialReasons.AuthorityRevoked,"The order no longer has its exact consumed capacity and Fund authority.");
        var consumed=await ReadOperationAsync<CapacityConsumptionCompletedEvent>(db,request.PortfolioId,request.Body.ConsumptionOperationId,
            request.Body.ConsumptionInputHash,ct);
        Require(consumed?.Id==request.Body.ConsumptionCompletedEventId && consumed.Receipt.ReservationId==order.ReservationId &&
            consumed.Receipt.Status==ReservationStatus.Consumed,FinancialReasons.AuthorityDenied,"Committed consumption receipt is required.");
        var accepted=await CapacityReservationStore.ReadEvidenceAsync<ICapacityExecutionAcceptedEvent>(db,order.ExecutionId,ct);
        Require(accepted?.CapacityAcceptance.ExecutionOrderHash==order.ContentHash && accepted.CapacityAcceptance.ExecutionId==order.ExecutionId &&
            accepted.CapacityAcceptance.SizedOrderHash==order.SizedOrderHash,FinancialReasons.AuthorityDenied,
            "Only the exact order in the committed execution intent may be submitted.");
        var id=Guid.NewGuid(); var revision=checked(authority.Revision+1);
        var receipt=new EmulatorOrderReceipt(order.ExecutionId,order,$"EMU-{order.ExecutionId:N}",revision,now,id);
        var completed=new EmulatorOrderSubmittedEvent
        {
            Id=id,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,EntityId=request.EntityId,
            Subject=new(ActorType.Event,SubmitEmulatorOrderCommand.Actor,nameof(EmulatorOrderSubmittedEvent),request.EntityId.Format()),
            CorrelationId=request.CorrelationId,CausationId=request.CausationId,InputHash=request.InputSha256,
            CommittedAtUtc=now,ReceivedOn=now,AggregateId=request.EntityId.Format(),Receipt=receipt
        };
        await db.ExecuteAsync("""
            INSERT INTO portfolio_financial.emulator_order(execution_id,portfolio_id,fund_id,order_id,reservation_id,operation_id,
              request,receipt,input_hash,submitted_at_utc) VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10);
            """,[order.ExecutionId,order.PortfolioId,order.FundId,order.OrderId,order.ReservationId,request.OperationId,
                Json(request),Json(receipt),request.InputSha256,now],ct);
        await SaveOutcomeAsync(db,request,completed,revision,false,ct);
        return completed;
    },token);
}
