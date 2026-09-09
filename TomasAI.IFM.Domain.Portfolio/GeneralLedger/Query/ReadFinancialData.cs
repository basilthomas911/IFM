using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;

public static class ReadFinancialData
{
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<PrepareFinancialAuthorityRequest,FinancialAuthorityDraft> query,
        FinancialAuthorityPreparation preparation,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","PrepareFinancialAuthority",()=>preparation.PrepareAsync(query.Scope,query.Parameters,token),token);
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<PrepareFinancialBookRequest,FinancialBookSetup> query,
        FinancialBookPreparation preparation,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","PrepareFinancialBook",()=>preparation.PrepareAsync(query.Scope,query.Parameters,token),token);
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFinancialLedgerConfigurationRequest,FinancialLedgerConfiguration> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetFinancialLedgerConfiguration",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFinancialPostingConfigurationRequest,FinancialPostingConfiguration> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetFinancialPostingConfiguration",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFundRiskAuthorizationRequest,FundRiskAuthorizationEvidence> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetFundRiskAuthorization",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFinancialAdmissionSnapshotRequest,FinancialAdmissionSnapshot> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"CapacityReservationQuery","GetFinancialAdmissionSnapshot",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);
    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetPostingReceiptRequest,FinancialOperationOutcome> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetPostingReceipt",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetJournalRequest,FinancialJournal> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetJournal",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetAccountBalancesRequest,FinancialBalanceSnapshot> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetAccountBalances",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetTrialBalanceRequest,FinancialTrialBalance> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetTrialBalance",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFundTransactionsPageRequest,FinancialPage<FinancialTransactionRow>> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetFundTransactionsPage",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetReconciliationRequest,FinancialReconciliationView> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"GeneralLedgerQuery","GetReconciliation",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetCapacityReservationRequest,FinancialReservationView> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"CapacityReservationQuery","GetCapacityReservation",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetCapacityUsageRequest,FinancialCapacityUsage> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"CapacityReservationQuery","GetCapacityUsage",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    public static ValueTask ExecuteAsync<TActor>(this FinancialQuery<GetFundReservationsPageRequest,FinancialPage<FinancialReservationView>> query,
        IFinancialQueryStore store,IQueryActorContext<TActor> context,CancellationToken token) where TActor:IActor
        =>query.ReplyAsync(context,"CapacityReservationQuery","GetFundReservationsPage",()=>store.ReadAsync(query.Scope,query.Parameters,token),token);

    static async ValueTask ReplyAsync<TRequest,TResult,TActor>(this FinancialQuery<TRequest,TResult> query,
        IQueryActorContext<TActor> context,string actor,string verb,Func<Task<FinancialRead<TResult>>> read,CancellationToken token)
        where TResult:class where TActor:IActor
    {
        token.ThrowIfCancellationRequested();
        if(query.SchemaVersion!=1 || query.Parameters is null || query.Scope is null || query.Scope.Access is null || query.Scope.PortfolioId<=0 ||
            query.QueryEntityId.PortfolioId!=query.Scope.PortfolioId || query.Subject.EntityId!=query.QueryEntityId.Format() ||
            !query.Subject.Is(ActorType.Query,actor,verb) || query.CorrelationId==Guid.Empty || query.RequestedAtUtc.Kind!=DateTimeKind.Utc ||
            MessagePackBinarySerializer.MeasureContent(query)>1048576)
            throw new FinancialOperationException(FinancialReasons.InvalidContract,"Financial query contract/scope is invalid.");
        var result=await read();
        if(MessagePackBinarySerializer.MeasureContent(result)>524288)
            throw new FinancialOperationException(FinancialReasons.InvalidContract,"Financial result exceeds 512 KiB; use a smaller page.");
        await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,new ServiceOk<FinancialRead<TResult>>(result));
    }
}
