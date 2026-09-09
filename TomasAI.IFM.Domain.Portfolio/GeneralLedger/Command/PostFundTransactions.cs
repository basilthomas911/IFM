using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

public static class PostFundTransactions
{
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this PostFundTransactionsCommand request,GeneralLedgerCommandServices services,CancellationToken token)
    {
        var replay=await services.Database.ReadOperationAsync<LedgerPostingBatchCompletedEvent>(request.PortfolioId,request.OperationId,request.InputSha256,token);
        if(replay is not null) return await replay.NotifyAsync(services.Projector,services.Logger);
        FinancialRequestValidation.Demand(request,"LedgerPost",DateTime.UtcNow);
        if(request.Body.Items.Length is <1 or >100 || request.Body.Items.Any(x=>x.BookId!=request.Body.BookId) ||
            request.Body.ManifestHash!=FinancialCanonicalHash.Compute(request.Body.Items))
            throw new FinancialOperationException(FinancialReasons.InvalidContract,"Batch book/count/manifest is invalid.");
        var items=new List<PreparedLedgerPosting>(request.Body.Items.Length);
        foreach(var body in request.Body.Items)
        {
            FinancialRequestValidation.DemandPosting(request,body,DateTime.UtcNow);
            items.Add(new(await services.Ids.TransactionAsync(token),await services.Ids.JournalAsync(token),body));
        }
        var completed=await services.Store.PostAsync(request,items,(body,rule,prior,original,remaining)=>
            LedgerPostingModel.Calculate(body,rule,prior,original,remaining,body.TransactionKind is LedgerTransactionKind.Adjustment or LedgerTransactionKind.OpeningBalance),
            info=>request.Complete(info),token);
        return await completed.NotifyAsync(services.Projector,services.Logger);
    }
    public static LedgerPostingBatchCompletedEvent Complete(this PostFundTransactionsCommand request,LedgerCommitInfo info)=>new()
    {
        Id=info.EventId,Subject=new(ActorType.Event,PostFundTransactionsCommand.Actor,nameof(LedgerPostingBatchCompletedEvent),request.EntityId.Format()),
        EntityId=request.EntityId,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,
        CorrelationId=request.CorrelationId,CausationId=request.CausationId,CommittedAtUtc=info.CommittedAtUtc,ReceivedOn=info.CommittedAtUtc,
        InputHash=request.InputSha256,AggregateId=request.EntityId.Format(),
        Receipt=new() { OperationId=request.OperationId,PortfolioId=request.PortfolioId,BookId=request.Body.BookId,Items=info.Items.ToArray(),
            ManifestHash=request.Body.ManifestHash,InputHash=request.InputSha256,FinancialRevision=info.Revision,CommittedAtUtc=info.CommittedAtUtc,CompletedEventId=info.EventId }
    };
}
