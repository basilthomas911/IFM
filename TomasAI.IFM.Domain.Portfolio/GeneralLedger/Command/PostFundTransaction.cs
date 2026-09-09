using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

public static class PostFundTransaction
{
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this PostFundTransactionCommand request,GeneralLedgerCommandServices services,CancellationToken token)
    {
        // Receipt replay precedes sequence allocation and expiry checks; validation still enforces caller permission.
        var replay=await services.Database.ReadOperationAsync<LedgerPostingCompletedEvent>(request.PortfolioId,request.OperationId,request.InputSha256,token);
        if(replay is not null) return await replay.NotifyAsync(services.Projector,services.Logger);
        var allowAdjustment=FinancialRequestValidation.DemandPosting(request,request.Body,DateTime.UtcNow);
        var item=new PreparedLedgerPosting(await services.Ids.TransactionAsync(token),await services.Ids.JournalAsync(token),request.Body);
        var completed=await services.Store.PostAsync(request,[item],(body,rule,prior,original,remaining)=>
            LedgerPostingModel.Calculate(body,rule,prior,original,remaining,allowAdjustment),
            info=>request.Complete(info),token);
        return await completed.NotifyAsync(services.Projector,services.Logger);
    }
    public static LedgerPostingCompletedEvent Complete(this PostFundTransactionCommand request,LedgerCommitInfo info)=>new()
    {
        Id=info.EventId,Subject=new(ActorType.Event,PostFundTransactionCommand.Actor,nameof(LedgerPostingCompletedEvent),request.EntityId.Format()),
        EntityId=request.EntityId,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,
        CorrelationId=request.CorrelationId,CausationId=request.CausationId,CommittedAtUtc=info.CommittedAtUtc,ReceivedOn=info.CommittedAtUtc,
        InputHash=request.InputSha256,AggregateId=request.EntityId.Format(),
        Receipt=new() { OperationId=request.OperationId,PortfolioId=request.PortfolioId,BookId=request.Body.BookId,FundId=request.Body.FundId,
            TransactionId=info.Items[0].TransactionId,JournalId=info.Items[0].JournalId,JournalHash=info.Items[0].JournalHash,
            InputHash=request.InputSha256,FinancialRevision=info.Revision,CommittedAtUtc=info.CommittedAtUtc,CompletedEventId=info.EventId,
            Source=request.Body.Source,ObligationId=info.Items[0].ObligationId }
    };
}
