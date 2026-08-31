using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query.Actor;

public interface IPortfolioQueryContext : IQueryActorContext<PortfolioQueryActor>
{
    IDbContextFactory DbFactory { get; }
    IPortfolioBusinessIdAllocator IdentityAllocator { get; }
    ILogger<PortfolioQueryActor> Logger { get; }
}

/// <summary>NATS-only public query boundary over PortfolioDb projections.</summary>
public sealed class PortfolioQueryActor(IQueryActorContext<PortfolioQueryActor> actorContext)
    : BaseQueryActor<PortfolioQueryActor>(actorContext, RequireContext(actorContext).Logger)
{
    public const string ActorName = PortfolioQuerySubjects.Actor;
    readonly PortfolioQueryService _service = new(
        RequireContext(actorContext).DbFactory.PortfolioDb,
        new PortfolioFundStrategyResolver());

    static IPortfolioQueryContext RequireContext(IQueryActorContext<PortfolioQueryActor> context) =>
        context as IPortfolioQueryContext
        ?? throw new ArgumentException("PortfolioQueryActor requires its Portfolio query context.", nameof(context));

    protected override IQuery ParseMessage(IQueryActorContext<PortfolioQueryActor> context, IActorMessage message) =>
        ParseMappedQuery(context, message, ParseMap);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IQuery>> ParseMap = new Dictionary<string, Func<IActorMessage, IQuery>>
    {
        ["GetPortfolio"] = x => x.AsQuery<PortfolioQuery<GetPortfolioRequest, PortfolioReadModel>, PortfolioReadModel>()!,
        ["GetPortfolioRevision"] = x => x.AsQuery<PortfolioQuery<GetPortfolioRevisionRequest, PortfolioAggregateRevision>, PortfolioAggregateRevision>()!,
        ["GetPortfolios"] = x => x.AsQuery<PortfolioQuery<GetPortfoliosRequest, PortfolioPage<PortfolioReadModel>>, PortfolioPage<PortfolioReadModel>>()!,
        ["GetFund"] = x => x.AsQuery<PortfolioQuery<GetFundRequest, FundMandateReadModel>, FundMandateReadModel>()!,
        ["GetFundRevision"] = x => x.AsQuery<PortfolioQuery<GetFundRevisionRequest, PortfolioAggregateRevision>, PortfolioAggregateRevision>()!,
        ["GetFunds"] = x => x.AsQuery<PortfolioQuery<GetFundsRequest, PortfolioPage<FundMandateReadModel>>, PortfolioPage<FundMandateReadModel>>()!,
        ["GetFundAllocation"] = x => x.AsQuery<PortfolioQuery<GetAllocationRequest, FundAllocationReadModel>, FundAllocationReadModel>()!,
        ["GetFundRiskEnvelope"] = x => x.AsQuery<PortfolioQuery<GetEnvelopeRequest, FundRiskEnvelopeReadModel>, FundRiskEnvelopeReadModel>()!,
        ["GetFundTemplateAssignments"] = x => x.AsQuery<PortfolioQuery<GetAssignmentsRequest, FundTradeTemplateAssignmentReadModel[]>, FundTradeTemplateAssignmentReadModel[]>()!,
        ["GetPortfolioFundStrategySnapshot"] = x => x.AsQuery<PortfolioQuery<GetStrategySnapshotRequest, PortfolioFundStrategySnapshot>, PortfolioFundStrategySnapshot>()!,
        ["GetFundOrderByOrderId"] = x => x.AsQuery<PortfolioQuery<GetOrderRequest, FundOrderProjectionReadModel>, FundOrderProjectionReadModel>()!,
        ["GetFundOrderTradeByTradeId"] = x => x.AsQuery<PortfolioQuery<GetTradeRequest, FundOrderTradeProjectionReadModel>, FundOrderTradeProjectionReadModel>()!,
        ["GetFundCompositionByWorkflow"] = x => x.AsQuery<PortfolioQuery<GetCompositionRequest, FundCompositionWorkflowProjectionReadModel[]>, FundCompositionWorkflowProjectionReadModel[]>()!,
        ["GetFundOrdersPage"] = x => x.AsQuery<PortfolioQuery<GetOrdersRequest, PortfolioPage<FundOrderProjectionReadModel>>, PortfolioPage<FundOrderProjectionReadModel>>()!,
        ["GetFundOrderTradesPage"] = x => x.AsQuery<PortfolioQuery<GetOrderTradesRequest, PortfolioPage<FundOrderTradeProjectionReadModel>>, PortfolioPage<FundOrderTradeProjectionReadModel>>()!,
        ["GetPortfolioFundStrategyReferenceCombinations"] = x => x.AsQuery<PortfolioQuery<GetStrategyReferenceCombinationsRequest, PortfolioFundStrategyReferenceCombination[]>, PortfolioFundStrategyReferenceCombination[]>()!,
        ["AllocatePortfolioBusinessId"] = x => x.AsQuery<PortfolioQuery<AllocatePortfolioBusinessIdRequest, PortfolioBusinessIdAllocation>, PortfolioBusinessIdAllocation>()!,
        ["GetPortfolioFinancialPolicy"] = x => x.AsQuery<PortfolioQuery<GetPolicyRequest, PortfolioFinancialPolicyReadModel>, PortfolioFinancialPolicyReadModel>()!,
        ["GetPortfolioFinancialPolicies"] = x => x.AsQuery<PortfolioQuery<GetPoliciesRequest, PortfolioPage<PortfolioFinancialPolicyReadModel>>, PortfolioPage<PortfolioFinancialPolicyReadModel>>()!,
        ["GetActivePortfolioFinancialPolicy"] = x => x.AsQuery<PortfolioQuery<GetActivePolicyRequest, PortfolioFinancialPolicyReadModel>, PortfolioFinancialPolicyReadModel>()!,
    };

    protected override ValueTask ReceiveAsync(IQueryActorContext<PortfolioQueryActor> context, IQuery query) =>
        ReceiveAsync(context, query, CancellationToken.None);

    protected override async ValueTask ReceiveAsync(IQueryActorContext<PortfolioQueryActor> context, IQuery query, CancellationToken cancellationToken)
    {
        switch (query)
        {
            case PortfolioQuery<GetPortfolioRequest, PortfolioReadModel> q:
                await Reply(context, q, await _service.GetPortfolioAsync(q.Parameters.PortfolioId, q.Parameters.Version, cancellationToken)); break;
            case PortfolioQuery<GetPortfolioRevisionRequest, PortfolioAggregateRevision> q:
                await Reply(context, q, await _service.GetPortfolioRevisionAsync(q.Parameters.PortfolioId, cancellationToken)); break;
            case PortfolioQuery<GetPortfoliosRequest, PortfolioPage<PortfolioReadModel>> q:
                await Reply(context, q, await _service.GetPortfoliosAsync(q.Parameters.State is null ? null : (PortfolioOperatingState)q.Parameters.State, q.Parameters.PageSize, q.Parameters.PageToken, cancellationToken)); break;
            case PortfolioQuery<GetFundRequest, FundMandateReadModel> q:
                await Reply(context, q, await _service.GetFundAsync(q.Parameters.PortfolioId, q.Parameters.FundId, q.Parameters.Version, cancellationToken)); break;
            case PortfolioQuery<GetFundRevisionRequest, PortfolioAggregateRevision> q:
                await Reply(context, q, await _service.GetFundRevisionAsync(q.Parameters.PortfolioId, q.Parameters.FundId, cancellationToken)); break;
            case PortfolioQuery<GetFundsRequest, PortfolioPage<FundMandateReadModel>> q:
                await Reply(context, q, await _service.GetFundsAsync(q.Parameters.PortfolioId, q.Parameters.State is null ? null : (FundOperatingState)q.Parameters.State, q.Parameters.PageSize, q.Parameters.PageToken, cancellationToken)); break;
            case PortfolioQuery<GetAllocationRequest, FundAllocationReadModel> q:
                await Reply(context, q, await _service.GetFundAllocationAsync(q.Parameters.PortfolioId, q.Parameters.FundId, cancellationToken)); break;
            case PortfolioQuery<GetEnvelopeRequest, FundRiskEnvelopeReadModel> q:
                await Reply(context, q, await _service.GetFundRiskEnvelopeAsync(q.Parameters.PortfolioId, q.Parameters.FundId, q.Parameters.AsOfUtc, cancellationToken)); break;
            case PortfolioQuery<GetAssignmentsRequest, FundTradeTemplateAssignmentReadModel[]> q:
                await Reply(context, q, await _service.GetAssignmentsAsync(q.Parameters.PortfolioId, q.Parameters.FundId, q.Parameters.MandateVersion, cancellationToken)); break;
            case PortfolioQuery<GetStrategySnapshotRequest, PortfolioFundStrategySnapshot> q:
                await Reply(context, q, await _service.GetStrategySnapshotAsync(q.Parameters.PortfolioId, q.Parameters.TradingYear, q.Parameters.DecisionHorizon, q.Parameters.UnderlyingRoot, q.Parameters.AssetType, q.Parameters.AsOfUtc, q.Parameters.WorkflowId, q.Parameters.WorkflowRevision, q.Parameters.CorrelationId, cancellationToken)); break;
            case PortfolioQuery<GetOrderRequest, FundOrderProjectionReadModel> q:
                await Reply(context, q, await _service.GetOrderAsync(q.Parameters.OrderId, cancellationToken)); break;
            case PortfolioQuery<GetTradeRequest, FundOrderTradeProjectionReadModel> q:
                await Reply(context, q, await _service.GetTradeAsync(q.Parameters.TradeId, cancellationToken)); break;
            case PortfolioQuery<GetCompositionRequest, FundCompositionWorkflowProjectionReadModel[]> q:
                await Reply(context, q, await _service.GetCompositionByWorkflowAsync(q.Parameters.WorkflowId, cancellationToken)); break;
            case PortfolioQuery<GetOrdersRequest, PortfolioPage<FundOrderProjectionReadModel>> q:
                await Reply(context, q, await _service.GetOrdersAsync(q.Parameters.PortfolioId, q.Parameters.FundId, q.Parameters.OrderMonth, q.Parameters.PageSize, q.Parameters.PageToken, cancellationToken)); break;
            case PortfolioQuery<GetOrderTradesRequest, PortfolioPage<FundOrderTradeProjectionReadModel>> q:
                await Reply(context, q, await _service.GetOrderTradesAsync(q.Parameters.OrderId, q.Parameters.PageSize, q.Parameters.PageToken, cancellationToken)); break;
            case PortfolioQuery<GetStrategyReferenceCombinationsRequest, PortfolioFundStrategyReferenceCombination[]> q:
                await Reply(context, q, await _service.GetStrategyReferenceCombinationsAsync(q.Parameters.PortfolioId, q.Parameters.AsOfUtc, cancellationToken)); break;
            case PortfolioQuery<AllocatePortfolioBusinessIdRequest, PortfolioBusinessIdAllocation> q:
                await Reply(context, q, await AllocateAsync(q, cancellationToken)); break;
            case PortfolioQuery<GetPolicyRequest, PortfolioFinancialPolicyReadModel> q:
                await Reply(context, q, await _service.GetPolicyAsync(q.Parameters.PolicyId, q.Parameters.PolicyVersion, cancellationToken)); break;
            case PortfolioQuery<GetPoliciesRequest, PortfolioPage<PortfolioFinancialPolicyReadModel>> q:
                await Reply(context, q, await _service.GetPoliciesAsync(q.Parameters.PortfolioId, q.Parameters.PageSize, cancellationToken)); break;
            case PortfolioQuery<GetActivePolicyRequest, PortfolioFinancialPolicyReadModel> q:
                await Reply(context, q, await _service.GetActivePolicyAsync(q.Parameters.PortfolioId, cancellationToken)); break;
            default: throw new InvalidOperationException($"Unsupported Portfolio query {query.GetType().Name}.");
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

    protected override async ValueTask OnExceptionAsync(IQueryActorContext<PortfolioQueryActor> context, ActorThreadId threadId, IQuery query, string verb, Exception ex)
    {
        var errorCode = query is PortfolioQuery<AllocatePortfolioBusinessIdRequest, PortfolioBusinessIdAllocation>
            ? PortfolioErrorCodes.SequenceAllocationFailed
            : PortfolioErrorCodes.ValidationFailed;
        var method = typeof(PortfolioQueryActor).GetMethod(nameof(ReplyFailure), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .MakeGenericMethod(query.GetType().GetGenericArguments()[1]);
        await (ValueTask)method.Invoke(null, [context, threadId, verb, errorCode, ex.Message])!;
    }

    static ValueTask Reply<TResult>(IQueryActorContext<PortfolioQueryActor> context, IQuery query, ServiceResult<TResult> result) =>
        context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result);

    static ValueTask ReplyFailure<TResult>(IQueryActorContext<PortfolioQueryActor> context, ActorThreadId threadId, string verb, int errorCode, string message) =>
        context.ReplyAsync(threadId, verb, new ServiceFailed<TResult>(errorCode, message));
}
