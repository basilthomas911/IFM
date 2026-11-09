using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class PortfolioFundCommandApi(IActorProducer actorProducer, IPortfolioQueryApi? queries = null,
    IPortfolioIdentityApi? identity = null) : NatsClientApi(actorProducer), IPortfolioFundCommandApi
{
    readonly IPortfolioIdentityApi _identity = identity ?? new PortfolioIdentityApi(actorProducer);
    static PortfolioAccessContext AdministratorAccess => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}");
    static PortfolioAccessContext WorkflowAccess => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Workflow("strategy-workflow");
    public Task<ServiceResult<Guid>> CreateFundMandateAsync(FundMandateReadModel mandate, Guid idempotencyKey, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(mandate);
        return Send(new(mandate.PortfolioId, mandate.FundId), CreateFundMandateCommand.Verb, new CreateFundMandateCommand(mandate, idempotencyKey), PortfolioErrorCodes.ValidationFailed, cancellationToken, IdempotentCommandId.Create(idempotencyKey, mandate));
    }
    public Task<ServiceResult<Guid>> AddFundMandateVersionAsync(FundMandateReadModel mandate, long expectedVersion, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(mandate);
        return Send(new(mandate.PortfolioId, mandate.FundId), AddFundMandateVersionCommand.Verb, new AddFundMandateVersionCommand(mandate, expectedVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    }
    public Task<ServiceResult<Guid>> ChangeFundStateAsync(PortfolioFundId fundId, long expectedVersion, FundOperatingState state, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(fundId);
        return Send(fundId, ChangeFundOperatingStateCommand.Verb, new ChangeFundOperatingStateCommand(expectedVersion, state, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    }
    public Task<ServiceResult<Guid>> AssignTradeTemplateAsync(FundTradeTemplateAssignmentReadModel assignment, long expectedVersion, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return Send(new(assignment.PortfolioId, assignment.FundId), AssignTradeTemplateCommand.Verb, new AssignTradeTemplateCommand(assignment, expectedVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);
    }

    public async Task<ServiceResult<FundCompositionReservationResult>> CreateManualOrderAsync(CreateManualFundOrderRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var alreadyProjected = queries is not null && await FindManualOrderAsync(request, cancellationToken).ConfigureAwait(false) is not null;
        var command = new CreateManualFundOrderCommand(request);
        var fund = new PortfolioFundId(request.PortfolioId, request.FundId);
        var commandId = IdempotentCommandId.Create(request.IdempotencyKey, request);
        if (!alreadyProjected)
        {
            if (queries is null)
                return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.Unavailable, "Manual Fund orders require the Portfolio query API.");
            using var scope = PortfolioAccessScope.Push(AdministratorAccess);
            var parent = await ReadActiveParentAsync(queries, request.PortfolioId, cancellationToken).ConfigureAwait(false);
            if (parent is not { Portfolio: { } portfolio, Revision: var revision } || portfolio.PortfolioVersion != request.PortfolioVersion)
                return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.VersionConflict,
                    "Manual draft Portfolio version is stale, its projection changed during preparation, or the Portfolio is not active.");
            var operationId = FundOrderPreparation.AllocationOperationId(fund, commandId, PortfolioBusinessIdentityKind.Order, 0);
            var order = await _identity.AllocateForOperationAsync(PortfolioBusinessIdentityKind.Order, operationId, cancellationToken).ConfigureAwait(false);
            if (!order.Success || order.Value is not { Value: > 0 } allocated)
                return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.SequenceAllocationFailed, order.ErrorMessage);
            command = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(command, command with { Preparation = new() { OrderId = allocated.Value, Parent = portfolio, ParentSourceRevision = revision } });
        }
        var acknowledged = await Send(fund, CreateManualFundOrderCommand.Verb,
            command, PortfolioErrorCodes.ValidationFailed, cancellationToken,
            commandId, AdministratorAccess).ConfigureAwait(false);
        if (!acknowledged.Success)
            return new ServiceFailed<FundCompositionReservationResult>(acknowledged.ErrorCode, acknowledged.ErrorMessage);
        if (queries is null)
            return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.Unavailable, "Portfolio query API is required to observe the committed manual order.");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var projected = await FindManualOrderAsync(request, cancellationToken).ConfigureAwait(false);
            if (projected is not null)
                return new ServiceOk<FundCompositionReservationResult>(projected with
                {
                    Disposition = alreadyProjected ? ReservationDisposition.IdempotentReplay : ReservationDisposition.Committed,
                });
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.Unavailable, "Manual order committed but its projection was not visible before the bounded query timeout.");
    }

    static async Task<(PortfolioReadModel Portfolio, long Revision)?> ReadActiveParentAsync(
        IPortfolioQueryApi queries, int portfolioId, CancellationToken cancellationToken)
    {
        var before = await queries.GetPortfolioRevisionAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        var parent = await queries.GetPortfolioAsync(portfolioId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var after = await queries.GetPortfolioRevisionAsync(portfolioId, cancellationToken).ConfigureAwait(false);
        return before.Success && after.Success && before.Value is { Revision: > 0 } revision && revision == after.Value
            && parent.Success && parent.Value is { OperatingState: PortfolioOperatingState.Active } portfolio
            ? (portfolio, revision.Revision) : null;
    }

    async Task<FundCompositionReservationResult?> FindManualOrderAsync(CreateManualFundOrderRequest request, CancellationToken cancellationToken)
    {
        if (queries is null) return null;
        var month = new DateOnly(request.RequestedAtUtc.Year, request.RequestedAtUtc.Month, 1);
        var orders = await queries.GetOrdersAsync(request.PortfolioId, request.FundId, month, 200, cancellationToken: cancellationToken).ConfigureAwait(false);
        var order = orders.Success && orders.Value is not null
            ? orders.Value.Items.SingleOrDefault(x => x.IdempotencyKey == request.IdempotencyKey)
            : null;
        return order is null ? null : new FundCompositionReservationResult
        {
            Order = order,
            Trades = [],
            AggregateVersion = order.AggregateVersion,
            CommittedOnUtc = order.CreatedOnUtc,
            Disposition = ReservationDisposition.Committed,
            CanonicalRequestSha256 = order.CanonicalRequestHash,
        };
    }

    /// <summary>
    /// Adds a trade to a manually managed Portfolio fund order and waits for its durable projection.
    /// </summary>
    /// <param name="request">The manual trade addition request.</param>
    /// <param name="cancellationToken">A token that cancels command publication or projection polling.</param>
    /// <returns>The committed order composition, including the added trade.</returns>
    public async Task<ServiceResult<FundCompositionReservationResult>> AddManualTradeAsync(
        AddManualFundOrderTradeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var acknowledged = await Send(
            new(request.PortfolioId, request.FundId),
            AddManualFundOrderTradeCommand.Verb,
            new AddManualFundOrderTradeCommand(request),
            PortfolioErrorCodes.VersionConflict,
            cancellationToken,
            access: AdministratorAccess).ConfigureAwait(false);
        if (!acknowledged.Success)
            return new ServiceFailed<FundCompositionReservationResult>(acknowledged.ErrorCode, acknowledged.ErrorMessage);
        if (queries is null)
            return new ServiceFailed<FundCompositionReservationResult>(
                PortfolioErrorCodes.Unavailable,
                "Portfolio query API is required to observe the committed manual trade.");

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var order = await queries.GetOrderAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
            var trades = await queries.GetOrderTradesAsync(
                request.OrderId,
                200,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (order.Success && order.Value is not null &&
                order.Value.AggregateVersion >= request.ExpectedOrderVersion + 1 &&
                trades.Success && trades.Value is not null &&
                trades.Value.Items.Any(x => x.TradeId == request.TradeId))
            {
                return new ServiceOk<FundCompositionReservationResult>(new()
                {
                    Order = order.Value,
                    Trades = trades.Value.Items,
                    AggregateVersion = order.Value.AggregateVersion,
                    CommittedOnUtc = DateTime.UtcNow,
                    Disposition = ReservationDisposition.Committed,
                    CanonicalRequestSha256 = order.Value.CanonicalRequestHash,
                });
            }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        return new ServiceFailed<FundCompositionReservationResult>(
            PortfolioErrorCodes.Unavailable,
            "Manual trade committed but its projection was not visible before the bounded query timeout.");
    }

    /// <summary>Removes an economically inactive trade from a manual Portfolio Fund order.</summary>
    /// <param name="request">The scoped trade-removal request.</param>
    /// <param name="cancellationToken">A token that cancels publication or projection polling.</param>
    /// <returns>The committed canonical order composition.</returns>
    public Task<ServiceResult<FundCompositionReservationResult>> RemoveManualTradeAsync(
        ManualFundOrderTradeMutationRequest request,
        CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendManualMutationAsync(
            new(request.PortfolioId, request.FundId),
            RemoveManualFundOrderTradeCommand.Verb,
            new RemoveManualFundOrderTradeCommand(request),
            request.OrderId,
            request.ExpectedOrderVersion + 1,
            trades => trades.All(x => x.TradeId != request.TradeId),
            cancellationToken);
    }

    /// <summary>Deletes an empty draft manual Portfolio Fund order and waits for its projection to disappear.</summary>
    /// <param name="request">The scoped order-deletion request.</param>
    /// <param name="cancellationToken">A token that cancels command publication or projection polling.</param>
    /// <returns>The accepted deletion command identifier.</returns>
    public async Task<ServiceResult<Guid>> DeleteManualOrderAsync(
        ManualFundOrderMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var acknowledged = await Send(
            new(request.PortfolioId, request.FundId),
            DeleteManualFundOrderCommand.Verb,
            new DeleteManualFundOrderCommand(request),
            PortfolioErrorCodes.VersionConflict,
            cancellationToken,
            access: AdministratorAccess).ConfigureAwait(false);
        if (!acknowledged.Success || queries is null)
            return acknowledged;

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var order = await queries.GetOrderAsync(request.OrderId, cancellationToken).ConfigureAwait(false);
            if (!order.Success || order.Value is null)
                return acknowledged;
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        return new ServiceFailed<Guid>(
            PortfolioErrorCodes.Unavailable,
            "Manual order was deleted but its projection remained visible beyond the bounded query timeout.");
    }
    async Task<ServiceResult<FundCompositionReservationResult>> SendManualMutationAsync<TCommand>(
        PortfolioFundId fundId,
        string verb,
        TCommand message,
        int orderId,
        long minimumVersion,
        Func<IReadOnlyList<FundOrderTradeProjectionReadModel>, bool> tradeCondition,
        CancellationToken cancellationToken,
        string? requiredStatus = null)
    {
        var acknowledged = await Send(
            fundId, verb, message, PortfolioErrorCodes.VersionConflict, cancellationToken,
            access: AdministratorAccess).ConfigureAwait(false);
        if (!acknowledged.Success)
            return new ServiceFailed<FundCompositionReservationResult>(
                acknowledged.ErrorCode, acknowledged.ErrorMessage);
        if (queries is null)
            return new ServiceFailed<FundCompositionReservationResult>(
                PortfolioErrorCodes.Unavailable,
                "Portfolio query API is required to observe the committed manual-order mutation.");

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var order = await queries.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            var trades = await queries.GetOrderTradesAsync(
                orderId, 200, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (order.Success && order.Value is not null &&
                order.Value.AggregateVersion >= minimumVersion &&
                (requiredStatus is null || string.Equals(order.Value.Status,requiredStatus,StringComparison.Ordinal)) &&
                trades.Success && trades.Value is not null &&
                tradeCondition(trades.Value.Items))
            {
                return new ServiceOk<FundCompositionReservationResult>(new()
                {
                    Order = order.Value,
                    Trades = trades.Value.Items,
                    AggregateVersion = order.Value.AggregateVersion,
                    CommittedOnUtc = DateTime.UtcNow,
                    Disposition = ReservationDisposition.Committed,
                    CanonicalRequestSha256 = order.Value.CanonicalRequestHash,
                });
            }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        return new ServiceFailed<FundCompositionReservationResult>(
            PortfolioErrorCodes.Unavailable,
            "Manual-order mutation committed but its projection was not visible before the bounded query timeout.");
    }

    public async Task<ServiceResult<FundCompositionReservationResult>> ReserveCompositionAsync(ReserveFundOrderCompositionRequest request, PortfolioFundStrategySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var wasAlreadyProjected = queries is not null && await FindReservationAsync(request, cancellationToken).ConfigureAwait(false) is not null;
        var command = new ReserveFundOrderCompositionCommand(request, snapshot);
        var fund = new PortfolioFundId(request.PortfolioId, request.FundId);
        var commandId = IdempotentCommandId.Create(request.IdempotencyKey, request);
        if (!wasAlreadyProjected)
        {
            using var scope = PortfolioAccessScope.Push(WorkflowAccess);
            var orderOperation = FundOrderPreparation.AllocationOperationId(fund, commandId, PortfolioBusinessIdentityKind.Order, 0);
            var order = await _identity.AllocateForOperationAsync(PortfolioBusinessIdentityKind.Order, orderOperation, cancellationToken).ConfigureAwait(false);
            if (!order.Success || order.Value is not { Value: > 0 } allocated)
                return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.SequenceAllocationFailed, order.ErrorMessage);
            var trades = new int[request.TradeInstructions.Length];
            for (var index = 0; index < trades.Length; index++)
            {
                var tradeOperation = FundOrderPreparation.AllocationOperationId(fund, commandId, PortfolioBusinessIdentityKind.Trade, index);
                var trade = await _identity.AllocateForOperationAsync(PortfolioBusinessIdentityKind.Trade, tradeOperation, cancellationToken).ConfigureAwait(false);
                if (!trade.Success || trade.Value is not { Value: > 0 } tradeId)
                    return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.SequenceAllocationFailed, trade.ErrorMessage);
                trades[index] = tradeId.Value;
            }
            command = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(command, command with { Preparation = new() { OrderId = allocated.Value, TradeIds = trades } });
        }
        var acknowledged = await Send(fund, ReserveFundOrderCompositionCommand.Verb, command, PortfolioErrorCodes.IdempotencyConflict, cancellationToken, commandId, WorkflowAccess).ConfigureAwait(false);
        if (!acknowledged.Success) return new ServiceFailed<FundCompositionReservationResult>(acknowledged.ErrorCode, acknowledged.ErrorMessage);
        if (queries is null) return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.Unavailable, "Portfolio query API is required to observe the committed reservation.");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var projected = await FindReservationAsync(request, cancellationToken).ConfigureAwait(false);
            if (projected is not null)
            {
                return new ServiceOk<FundCompositionReservationResult>(projected with
                {
                    Disposition = wasAlreadyProjected ? ReservationDisposition.IdempotentReplay : ReservationDisposition.Committed,
                });
            }
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        return new ServiceFailed<FundCompositionReservationResult>(PortfolioErrorCodes.Unavailable, "Reservation committed but its projection was not visible before the bounded query timeout.");
    }

    async Task<FundCompositionReservationResult?> FindReservationAsync(ReserveFundOrderCompositionRequest request, CancellationToken cancellationToken)
    {
        if (queries is null) return null;
        var workflow = await queries.GetCompositionByWorkflowAsync(request.WorkflowId, cancellationToken).ConfigureAwait(false);
        if (!workflow.Success || workflow.Value is null) return null;
        foreach (var orderId in workflow.Value.Where(x => x.PortfolioId == request.PortfolioId && x.FundId == request.FundId).Select(reference => reference.OrderId))
        {
            var order = await queries.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (!order.Success || order.Value is null || order.Value.IdempotencyKey != request.IdempotencyKey) continue;
            var trades = await queries.GetOrderTradesAsync(orderId, 200, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!trades.Success || trades.Value is null) continue;
            return new FundCompositionReservationResult
            {
                Order = order.Value,
                Trades = trades.Value.Items,
                AggregateVersion = order.Value.AggregateVersion,
                CommittedOnUtc = order.Value.CreatedOnUtc,
                Disposition = ReservationDisposition.Committed,
                CanonicalRequestSha256 = order.Value.CanonicalRequestHash,
            };
        }
        return null;
    }

    public Task<ServiceResult<FundOrderProjectionReadModel>> MarkComposingAsync(PortfolioFundOrderId orderId, long expectedVersion, Guid invocationId, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(orderId);
        return SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), MarkFundOrderComposingCommand.Verb, new MarkFundOrderComposingCommand(orderId, expectedVersion, invocationId), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, WorkflowAccess, null, cancellationToken);
    }
    public Task<ServiceResult<FundOrderProjectionReadModel>> RecordComposedAsync(PortfolioFundOrderId orderId, long expectedVersion, OrderCompositionResultReference result, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(orderId);
        return SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), RecordFundOrderComposedCommand.Verb, new RecordFundOrderComposedCommand(orderId, expectedVersion, result), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.ResultMismatch, WorkflowAccess, null, cancellationToken);
    }
    public Task<ServiceResult<FundOrderProjectionReadModel>> AuthorizeRiskAsync(Guid commandId, PortfolioFundOrderId orderId, long expectedVersion,
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.FundRiskAuthorizationReference authorization, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(orderId);
        if (commandId == Guid.Empty) throw new ArgumentException("A stable authorization CommandId is required.", nameof(commandId));
        return SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), AuthorizeFundOrderRiskCommand.Verb,
            new AuthorizeFundOrderRiskCommand(orderId, expectedVersion, authorization), orderId.OrderId, expectedVersion + 1,
            PortfolioErrorCodes.ResultMismatch, WorkflowAccess, commandId, cancellationToken);
    }
    public Task<ServiceResult<FundOrderProjectionReadModel>> RecordRiskOutcomeAsync(PortfolioFundOrderId orderId, long expectedVersion, RiskManagementResultReference result, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(orderId);
        return SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), RecordFundOrderRiskOutcomeCommand.Verb, new RecordFundOrderRiskOutcomeCommand(orderId, expectedVersion, result), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.ResultMismatch, WorkflowAccess, null, cancellationToken);
    }
    public Task<ServiceResult<FundOrderProjectionReadModel>> CancelCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(orderId);
        return SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), CancelFundOrderCompositionCommand.Verb, new CancelFundOrderCompositionCommand(orderId, expectedVersion, reason), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, AdministratorAccess, null, cancellationToken);
    }
    public Task<ServiceResult<FundOrderProjectionReadModel>> ExpireCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default) 
    {
        ArgumentNullException.ThrowIfNull(orderId);
        return SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), ExpireFundOrderCompositionCommand.Verb, new ExpireFundOrderCompositionCommand(orderId, expectedVersion, reason), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, WorkflowAccess, null, cancellationToken);
    }

    async Task<ServiceResult<Guid>> Send<TCommand>(PortfolioFundId id, string verb, TCommand message, int errorCode, CancellationToken cancellationToken, Guid? commandId = null, PortfolioAccessContext? access = null)
    {
        var subject = new ActorSubject(ActorType.Command, CreateFundMandateCommand.Actor, verb, id.Format());
        var actualCommandId = commandId ?? Guid.NewGuid();
        var correlationId = PortfolioRequestCorrelation.CurrentOrNew();
        var requestedOnUtc = DateTime.UtcNow;
        var actualAccess = access ?? AdministratorAccess;
        object command = message switch
        {
            CreateFundMandateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            AddFundMandateVersionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            ChangeFundOperatingStateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            AssignTradeTemplateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            ReserveFundOrderCompositionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            CreateManualFundOrderCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            AddManualFundOrderTradeCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            MarkFundOrderComposingCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            RecordFundOrderComposedCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            RemoveManualFundOrderTradeCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            DeleteManualFundOrderCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            RecordFundOrderRiskOutcomeCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            AuthorizeFundOrderRiskCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            SynchronizeFundRiskOutcomeCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            CancelFundOrderCompositionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            ExpireFundOrderCompositionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.CopyCommandIfSameIdentity(value, value with { CommandId = actualCommandId, Subject = subject, EntityId = id, ErrorCode = errorCode, CorrelationId = correlationId, RequestedOnUtc = requestedOnUtc, Access = actualAccess }),
            _ => throw new InvalidOperationException($"Unsupported Portfolio Fund command message {typeof(TCommand).FullName}."),
        };
        try
        {
            if (command is CreateFundMandateCommand or AddFundMandateVersionCommand or AssignTradeTemplateCommand
                or ChangeFundOperatingStateCommand { State: FundOperatingState.Active })
            {
                var reference = ValidatePortfolioReferenceCommand.ForOwnerCommand(actualCommandId) with { Access = actualAccess };
                FundActivationPreparation? activation = null;
                if (command is ChangeFundOperatingStateCommand { State: FundOperatingState.Active } ||
                    command is AddFundMandateVersionCommand { Mandate.OperatingState: FundOperatingState.Active })
                {
                    if (queries is null)
                        return new ServiceFailed<Guid>(PortfolioErrorCodes.Unavailable, "Fund activation requires the Portfolio query API.");
                    var parent = await ReadActiveParentAsync(queries, id.PortfolioId, cancellationToken).ConfigureAwait(false);
                    if (parent is not { Portfolio: { } portfolio, Revision: var revision })
                        return new ServiceFailed<Guid>(PortfolioErrorCodes.VersionConflict, "The active parent Portfolio changed or is unavailable.");
                    var fund = await queries.GetFundAsync(id.PortfolioId, id.FundId, cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (!fund.Success || fund.Value is not { } current)
                        return new ServiceFailed<Guid>(PortfolioErrorCodes.VersionConflict, "The current Fund mandate is unavailable for activation.");
                    var assignments = await queries.GetAssignmentsAsync(id.PortfolioId, id.FundId, current.FundMandateVersion, cancellationToken).ConfigureAwait(false);
                    if (!assignments.Success || assignments.Value is null)
                        return new ServiceFailed<Guid>(PortfolioErrorCodes.VersionConflict, "Fund assignments are unavailable for activation.");
                    activation = new() { ParentSourceRevision = revision, ParentPortfolioVersion = portfolio.PortfolioVersion };
                    reference = global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(reference, reference with { ActivationAssignments = assignments.Value.Where(assignment => assignment.Enabled).ToArray() });
                }
                var preparation = command switch
                {
                    CreateFundMandateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(reference, reference with { Mandate = value.Mandate }),
                    AddFundMandateVersionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(reference, reference with { Mandate = value.Mandate }),
                    AssignTradeTemplateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(reference, reference with { Assignment = value.Assignment }),
                    ChangeFundOperatingStateCommand when activation is not null => reference,
                    _ => null
                };
                if (preparation is not null)
                {
                    var receipt = await ValidatePortfolioReferenceAsync(preparation, cancellationToken).ConfigureAwait(false);
                    if (!receipt.Success) return receipt;
                    command = command switch
                    {
                        CreateFundMandateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with { ReferencePreparationId = receipt.Value }),
                        AddFundMandateVersionCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with { ReferencePreparationId = receipt.Value, ActivationPreparation = activation }),
                        AssignTradeTemplateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with { ReferencePreparationId = receipt.Value }),
                        ChangeFundOperatingStateCommand value => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with { ReferencePreparationId = receipt.Value, ActivationPreparation = activation }),
                        _ => command
                    };
                }
            }
            return await RequestCommandAsync((dynamic)command, id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(errorCode, ex.Message); }
    }

    async Task<ServiceResult<FundOrderProjectionReadModel>> SendAndReadOrder<TCommand>(PortfolioFundId id, string verb, TCommand message, int orderId, long minimumVersion, int errorCode, PortfolioAccessContext access, Guid? commandId, CancellationToken cancellationToken)
    {
        var acknowledged = await Send(id, verb, message, errorCode, cancellationToken, commandId, access: access).ConfigureAwait(false);
        if (!acknowledged.Success) return new ServiceFailed<FundOrderProjectionReadModel>(acknowledged.ErrorCode, acknowledged.ErrorMessage);
        if (queries is null) return new ServiceFailed<FundOrderProjectionReadModel>(PortfolioErrorCodes.Unavailable, "Portfolio query API is required to observe the committed order state.");
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var order = await queries.GetOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
            if (order.Success && order.Value is not null && order.Value.AggregateVersion >= minimumVersion) return order;
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
        return new ServiceFailed<FundOrderProjectionReadModel>(PortfolioErrorCodes.Unavailable, "Command committed but its order projection was not visible before the bounded query timeout.");
    }
}
