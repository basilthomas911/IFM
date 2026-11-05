using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query.Model;
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
public sealed class GeneralLedgerQueryActor(IQueryActorContext<GeneralLedgerQueryActor> context, IFinancialQueryStore store, ILogger<GeneralLedgerQueryActor> logger)
    : BaseQueryActor<GeneralLedgerQueryActor>(context, logger)
{
    public const string ActorName = "GeneralLedgerQuery";
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
        {
            ["GetFinancialLedgerConfiguration"] = message => message.AsQuery<GetFinancialLedgerConfigurationQuery, FinancialRead<FinancialLedgerConfiguration>>()!,
            ["GetFinancialPostingConfiguration"] = message => message.AsQuery<GetFinancialPostingConfigurationQuery, FinancialRead<FinancialPostingConfiguration>>()!,
            ["GetFundRiskAuthorization"] = message => message.AsQuery<GetFundRiskAuthorizationQuery, FinancialRead<FundRiskAuthorizationEvidence>>()!,
            ["GetPostingReceipt"] = message => message.AsQuery<GetPostingReceiptQuery, FinancialRead<FinancialOperationOutcome>>()!,
            ["GetJournal"] = message => message.AsQuery<GetJournalQuery, FinancialRead<FinancialJournal>>()!,
            ["GetAccountBalances"] = message => message.AsQuery<GetAccountBalancesQuery, FinancialRead<FinancialBalanceSnapshot>>()!,
            ["GetTrialBalance"] = message => message.AsQuery<GetTrialBalanceQuery, FinancialRead<FinancialTrialBalance>>()!,
            [GetFundPerformanceMetricsQuery.Verb] = message => message.AsQuery<GetFundPerformanceMetricsQuery, FinancialRead<FundPerformanceMetrics>>()!,
            ["GetFundTransactionsPage"] = message => message.AsQuery<GetFundTransactionsPageQuery, FinancialRead<FinancialPage<FinancialTransactionRow>>>()!,
            ["GetReconciliation"] = message => message.AsQuery<GetReconciliationQuery, FinancialRead<FinancialReconciliationView>>()!,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IQuery, IFinancialQueryStore, IQueryActorContext<GeneralLedgerQueryActor>, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IQuery, IFinancialQueryStore, IQueryActorContext<GeneralLedgerQueryActor>, CancellationToken, ValueTask>>
        {
            [typeof(GetFinancialLedgerConfigurationQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetFinancialLedgerConfigurationQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetFinancialPostingConfigurationQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetFinancialPostingConfigurationQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetFundRiskAuthorizationQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetFundRiskAuthorizationQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetPostingReceiptQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetPostingReceiptQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetJournalQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetJournalQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetAccountBalancesQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetAccountBalancesQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetTrialBalanceQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetTrialBalanceQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetFundPerformanceMetricsQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetFundPerformanceMetricsQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetFundTransactionsPageQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetFundTransactionsPageQuery)query).ExecuteAsync(database, token), token),
            [typeof(GetReconciliationQuery)] = (query, database, owner, token) => FinancialQueryReply.ReplyAsync(owner, query, ((GetReconciliationQuery)query).ExecuteAsync(database, token), token),
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap = CreateQueryExceptionMap(_receiveMap.Keys,
        (query, error) => error is FinancialOperationException financial ? financial.Code : FinancialReasons.PersistenceFailed);
    protected override IQuery ParseMessage(IQueryActorContext<GeneralLedgerQueryActor> context, IActorMessage message) => ParseMappedQuery(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<GeneralLedgerQueryActor> context, IQuery query) => ReceiveAsync(context, query, CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<GeneralLedgerQueryActor> context, IQuery query, CancellationToken cancellationToken)
        => ResolveMappedQueryHandler(query, _receiveMap)(query, store, context, cancellationToken);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<GeneralLedgerQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
        => ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}

