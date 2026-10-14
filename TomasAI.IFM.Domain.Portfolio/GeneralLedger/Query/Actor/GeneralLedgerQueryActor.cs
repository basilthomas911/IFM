using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Actor;

/// <summary>Maps scoped financial reads to the authoritative PostgreSQL query store.</summary>
public sealed class GeneralLedgerQueryActor(IQueryActorContext<GeneralLedgerQueryActor> context, IFinancialQueryStore store, FinancialBookPreparation preparation, FinancialAuthorityPreparation authority, ILogger<GeneralLedgerQueryActor> logger)
    : BaseQueryActor<GeneralLedgerQueryActor>(context, logger)
{
    public const string ActorName = "GeneralLedgerQuery";
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            ["PrepareFinancialAuthority"] = message => message.AsQuery<PrepareFinancialAuthorityQuery, FinancialRead<FinancialAuthorityDraft>>()!,
            ["PrepareFinancialBook"] = message => message.AsQuery<PrepareFinancialBookQuery, FinancialRead<FinancialBookSetup>>()!,
            ["GetFinancialLedgerConfiguration"] = message => message.AsQuery<GetFinancialLedgerConfigurationQuery, FinancialRead<FinancialLedgerConfiguration>>()!,
            ["GetFinancialPostingConfiguration"] = message => message.AsQuery<GetFinancialPostingConfigurationQuery, FinancialRead<FinancialPostingConfiguration>>()!,
            ["GetFundRiskAuthorization"] = message => message.AsQuery<GetFundRiskAuthorizationQuery, FinancialRead<FundRiskAuthorizationEvidence>>()!,
            ["GetPostingReceipt"] = message => message.AsQuery<GetPostingReceiptQuery, FinancialRead<FinancialOperationOutcome>>()!,
            ["GetJournal"] = message => message.AsQuery<GetJournalQuery, FinancialRead<FinancialJournal>>()!,
            ["GetAccountBalances"] = message => message.AsQuery<GetAccountBalancesQuery, FinancialRead<FinancialBalanceSnapshot>>()!,
            ["GetTrialBalance"] = message => message.AsQuery<GetTrialBalanceQuery, FinancialRead<FinancialTrialBalance>>()!,
            ["GetFundTransactionsPage"] = message => message.AsQuery<GetFundTransactionsPageQuery, FinancialRead<FinancialPage<FinancialTransactionRow>>>()!,
            ["GetReconciliation"] = message => message.AsQuery<GetReconciliationQuery, FinancialRead<FinancialReconciliationView>>()!,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IQuery, IFinancialQueryStore, FinancialBookPreparation, FinancialAuthorityPreparation, IQueryActorContext<GeneralLedgerQueryActor>, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IQuery, IFinancialQueryStore, FinancialBookPreparation, FinancialAuthorityPreparation, IQueryActorContext<GeneralLedgerQueryActor>, CancellationToken, ValueTask>>
        {
            [typeof(PrepareFinancialAuthorityQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((PrepareFinancialAuthorityQuery)query).ExecuteAsync(authorityPreparer, owner, token),
            [typeof(PrepareFinancialBookQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((PrepareFinancialBookQuery)query).ExecuteAsync(preparer, owner, token),
            [typeof(GetFinancialLedgerConfigurationQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetFinancialLedgerConfigurationQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetFinancialPostingConfigurationQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetFinancialPostingConfigurationQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetFundRiskAuthorizationQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetFundRiskAuthorizationQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetPostingReceiptQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetPostingReceiptQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetJournalQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetJournalQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetAccountBalancesQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetAccountBalancesQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetTrialBalanceQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetTrialBalanceQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetFundTransactionsPageQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetFundTransactionsPageQuery)query).ExecuteAsync(database, owner, token),
            [typeof(GetReconciliationQuery)] = (query, database, preparer, authorityPreparer, owner, token) => ((GetReconciliationQuery)query).ExecuteAsync(database, owner, token),
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys,
        (query, error) => error is FinancialOperationException financial ? financial.Code : FinancialReasons.PersistenceFailed);
    protected override IQuery ParseMessage(IQueryActorContext<GeneralLedgerQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<GeneralLedgerQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<GeneralLedgerQueryActor> context, IQuery query, CancellationToken cancellationToken)
        => ResolveMappedQueryHandler(query, _receiveMap)(query, store, preparation, authority, context, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<GeneralLedgerQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}

