using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using AllocatePortfolioBusinessIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.AllocatePortfolioBusinessIdRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioBusinessIdAllocation>;
using GetActivePortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetActivePolicyRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>;
using GetFundAllocationQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetAllocationRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundAllocationReadModel>;
using GetFundCompositionByWorkflowQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetCompositionRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundCompositionWorkflowProjectionReadModel[]>;
using GetFundOrderByOrderIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetOrderRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel>;
using GetFundOrderTradeByTradeIdQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetTradeRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel>;
using GetFundOrderTradesPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetOrderTradesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel>>;
using GetFundOrdersPageQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetOrdersRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel>>;
using GetFundQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel>;
using GetFundRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundRevisionRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioAggregateRevision>;
using GetFundRiskEnvelopeQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetEnvelopeRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundRiskEnvelopeReadModel>;
using GetFundsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetFundsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundMandateReadModel>>;
using GetFundTemplateAssignmentsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetAssignmentsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundTradeTemplateAssignmentReadModel[]>;
using GetLegacyFundCatalogQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetLegacyFundCatalogRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.LegacyFundHistoryReadModel[]>;
using GetLegacyFundOrdersQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetLegacyFundOrdersRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.LegacyFundOrderHistoryReadModel[]>;
using GetLegacyFundOrderTradesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetLegacyFundOrderTradesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.LegacyFundTradeHistoryReadModel[]>;
using GetLegacyPortfolioScopesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetLegacyPortfolioScopesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.LegacyPortfolioScopeReadModel[]>;
using GetPortfolioFinancialPoliciesQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPoliciesRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>>;
using GetPortfolioFinancialPolicyQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPolicyRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioFinancialPolicyReadModel>;
using GetPortfolioFundStrategyReferenceCombinationsQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetStrategyReferenceCombinationsRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioFundStrategyReferenceCombination[]>;
using GetPortfolioFundStrategySnapshotQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetStrategySnapshotRequest, TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioFundStrategySnapshot>;
using GetPortfolioQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioRequest, TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel>;
using GetPortfolioRevisionQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfolioRevisionRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioAggregateRevision>;
using GetPortfoliosQuery = TomasAI.IFM.Domain.Portfolio.Shared.Queries.PortfolioQuery<TomasAI.IFM.Domain.Portfolio.Shared.Queries.GetPortfoliosRequest, TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.PortfolioPage<TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.PortfolioReadModel>>;

namespace TomasAI.IFM.Domain.Portfolio.Query.Actor;

public interface IPortfolioQueryContext : IQueryActorContext<PortfolioQueryActor>
{
    IDbContextFactory DbFactory { get; }
    IPortfolioBusinessIdAllocator IdentityAllocator { get; }
    ILogger<PortfolioQueryActor> Logger { get; }
}

/// <summary>NATS-only public query boundary over PortfolioDb projections.</summary>
public sealed class PortfolioQueryActor(IQueryActorContext<PortfolioQueryActor> actorContext, IPortfolioOperationalGuard operationalGuard)
    : BaseQueryActor<PortfolioQueryActor>(actorContext, RequireContext(actorContext).Logger)
{
    public const string ActorName = PortfolioQuerySubjects.Actor;
    readonly PortfolioQueryService _service = new(
        RequireContext(actorContext).DbFactory.PortfolioDb,
        new PortfolioFundStrategyResolver(),
        RequireContext(actorContext).IdentityAllocator);
    readonly LegacyPortfolioHistoryQueryService _legacyHistory = new(
        new LegacyPortfolioHistoryStore(
            RequireContext(actorContext).DbFactory.FundLegacyDb,
            RequireContext(actorContext).DbFactory.TradeDb),
        RequireContext(actorContext).DbFactory.PortfolioDb,
        RequireContext(actorContext).IdentityAllocator);

    static IPortfolioQueryContext RequireContext(IQueryActorContext<PortfolioQueryActor> context) =>
        context as IPortfolioQueryContext
        ?? throw new ArgumentException("PortfolioQueryActor requires its Portfolio query context.", nameof(context));

    protected override IQuery ParseMessage(IQueryActorContext<PortfolioQueryActor> context, IActorMessage message) =>
        ParseMappedQuery(context, message, _parseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IQuery>>(StringComparer.Ordinal)
    {
        [PortfolioQueryVerbs.GetPortfolio] = static message => message.AsQuery<GetPortfolioQuery, PortfolioReadModel>()!,
        [PortfolioQueryVerbs.GetPortfolioRevision] = static message => message.AsQuery<GetPortfolioRevisionQuery, PortfolioAggregateRevision>()!,
        [PortfolioQueryVerbs.GetPortfolios] = static message => message.AsQuery<GetPortfoliosQuery, PortfolioPage<PortfolioReadModel>>()!,
        [PortfolioQueryVerbs.GetFund] = static message => message.AsQuery<GetFundQuery, FundMandateReadModel>()!,
        [PortfolioQueryVerbs.GetFundRevision] = static message => message.AsQuery<GetFundRevisionQuery, PortfolioAggregateRevision>()!,
        [PortfolioQueryVerbs.GetFunds] = static message => message.AsQuery<GetFundsQuery, PortfolioPage<FundMandateReadModel>>()!,
        [PortfolioQueryVerbs.GetFundAllocation] = static message => message.AsQuery<GetFundAllocationQuery, FundAllocationReadModel>()!,
        [PortfolioQueryVerbs.GetFundRiskEnvelope] = static message => message.AsQuery<GetFundRiskEnvelopeQuery, FundRiskEnvelopeReadModel>()!,
        [PortfolioQueryVerbs.GetFundTemplateAssignments] = static message => message.AsQuery<GetFundTemplateAssignmentsQuery, FundTradeTemplateAssignmentReadModel[]>()!,
        [PortfolioQueryVerbs.GetPortfolioFundStrategySnapshot] = static message => message.AsQuery<GetPortfolioFundStrategySnapshotQuery, PortfolioFundStrategySnapshot>()!,
        [PortfolioQueryVerbs.GetFundOrderByOrderId] = static message => message.AsQuery<GetFundOrderByOrderIdQuery, FundOrderProjectionReadModel>()!,
        [PortfolioQueryVerbs.GetFundOrderTradeByTradeId] = static message => message.AsQuery<GetFundOrderTradeByTradeIdQuery, FundOrderTradeProjectionReadModel>()!,
        [PortfolioQueryVerbs.GetFundCompositionByWorkflow] = static message => message.AsQuery<GetFundCompositionByWorkflowQuery, FundCompositionWorkflowProjectionReadModel[]>()!,
        [PortfolioQueryVerbs.GetFundOrdersPage] = static message => message.AsQuery<GetFundOrdersPageQuery, PortfolioPage<FundOrderProjectionReadModel>>()!,
        [PortfolioQueryVerbs.GetFundOrderTradesPage] = static message => message.AsQuery<GetFundOrderTradesPageQuery, PortfolioPage<FundOrderTradeProjectionReadModel>>()!,
        [PortfolioQueryVerbs.GetPortfolioFundStrategyReferenceCombinations] = static message => message.AsQuery<GetPortfolioFundStrategyReferenceCombinationsQuery, PortfolioFundStrategyReferenceCombination[]>()!,
        [PortfolioQueryVerbs.AllocatePortfolioBusinessId] = static message => message.AsQuery<AllocatePortfolioBusinessIdQuery, PortfolioBusinessIdAllocation>()!,
        [PortfolioQueryVerbs.GetPortfolioFinancialPolicy] = static message => message.AsQuery<GetPortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>()!,
        [PortfolioQueryVerbs.GetPortfolioFinancialPolicies] = static message => message.AsQuery<GetPortfolioFinancialPoliciesQuery, PortfolioPage<PortfolioFinancialPolicyReadModel>>()!,
        [PortfolioQueryVerbs.GetActivePortfolioFinancialPolicy] = static message => message.AsQuery<GetActivePortfolioFinancialPolicyQuery, PortfolioFinancialPolicyReadModel>()!,
        [PortfolioQueryVerbs.GetLegacyPortfolioScopes] = static message => message.AsQuery<GetLegacyPortfolioScopesQuery, LegacyPortfolioScopeReadModel[]>()!,
        [PortfolioQueryVerbs.GetLegacyFundCatalog] = static message => message.AsQuery<GetLegacyFundCatalogQuery, LegacyFundHistoryReadModel[]>()!,
        [PortfolioQueryVerbs.GetLegacyFundOrders] = static message => message.AsQuery<GetLegacyFundOrdersQuery, LegacyFundOrderHistoryReadModel[]>()!,
        [PortfolioQueryVerbs.GetLegacyFundOrderTrades] = static message => message.AsQuery<GetLegacyFundOrderTradesQuery, LegacyFundTradeHistoryReadModel[]>()!,
    };

    static readonly IReadOnlyDictionary<Type, Func<PortfolioQueryActor,
        IQueryActorContext<PortfolioQueryActor>, IQuery, CancellationToken, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<PortfolioQueryActor,
            IQueryActorContext<PortfolioQueryActor>, IQuery, CancellationToken, ValueTask>>
        {
            [typeof(GetPortfolioQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetPortfolioQuery)query;
                return ReplyAsync(context, typed, actor._service.GetPortfolioAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.Version, cancellationToken));
            },
            [typeof(GetPortfolioRevisionQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetPortfolioRevisionQuery)query;
                return ReplyAsync(context, typed, actor._service.GetPortfolioRevisionAsync(
                    typed.Parameters.PortfolioId, cancellationToken));
            },
            [typeof(GetPortfoliosQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetPortfoliosQuery)query;
                return ReplyAsync(context, typed, actor._service.GetPortfoliosAsync(
                    typed.Parameters.State is null ? null : (PortfolioOperatingState)typed.Parameters.State,
                    typed.Parameters.PageSize, typed.Parameters.PageToken, cancellationToken));
            },
            [typeof(GetFundQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundQuery)query;
                return ReplyAsync(context, typed, actor._service.GetFundAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.FundId, typed.Parameters.Version, cancellationToken));
            },
            [typeof(GetFundRevisionQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundRevisionQuery)query;
                return ReplyAsync(context, typed, actor._service.GetFundRevisionAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.FundId, cancellationToken));
            },
            [typeof(GetFundsQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundsQuery)query;
                return ReplyAsync(context, typed, actor._service.GetFundsAsync(
                    typed.Parameters.PortfolioId,
                    typed.Parameters.State is null ? null : (FundOperatingState)typed.Parameters.State,
                    typed.Parameters.PageSize, typed.Parameters.PageToken, cancellationToken));
            },
            [typeof(GetFundAllocationQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundAllocationQuery)query;
                return ReplyAsync(context, typed, actor._service.GetFundAllocationAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.FundId, cancellationToken));
            },
            [typeof(GetFundRiskEnvelopeQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundRiskEnvelopeQuery)query;
                return ReplyAsync(context, typed, actor._service.GetFundRiskEnvelopeAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.FundId, typed.Parameters.AsOfUtc, cancellationToken));
            },
            [typeof(GetFundTemplateAssignmentsQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundTemplateAssignmentsQuery)query;
                return ReplyAsync(context, typed, actor._service.GetAssignmentsAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.FundId, typed.Parameters.MandateVersion, cancellationToken));
            },
            [typeof(GetPortfolioFundStrategySnapshotQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetPortfolioFundStrategySnapshotQuery)query;
                return ReplyAsync(context, typed, actor._service.GetStrategySnapshotAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.TradingYear, typed.Parameters.DecisionHorizon,
                    typed.Parameters.UnderlyingRoot, typed.Parameters.AssetType, typed.Parameters.AsOfUtc,
                    typed.Parameters.WorkflowId, typed.Parameters.WorkflowRevision, typed.Parameters.CorrelationId,
                    cancellationToken));
            },
            [typeof(GetFundOrderByOrderIdQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundOrderByOrderIdQuery)query;
                return ReplyAsync(context, typed, actor._service.GetOrderAsync(
                    typed.Parameters.OrderId, cancellationToken));
            },
            [typeof(GetFundOrderTradeByTradeIdQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundOrderTradeByTradeIdQuery)query;
                return ReplyAsync(context, typed, actor._service.GetTradeAsync(
                    typed.Parameters.TradeId, cancellationToken));
            },
            [typeof(GetFundCompositionByWorkflowQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundCompositionByWorkflowQuery)query;
                return ReplyAsync(context, typed, actor._service.GetCompositionByWorkflowAsync(
                    typed.Parameters.WorkflowId, cancellationToken));
            },
            [typeof(GetFundOrdersPageQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundOrdersPageQuery)query;
                return ReplyAsync(context, typed, actor._service.GetOrdersAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.FundId, typed.Parameters.OrderMonth,
                    typed.Parameters.PageSize, typed.Parameters.PageToken, cancellationToken));
            },
            [typeof(GetFundOrderTradesPageQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetFundOrderTradesPageQuery)query;
                return ReplyAsync(context, typed, actor._service.GetOrderTradesAsync(
                    typed.Parameters.OrderId, typed.Parameters.PageSize, typed.Parameters.PageToken, cancellationToken));
            },
            [typeof(GetPortfolioFundStrategyReferenceCombinationsQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetPortfolioFundStrategyReferenceCombinationsQuery)query;
                return ReplyAsync(context, typed, actor._service.GetStrategyReferenceCombinationsAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.AsOfUtc, cancellationToken));
            },
            [typeof(AllocatePortfolioBusinessIdQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (AllocatePortfolioBusinessIdQuery)query;
                return ReplyAsync(context, typed, actor.AllocateAsync(typed, cancellationToken));
            },
            [typeof(GetPortfolioFinancialPolicyQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetPortfolioFinancialPolicyQuery)query;
                return ReplyAsync(context, typed, actor._service.GetPolicyAsync(
                    typed.Parameters.PolicyId, typed.Parameters.PolicyVersion, cancellationToken));
            },
            [typeof(GetPortfolioFinancialPoliciesQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetPortfolioFinancialPoliciesQuery)query;
                return ReplyAsync(context, typed, actor._service.GetPoliciesAsync(
                    typed.Parameters.PortfolioId, typed.Parameters.PageSize, cancellationToken));
            },
            [typeof(GetActivePortfolioFinancialPolicyQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetActivePortfolioFinancialPolicyQuery)query;
                return ReplyAsync(context, typed, actor._service.GetActivePolicyAsync(
                    typed.Parameters.PortfolioId, cancellationToken));
            },
            [typeof(GetLegacyPortfolioScopesQuery)] = static (actor, context, query, cancellationToken) =>
                ReplyAsync(context, query, actor._legacyHistory.GetScopesAsync(cancellationToken)),
            [typeof(GetLegacyFundCatalogQuery)] = static (actor, context, query, cancellationToken) =>
                ReplyAsync(context, query, actor._legacyHistory.GetCatalogAsync(cancellationToken)),
            [typeof(GetLegacyFundOrdersQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetLegacyFundOrdersQuery)query;
                return ReplyAsync(context, typed, actor._legacyHistory.GetOrdersAsync(
                    typed.Parameters.LegacyFundId, typed.Parameters.FromDate, typed.Parameters.ToDate,
                    typed.Parameters.PageSize, cancellationToken));
            },
            [typeof(GetLegacyFundOrderTradesQuery)] = static (actor, context, query, cancellationToken) =>
            {
                var typed = (GetLegacyFundOrderTradesQuery)query;
                return ReplyAsync(context, typed, actor._legacyHistory.GetOrderTradesAsync(
                    typed.Parameters.LegacyFundId, typed.Parameters.OrderId, cancellationToken));
            },
        };

    static readonly IReadOnlyDictionary<Type, QueryExceptionHandler> _exceptionMap =
        CreateQueryExceptionMap(_receiveMap.Keys, static (query, exception) => exception switch
        {
            PortfolioAuthorizationException => PortfolioErrorCodes.Unauthorized,
            PortfolioOperationalException => PortfolioErrorCodes.OperationallyDisabled,
            _ when query is AllocatePortfolioBusinessIdQuery => PortfolioErrorCodes.SequenceAllocationFailed,
            _ => PortfolioErrorCodes.ValidationFailed,
        });

    protected override ValueTask ReceiveAsync(IQueryActorContext<PortfolioQueryActor> context, IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<PortfolioQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        var request = (IPortfolioRequestMetadata)query;
        using var activity = PortfolioTelemetry.StartRequest("query", query.Subject.Verb, request);
        var allocatesIdentity = query is AllocatePortfolioBusinessIdQuery;
        operationalGuard.Demand(allocatesIdentity ? PortfolioOperation.AdministerPortfolio : PortfolioOperation.Read,
            request, mutation: allocatesIdentity);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var receive = ResolveMappedQueryHandler(query, _receiveMap);
            await receive(this, context, query, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PortfolioTelemetry.QueryDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("portfolio.operation", query.Subject.Verb),
                new KeyValuePair<string, object?>("portfolio.outcome", "completed"));
        }
    }

    async Task<ServiceResult<PortfolioBusinessIdAllocation>> AllocateAsync(
        PortfolioQuery<AllocatePortfolioBusinessIdRequest, PortfolioBusinessIdAllocation> query,
        CancellationToken cancellationToken)
    {
        var value = query.Parameters.Kind switch
        {
            PortfolioBusinessIdentityKind.Portfolio => (await RequireContext(Context).IdentityAllocator.AllocatePortfolioIdAsync(cancellationToken).ConfigureAwait(false)).Id,
            PortfolioBusinessIdentityKind.Fund => await RequireContext(Context).IdentityAllocator.AllocateFundIdAsync(cancellationToken).ConfigureAwait(false),
            PortfolioBusinessIdentityKind.Order => await RequireContext(Context).IdentityAllocator.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false),
            PortfolioBusinessIdentityKind.Trade => await RequireContext(Context).IdentityAllocator.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false),
            PortfolioBusinessIdentityKind.Policy => await RequireContext(Context).IdentityAllocator.AllocatePolicyIdAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(query), "A supported business identity kind is required."),
        };
        return new ServiceOk<PortfolioBusinessIdAllocation>(new()
        {
            Kind = query.Parameters.Kind,
            Value = value,
            CorrelationId = query.CorrelationId,
        });
    }

    protected override ValueTask OnExceptionAsync(
        IQueryActorContext<PortfolioQueryActor> context,
        ActorThreadId threadId,
        IQuery query,
        string verb,
        Exception exception) =>
        ExceptionMappedQueryAsync(context, threadId, query, verb, exception, _exceptionMap);

    static async ValueTask ReplyAsync<TResult>(
        IQueryActorContext<PortfolioQueryActor> context,
        IQuery query,
        Task<ServiceResult<TResult>> resultTask)
        where TResult : class
    {
        var result = await resultTask.ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }
}
