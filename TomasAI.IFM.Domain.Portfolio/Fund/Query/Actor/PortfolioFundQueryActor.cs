using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Fund.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using GetFundQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundQuery;
using GetFundRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRevisionQuery;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsQuery;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundAllocationQuery;
using GetFundRiskEnvelopeQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRiskEnvelopeQuery;
using GetFundTemplateAssignmentsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundTemplateAssignmentsQuery;
using ResolveForSelectionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.ResolveForSelectionQuery;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategySnapshotQuery;
using GetFundOrderByOrderIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderByOrderIdQuery;
using GetFundOrderTradeByTradeIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradeByTradeIdQuery;
using GetFundCompositionByWorkflowQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundCompositionByWorkflowQuery;
using GetFundOrdersPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrdersPageQuery;
using GetFundOrderTradesPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundOrderTradesPageQuery;
using GetPortfolioFundStrategyReferenceCombinationsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioFundStrategyReferenceCombinationsQuery;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Query.Actor;

/// <summary>Owns Fund projection queries on the PortfolioFundQuery route.</summary>
/// <param name="actorContext">The Fund query mailbox context.</param>
/// <param name="dbFactory">The Portfolio projection database factory.</param>
/// <param name="identityAllocator">The durable business-identity allocator.</param>
/// <param name="operationalGuard">The Portfolio read policy guard.</param>
/// <param name="logger">The typed actor logger.</param>
public sealed class PortfolioFundQueryActor(
    IQueryActorContext<PortfolioFundQueryActor> actorContext,
    IDbContextFactory dbFactory,
    IPortfolioBusinessIdAllocator identityAllocator,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioFundQueryActor> logger)
    : BaseQueryActor<PortfolioFundQueryActor>(actorContext, logger)
{
    public const string ActorName = PortfolioQueryRoutes.Fund;
    readonly PortfolioQueryParameters _parameters = new(dbFactory, identityAllocator);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [GetFundQuery.Verb] = static message => message.AsQuery<GetFundQuery, FundMandateReadModel>()!,
        [GetFundRevisionQuery.Verb] = static message => message.AsQuery<GetFundRevisionQuery, PortfolioAggregateRevision>()!,
        [GetFundsQuery.Verb] = static message => message.AsQuery<GetFundsQuery, PortfolioPage<FundMandateReadModel>>()!,
        [GetFundAllocationQuery.Verb] = static message => message.AsQuery<GetFundAllocationQuery, FundAllocationReadModel>()!,
        [GetFundRiskEnvelopeQuery.Verb] = static message => message.AsQuery<GetFundRiskEnvelopeQuery, FundRiskEnvelopeReadModel>()!,
        [GetFundTemplateAssignmentsQuery.Verb] = static message => message.AsQuery<GetFundTemplateAssignmentsQuery, FundTradeTemplateAssignmentReadModel[]>()!,
        [ResolveForSelectionQuery.Verb] = static message => message.AsQuery<ResolveForSelectionQuery, PortfolioFundStrategySnapshot>()!,
        [GetPortfolioFundStrategySnapshotQuery.Verb] = static message => message.AsQuery<GetPortfolioFundStrategySnapshotQuery, PortfolioFundStrategySnapshot>()!,
        [GetFundOrderByOrderIdQuery.Verb] = static message => message.AsQuery<GetFundOrderByOrderIdQuery, FundOrderProjectionReadModel>()!,
        [GetFundOrderTradeByTradeIdQuery.Verb] = static message => message.AsQuery<GetFundOrderTradeByTradeIdQuery, FundOrderTradeProjectionReadModel>()!,
        [GetFundCompositionByWorkflowQuery.Verb] = static message => message.AsQuery<GetFundCompositionByWorkflowQuery, FundCompositionWorkflowProjectionReadModel[]>()!,
        [GetFundOrdersPageQuery.Verb] = static message => message.AsQuery<GetFundOrdersPageQuery, PortfolioPage<FundOrderProjectionReadModel>>()!,
        [GetFundOrderTradesPageQuery.Verb] = static message => message.AsQuery<GetFundOrderTradesPageQuery, PortfolioPage<FundOrderTradeProjectionReadModel>>()!,
        [GetPortfolioFundStrategyReferenceCombinationsQuery.Verb] = static message => message.AsQuery<GetPortfolioFundStrategyReferenceCombinationsQuery, PortfolioFundStrategyReferenceCombination[]>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioQueryParameters, IQueryActorContext<PortfolioFundQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<PortfolioQueryParameters, IQueryActorContext<PortfolioFundQueryActor>, IQuery, CancellationToken, ValueTask>>
    {
        [typeof(GetFundQuery)] = static (parameters, context, query, token) =>
            ((GetFundQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundRevisionQuery)] = static (parameters, context, query, token) =>
            ((GetFundRevisionQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundsQuery)] = static (parameters, context, query, token) =>
            ((GetFundsQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundAllocationQuery)] = static (parameters, context, query, token) =>
            ((GetFundAllocationQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundRiskEnvelopeQuery)] = static (parameters, context, query, token) =>
            ((GetFundRiskEnvelopeQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundTemplateAssignmentsQuery)] = static (parameters, context, query, token) =>
            ((GetFundTemplateAssignmentsQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(ResolveForSelectionQuery)] = static (parameters, context, query, token) =>
            ((ResolveForSelectionQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetPortfolioFundStrategySnapshotQuery)] = static (parameters, context, query, token) =>
            ((GetPortfolioFundStrategySnapshotQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundOrderByOrderIdQuery)] = static (parameters, context, query, token) =>
            ((GetFundOrderByOrderIdQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundOrderTradeByTradeIdQuery)] = static (parameters, context, query, token) =>
            ((GetFundOrderTradeByTradeIdQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundCompositionByWorkflowQuery)] = static (parameters, context, query, token) =>
            ((GetFundCompositionByWorkflowQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundOrdersPageQuery)] = static (parameters, context, query, token) =>
            ((GetFundOrdersPageQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetFundOrderTradesPageQuery)] = static (parameters, context, query, token) =>
            ((GetFundOrderTradesPageQuery)query).ExecuteAsync(context, parameters, token),
        [typeof(GetPortfolioFundStrategyReferenceCombinationsQuery)] = static (parameters, context, query, token) =>
            ((GetPortfolioFundStrategyReferenceCombinationsQuery)query).ExecuteAsync(context, parameters, token),
    };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys, static (query, exception) => exception switch
        {
            PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
            PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
            _ => PortfolioErrorCodes.ValidationFailed,
        });

    protected override IQuery ParseMessage(IQueryActorContext<PortfolioFundQueryActor> context, IActorMessage message) =>
        ParseMappedQuery(context, message, _parseMap);

    protected override ValueTask ReceiveAsync(IQueryActorContext<PortfolioFundQueryActor> context, IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<PortfolioFundQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        dynamic request = query;
        using var activity = PortfolioTelemetry.StartRequest("query", query.Subject.Verb, request.CorrelationId);
        operationalGuard.Demand(PortfolioOperation.Read, request.Access, mutation: false);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            await ResolveMappedQueryHandler(query, _receiveMap)(_parameters, context, query, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PortfolioTelemetry.QueryDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("portfolio.operation", query.Subject.Verb),
                new KeyValuePair<string, object?>("portfolio.outcome", "completed"));
        }
    }

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<PortfolioFundQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);
}

/// <summary>Provides the Fund query mailbox for the split actor route.</summary>
/// <param name="supervisor">The actor supervisor.</param>
public sealed class PortfolioFundQueryContext(IActorSupervisor supervisor)
    : QueryActorContext(supervisor, new ActorMailboxId(ActorType.Query, PortfolioFundQueryActor.ActorName)),
        IQueryActorContext<PortfolioFundQueryActor>;
