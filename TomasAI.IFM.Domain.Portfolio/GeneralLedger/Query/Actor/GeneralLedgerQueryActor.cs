using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Actor;

/// <summary>Maps scoped financial reads to the authoritative PostgreSQL query store.</summary>
public sealed class GeneralLedgerQueryActor(IQueryActorContext<GeneralLedgerQueryActor> context,IFinancialQueryStore store,FinancialBookPreparation preparation,FinancialAuthorityPreparation authority,ILogger<GeneralLedgerQueryActor> logger)
    :BaseQueryActor<GeneralLedgerQueryActor>(context,logger)
{
    public const string ActorName="GeneralLedgerQuery";
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,IQuery>> _parseMap=
        new Dictionary<string,Func<IActorMessage,IQuery>>(StringComparer.Ordinal)
        {
            ["PrepareFinancialAuthority"]=message=>message.AsQuery<FinancialQuery<PrepareFinancialAuthorityRequest,FinancialAuthorityDraft>,FinancialRead<FinancialAuthorityDraft>>()!,
            ["PrepareFinancialBook"]=message=>message.AsQuery<FinancialQuery<PrepareFinancialBookRequest,FinancialBookSetup>,FinancialRead<FinancialBookSetup>>()!,
            ["GetFinancialLedgerConfiguration"]=message=>message.AsQuery<FinancialQuery<GetFinancialLedgerConfigurationRequest,FinancialLedgerConfiguration>,FinancialRead<FinancialLedgerConfiguration>>()!,
            ["GetFinancialPostingConfiguration"]=message=>message.AsQuery<FinancialQuery<GetFinancialPostingConfigurationRequest,FinancialPostingConfiguration>,FinancialRead<FinancialPostingConfiguration>>()!,
            ["GetFundRiskAuthorization"] = message=>message.AsQuery<FinancialQuery<GetFundRiskAuthorizationRequest,FundRiskAuthorizationEvidence>,FinancialRead<FundRiskAuthorizationEvidence>>()!,
            ["GetPostingReceipt"]=message=>message.AsQuery<FinancialQuery<GetPostingReceiptRequest,FinancialOperationOutcome>,FinancialRead<FinancialOperationOutcome>>()!,
            ["GetJournal"]=message=>message.AsQuery<FinancialQuery<GetJournalRequest,FinancialJournal>,FinancialRead<FinancialJournal>>()!,
            ["GetAccountBalances"]=message=>message.AsQuery<FinancialQuery<GetAccountBalancesRequest,FinancialBalanceSnapshot>,FinancialRead<FinancialBalanceSnapshot>>()!,
            ["GetTrialBalance"]=message=>message.AsQuery<FinancialQuery<GetTrialBalanceRequest,FinancialTrialBalance>,FinancialRead<FinancialTrialBalance>>()!,
            ["GetFundTransactionsPage"]=message=>message.AsQuery<FinancialQuery<GetFundTransactionsPageRequest,FinancialPage<FinancialTransactionRow>>,FinancialRead<FinancialPage<FinancialTransactionRow>>>()!,
            ["GetReconciliation"]=message=>message.AsQuery<FinancialQuery<GetReconciliationRequest,FinancialReconciliationView>,FinancialRead<FinancialReconciliationView>>()!,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<IQuery,IFinancialQueryStore,FinancialBookPreparation,FinancialAuthorityPreparation,IQueryActorContext<GeneralLedgerQueryActor>,CancellationToken,ValueTask>> _receiveMap=
        new Dictionary<Type,Func<IQuery,IFinancialQueryStore,FinancialBookPreparation,FinancialAuthorityPreparation,IQueryActorContext<GeneralLedgerQueryActor>,CancellationToken,ValueTask>>
        {
            [typeof(FinancialQuery<PrepareFinancialAuthorityRequest,FinancialAuthorityDraft>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<PrepareFinancialAuthorityRequest,FinancialAuthorityDraft>)query).ExecuteAsync(authorityPreparer,owner,token),
            [typeof(FinancialQuery<PrepareFinancialBookRequest,FinancialBookSetup>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<PrepareFinancialBookRequest,FinancialBookSetup>)query).ExecuteAsync(preparer,owner,token),
            [typeof(FinancialQuery<GetFinancialLedgerConfigurationRequest,FinancialLedgerConfiguration>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetFinancialLedgerConfigurationRequest,FinancialLedgerConfiguration>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetFinancialPostingConfigurationRequest,FinancialPostingConfiguration>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetFinancialPostingConfigurationRequest,FinancialPostingConfiguration>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetFundRiskAuthorizationRequest,FundRiskAuthorizationEvidence>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetFundRiskAuthorizationRequest,FundRiskAuthorizationEvidence>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetPostingReceiptRequest,FinancialOperationOutcome>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetPostingReceiptRequest,FinancialOperationOutcome>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetJournalRequest,FinancialJournal>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetJournalRequest,FinancialJournal>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetAccountBalancesRequest,FinancialBalanceSnapshot>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetAccountBalancesRequest,FinancialBalanceSnapshot>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetTrialBalanceRequest,FinancialTrialBalance>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetTrialBalanceRequest,FinancialTrialBalance>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetFundTransactionsPageRequest,FinancialPage<FinancialTransactionRow>>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetFundTransactionsPageRequest,FinancialPage<FinancialTransactionRow>>)query).ExecuteAsync(database,owner,token),
            [typeof(FinancialQuery<GetReconciliationRequest,FinancialReconciliationView>)]=(query,database,preparer,authorityPreparer,owner,token)=>((FinancialQuery<GetReconciliationRequest,FinancialReconciliationView>)query).ExecuteAsync(database,owner,token),
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,QueryExceptionHandler> _exceptionMap=CreateQueryExceptionMap(_receiveMap.Keys,
        (query,error)=>error is FinancialOperationException financial?financial.Code:FinancialReasons.PersistenceFailed);
    protected override IQuery ParseMessage(IQueryActorContext<GeneralLedgerQueryActor> owner,IActorMessage message)=>ParseMappedQuery(owner,message,_parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<GeneralLedgerQueryActor> owner,IQuery query)=>ReceiveAsync(owner,query,CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<GeneralLedgerQueryActor> owner,IQuery query,CancellationToken token)
        =>ResolveMappedQueryHandler(query,_receiveMap)(query,store,preparation,authority,owner,token);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<GeneralLedgerQueryActor> owner,ActorThreadId threadId,IQuery query,string verb,Exception error)
        =>ExceptionMappedQueryAsync(owner,threadId,query,verb,error,_exceptionMap);
}
public sealed class GeneralLedgerQueryContext(IActorSupervisor supervisor)
    :QueryActorContext(supervisor,new ActorMailboxId(ActorType.Query,GeneralLedgerQueryActor.ActorName)),IQueryActorContext<GeneralLedgerQueryActor>;

