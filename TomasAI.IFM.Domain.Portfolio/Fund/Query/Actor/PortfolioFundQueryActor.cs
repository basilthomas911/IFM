using TomasAI.IFM.Domain.Portfolio.Query.Model;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
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
/// <param name="operationalGuard">The Portfolio read policy guard.</param>
/// <param name="logger">The typed actor logger.</param>
public sealed class PortfolioFundQueryActor(
    IQueryActorContext<PortfolioFundQueryActor> actorContext,
    IDbContextFactory dbFactory,
    IPortfolioOperationalGuard operationalGuard,
    ILogger<PortfolioFundQueryActor> logger)
    : BaseQueryActor<PortfolioFundQueryActor>(actorContext, logger)
{
    public const string ActorName = PortfolioQueryRoutes.Fund;
    readonly PortfolioQueryParameters _parameters = new(dbFactory);

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
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundRevisionQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundRevisionQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundsQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundsQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundAllocationQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundAllocationQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundRiskEnvelopeQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundRiskEnvelopeQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundTemplateAssignmentsQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundTemplateAssignmentsQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(ResolveForSelectionQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((ResolveForSelectionQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetPortfolioFundStrategySnapshotQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetPortfolioFundStrategySnapshotQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundOrderByOrderIdQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundOrderByOrderIdQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundOrderTradeByTradeIdQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundOrderTradeByTradeIdQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundCompositionByWorkflowQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundCompositionByWorkflowQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundOrdersPageQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundOrdersPageQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetFundOrderTradesPageQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetFundOrderTradesPageQuery)query).ExecuteAsync(parameters, token), token),
            [typeof(GetPortfolioFundStrategyReferenceCombinationsQuery)] = static (parameters, context, query, token) =>
                PortfolioQueryHandlerModel.ReplyAsync(context, query, ((GetPortfolioFundStrategyReferenceCombinationsQuery)query).ExecuteAsync(parameters, token), token),
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
        ArgumentNullException.ThrowIfNull(query);
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
        Exception ex) =>
        ExceptionMappedQueryAsync(context, threadId, query, verb, ex, _exceptionMap);
}
