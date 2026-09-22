using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Query.Actor;

/// <summary>Maps scoped financial reads to the authoritative PostgreSQL query store.</summary>
public sealed class CapacityReservationQueryActor(IQueryActorContext<CapacityReservationQueryActor> context,IFinancialQueryStore store,ILogger<CapacityReservationQueryActor> logger)
    :BaseQueryActor<CapacityReservationQueryActor>(context,logger)
{
    public const string ActorName="CapacityReservationQuery";
    static readonly IReadOnlyDictionary<string,Func<IActorMessage,IQuery>> _parseMap=
        new Dictionary<string,Func<IActorMessage,IQuery>>(StringComparer.Ordinal)
        {
            ["GetFinancialAdmissionSnapshot"]=message=>message.AsQuery<GetFinancialAdmissionSnapshotQuery,FinancialRead<FinancialAdmissionSnapshot>>()!,
            ["GetCapacityReservation"]=message=>message.AsQuery<GetCapacityReservationQuery,FinancialRead<FinancialReservationView>>()!,
            ["GetCapacityUsage"]=message=>message.AsQuery<GetCapacityUsageQuery,FinancialRead<FinancialCapacityUsage>>()!,
            ["GetFundReservationsPage"]=message=>message.AsQuery<GetFundReservationsPageQuery,FinancialRead<FinancialPage<FinancialReservationView>>>()!,
        }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type,Func<IQuery,IFinancialQueryStore,IQueryActorContext<CapacityReservationQueryActor>,CancellationToken,ValueTask>> _receiveMap=
        new Dictionary<Type,Func<IQuery,IFinancialQueryStore,IQueryActorContext<CapacityReservationQueryActor>,CancellationToken,ValueTask>>
        {
            [typeof(GetFinancialAdmissionSnapshotQuery)]=(query,database,owner,token)=>((GetFinancialAdmissionSnapshotQuery)query).ExecuteAsync(database,owner,token),
            [typeof(GetCapacityReservationQuery)]=(query,database,owner,token)=>((GetCapacityReservationQuery)query).ExecuteAsync(database,owner,token),
            [typeof(GetCapacityUsageQuery)]=(query,database,owner,token)=>((GetCapacityUsageQuery)query).ExecuteAsync(database,owner,token),
            [typeof(GetFundReservationsPageQuery)]=(query,database,owner,token)=>((GetFundReservationsPageQuery)query).ExecuteAsync(database,owner,token),
        }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type,QueryExceptionHandler> _exceptionMap=CreateQueryExceptionMap(_receiveMap.Keys,
        (query,error)=>error is FinancialOperationException financial?financial.Code:FinancialReasons.PersistenceFailed);
    protected override IQuery ParseMessage(IQueryActorContext<CapacityReservationQueryActor> owner,IActorMessage message)=>ParseMappedQuery(owner,message,_parseMap);
    protected override ValueTask ReceiveAsync(IQueryActorContext<CapacityReservationQueryActor> owner,IQuery query)=>ReceiveAsync(owner,query,CancellationToken.None);
    protected override ValueTask ReceiveAsync(IQueryActorContext<CapacityReservationQueryActor> owner,IQuery query,CancellationToken token)
        =>ResolveMappedQueryHandler(query,_receiveMap)(query,store,owner,token);
    protected override ValueTask OnExceptionAsync(IQueryActorContext<CapacityReservationQueryActor> owner,ActorThreadId threadId,IQuery query,string verb,Exception error)
        =>ExceptionMappedQueryAsync(owner,threadId,query,verb,error,_exceptionMap);
}
public sealed class CapacityReservationQueryContext(IActorSupervisor supervisor)
    :QueryActorContext(supervisor,new ActorMailboxId(ActorType.Query,CapacityReservationQueryActor.ActorName)),IQueryActorContext<CapacityReservationQueryActor>;
