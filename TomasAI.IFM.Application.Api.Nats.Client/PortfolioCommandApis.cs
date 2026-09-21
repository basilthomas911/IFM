using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public sealed class PortfolioCommandApi(IActorProducer actorProducer) : NatsClientApi(actorProducer), IPortfolioCommandApi
{
    static PortfolioAccessContext Access => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}");
    public Task<ServiceResult<Guid>> CreatePortfolioAsync(PortfolioReadModel portfolio, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(portfolio.PortfolioId), PortfolioCommandVerbs.CreatePortfolio, new CreatePortfolioPayload(portfolio, idempotencyKey), PortfolioErrorCodes.ValidationFailed, cancellationToken, IdempotentCommandId.Create(idempotencyKey, portfolio));
    public Task<ServiceResult<Guid>> AddPortfolioVersionAsync(PortfolioReadModel portfolio, long expectedVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(portfolio.PortfolioId), PortfolioCommandVerbs.AddPortfolioVersion, new AddPortfolioVersionPayload(portfolio, expectedVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    public Task<ServiceResult<Guid>> ChangePortfolioStateAsync(PortfolioId portfolioId, long expectedVersion, PortfolioOperatingState state, string reason, CancellationToken cancellationToken = default) =>
        Send(portfolioId, PortfolioCommandVerbs.ChangePortfolioOperatingState, new ChangePortfolioStatePayload(expectedVersion, state, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<Guid>> AddFundAsync(PortfolioFundId fundId, long expectedPortfolioVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(fundId.PortfolioId), PortfolioCommandVerbs.AddFundToPortfolio, new AddFundPayload(fundId, expectedPortfolioVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    public Task<ServiceResult<Guid>> DelegateAllocationAsync(FundAllocationReadModel allocation, long expectedPortfolioVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(allocation.PortfolioId), PortfolioCommandVerbs.DelegateFundAllocation, new DelegateAllocationPayload(allocation, expectedPortfolioVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);
    public Task<ServiceResult<Guid>> DelegateRiskEnvelopeAsync(FundRiskEnvelopeReadModel envelope, long expectedPortfolioVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(envelope.PortfolioId), PortfolioCommandVerbs.DelegateFundRiskEnvelope, new DelegateRiskEnvelopePayload(envelope, expectedPortfolioVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);
    public Task<ServiceResult<Guid>> RetirePortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        Send(portfolioId, PortfolioCommandVerbs.RetirePortfolio, new RetirePortfolioPayload(expectedVersion, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<Guid>> DeleteDraftPortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        Send(portfolioId, PortfolioCommandVerbs.DeleteDraftPortfolio, new DeleteDraftPortfolioPayload(expectedVersion, reason), PortfolioErrorCodes.DraftDeletionNotAllowed, cancellationToken);

    async Task<ServiceResult<Guid>> Send<TPayload>(PortfolioId id, string verb, TPayload payload, int errorCode, CancellationToken cancellationToken, Guid? commandId = null)
    {
        var subject = new ActorSubject(ActorType.Command, PortfolioCommandSubjects.PortfolioActor, verb, id.Format());
        var command = new PortfolioCommand<TPayload, PortfolioId>
        {
            CommandId = commandId ?? Guid.NewGuid(), Subject = subject, EntityId = id, ErrorCode = errorCode, Payload = payload,
            CorrelationId = PortfolioRequestCorrelation.CurrentOrNew(), RequestedOnUtc = DateTime.UtcNow, Access = Access,
        };
        try { return await RequestCommandAsync(command, id, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(errorCode, ex.Message); }
    }
}

public sealed class PortfolioFundCommandApi(IActorProducer actorProducer, IPortfolioQueryApi? queries = null) : NatsClientApi(actorProducer), IPortfolioFundCommandApi
{
    static PortfolioAccessContext AdministratorAccess => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Administrator($"interactive:{Environment.UserName}");
    static PortfolioAccessContext WorkflowAccess => PortfolioAccessScope.Current
        ?? PortfolioAccessContext.Workflow("strategy-workflow");
    public Task<ServiceResult<Guid>> CreateFundMandateAsync(FundMandateReadModel mandate, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        Send(new(mandate.PortfolioId, mandate.FundId), PortfolioCommandVerbs.CreateFundMandate, new CreateFundMandatePayload(mandate, idempotencyKey), PortfolioErrorCodes.ValidationFailed, cancellationToken, IdempotentCommandId.Create(idempotencyKey, mandate));
    public Task<ServiceResult<Guid>> AddFundMandateVersionAsync(FundMandateReadModel mandate, long expectedVersion, CancellationToken cancellationToken = default) =>
        Send(new(mandate.PortfolioId, mandate.FundId), PortfolioCommandVerbs.AddFundMandateVersion, new AddFundMandateVersionPayload(mandate, expectedVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    public Task<ServiceResult<Guid>> ChangeFundStateAsync(PortfolioFundId fundId, long expectedVersion, FundOperatingState state, string reason, CancellationToken cancellationToken = default) =>
        Send(fundId, PortfolioCommandVerbs.ChangeFundOperatingState, new ChangeFundStatePayload(expectedVersion, state, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<Guid>> AssignTradeTemplateAsync(FundTradeTemplateAssignmentReadModel assignment, long expectedVersion, CancellationToken cancellationToken = default) =>
        Send(new(assignment.PortfolioId, assignment.FundId), PortfolioCommandVerbs.AssignTradeTemplate, new AssignTradeTemplatePayload(assignment, expectedVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);

    public async Task<ServiceResult<FundCompositionReservationResult>> CreateManualOrderAsync(CreateManualFundOrderRequest request, CancellationToken cancellationToken = default)
    {
        var alreadyProjected = queries is not null && await FindManualOrderAsync(request, cancellationToken).ConfigureAwait(false) is not null;
        var acknowledged = await Send(new(request.PortfolioId, request.FundId), PortfolioCommandVerbs.CreateManualFundOrder,
            new CreateManualFundOrderPayload(request), PortfolioErrorCodes.ValidationFailed, cancellationToken,
            IdempotentCommandId.Create(request.IdempotencyKey, request), AdministratorAccess).ConfigureAwait(false);
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
        var acknowledged = await Send(
            new(request.PortfolioId, request.FundId),
            PortfolioCommandVerbs.AddManualFundOrderTrade,
            new AddManualFundOrderTradePayload(request),
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
        CancellationToken cancellationToken = default) =>
        SendManualMutationAsync(
            new(request.PortfolioId, request.FundId),
            PortfolioCommandVerbs.RemoveManualFundOrderTrade,
            new RemoveManualFundOrderTradePayload(request),
            request.OrderId,
            request.ExpectedOrderVersion + 1,
            trades => trades.All(x => x.TradeId != request.TradeId),
            cancellationToken);

    /// <summary>Changes a trade lifecycle state on a manual Portfolio Fund order.</summary>
    /// <param name="request">The scoped trade-state mutation request.</param>
    /// <param name="cancellationToken">A token that cancels publication or projection polling.</param>
    /// <returns>The committed canonical order composition.</returns>
    public Task<ServiceResult<FundCompositionReservationResult>> ChangeManualTradeStateAsync(
        ManualFundOrderTradeMutationRequest request,
        CancellationToken cancellationToken = default) =>
        SendManualMutationAsync(
            new(request.PortfolioId, request.FundId),
            PortfolioCommandVerbs.ChangeManualFundOrderTradeState,
            new ChangeManualFundOrderTradeStatePayload(request),
            request.OrderId,
            request.ExpectedOrderVersion + 1,
            trades => trades.Any(x => x.TradeId == request.TradeId && x.TradeState == request.TradeState),
            cancellationToken);

    /// <summary>Closes a manual Portfolio Fund order after its closing trade completes.</summary>
    /// <param name="request">The scoped order-close request.</param>
    /// <param name="cancellationToken">A token that cancels publication or projection polling.</param>
    /// <returns>The committed canonical order composition.</returns>
    public Task<ServiceResult<FundCompositionReservationResult>> CloseManualOrderAsync(
        ManualFundOrderMutationRequest request,
        CancellationToken cancellationToken = default) =>
        SendManualMutationAsync(
            new(request.PortfolioId, request.FundId),
            PortfolioCommandVerbs.CloseManualFundOrder,
            new CloseManualFundOrderPayload(request),
            request.OrderId,
            request.ExpectedOrderVersion + 1,
            _ => true,
            cancellationToken,
            nameof(FundCompositionState.Executed));

    /// <summary>Deletes an empty draft manual Portfolio Fund order and waits for its projection to disappear.</summary>
    /// <param name="request">The scoped order-deletion request.</param>
    /// <param name="cancellationToken">A token that cancels command publication or projection polling.</param>
    /// <returns>The accepted deletion command identifier.</returns>
    public async Task<ServiceResult<Guid>> DeleteManualOrderAsync(
        ManualFundOrderMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var acknowledged = await Send(
            new(request.PortfolioId, request.FundId),
            PortfolioCommandVerbs.DeleteManualFundOrder,
            new DeleteManualFundOrderPayload(request),
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
    async Task<ServiceResult<FundCompositionReservationResult>> SendManualMutationAsync<TPayload>(
        PortfolioFundId fundId,
        string verb,
        TPayload payload,
        int orderId,
        long minimumVersion,
        Func<IReadOnlyList<FundOrderTradeProjectionReadModel>, bool> tradeCondition,
        CancellationToken cancellationToken,
        string? requiredStatus = null)
    {
        var acknowledged = await Send(
            fundId, verb, payload, PortfolioErrorCodes.VersionConflict, cancellationToken,
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
                (requiredStatus is null || order.Value.Status == requiredStatus) &&
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
        var wasAlreadyProjected = queries is not null && await FindReservationAsync(request, cancellationToken).ConfigureAwait(false) is not null;
        var acknowledged = await Send(new(request.PortfolioId, request.FundId), PortfolioCommandVerbs.ReserveFundOrderComposition, new ReserveCompositionPayload(request, snapshot), PortfolioErrorCodes.IdempotencyConflict, cancellationToken, access: WorkflowAccess).ConfigureAwait(false);
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
        foreach (var reference in workflow.Value.Where(x => x.PortfolioId == request.PortfolioId && x.FundId == request.FundId))
        {
            var order = await queries.GetOrderAsync(reference.OrderId, cancellationToken).ConfigureAwait(false);
            if (!order.Success || order.Value is null || order.Value.IdempotencyKey != request.IdempotencyKey) continue;
            var trades = await queries.GetOrderTradesAsync(reference.OrderId, 200, cancellationToken: cancellationToken).ConfigureAwait(false);
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

    public Task<ServiceResult<FundOrderProjectionReadModel>> MarkComposingAsync(PortfolioFundOrderId orderId, long expectedVersion, Guid invocationId, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), PortfolioCommandVerbs.MarkFundOrderComposing, new MarkComposingPayload(orderId, expectedVersion, invocationId), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, cancellationToken, WorkflowAccess);
    public Task<ServiceResult<FundOrderProjectionReadModel>> RecordComposedAsync(PortfolioFundOrderId orderId, long expectedVersion, OrderCompositionResultReference result, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), PortfolioCommandVerbs.RecordFundOrderComposed, new RecordComposedPayload(orderId, expectedVersion, result), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.ResultMismatch, cancellationToken, WorkflowAccess);
    public Task<ServiceResult<FundOrderProjectionReadModel>> AuthorizeRiskAsync(Guid commandId, PortfolioFundOrderId orderId, long expectedVersion,
        TomasAI.IFM.Domain.Portfolio.Shared.Financial.FundRiskAuthorizationReference authorization, CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("A stable authorization CommandId is required.", nameof(commandId));
        return SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), PortfolioCommandVerbs.AuthorizeFundOrderRisk,
            new AuthorizeFundOrderRiskPayload(orderId, expectedVersion, authorization), orderId.OrderId, expectedVersion + 1,
            PortfolioErrorCodes.ResultMismatch, cancellationToken, WorkflowAccess, commandId);
    }
    public Task<ServiceResult<FundOrderProjectionReadModel>> RecordRiskOutcomeAsync(PortfolioFundOrderId orderId, long expectedVersion, RiskManagementResultReference result, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), PortfolioCommandVerbs.RecordFundOrderRiskOutcome, new RecordRiskOutcomePayload(orderId, expectedVersion, result), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.ResultMismatch, cancellationToken, WorkflowAccess);
    public Task<ServiceResult<FundOrderProjectionReadModel>> CancelCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), PortfolioCommandVerbs.CancelFundOrderComposition, new CancelFundOrderCompositionPayload(orderId, expectedVersion, reason), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, cancellationToken, AdministratorAccess);
    public Task<ServiceResult<FundOrderProjectionReadModel>> ExpireCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), PortfolioCommandVerbs.ExpireFundOrderComposition, new ExpireFundOrderCompositionPayload(orderId, expectedVersion, reason), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, cancellationToken, WorkflowAccess);

    async Task<ServiceResult<Guid>> Send<TPayload>(PortfolioFundId id, string verb, TPayload payload, int errorCode, CancellationToken cancellationToken, Guid? commandId = null, PortfolioAccessContext? access = null)
    {
        var subject = new ActorSubject(ActorType.Command, PortfolioCommandSubjects.FundActor, verb, id.Format());
        var command = new PortfolioCommand<TPayload, PortfolioFundId>
        {
            CommandId = commandId ?? Guid.NewGuid(), Subject = subject, EntityId = id, ErrorCode = errorCode, Payload = payload,
            CorrelationId = PortfolioRequestCorrelation.CurrentOrNew(), RequestedOnUtc = DateTime.UtcNow, Access = access ?? AdministratorAccess,
        };
        try { return await RequestCommandAsync(command, id, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(errorCode, ex.Message); }
    }

    async Task<ServiceResult<FundOrderProjectionReadModel>> SendAndReadOrder<TPayload>(PortfolioFundId id, string verb, TPayload payload, int orderId, long minimumVersion, int errorCode, CancellationToken cancellationToken, PortfolioAccessContext access, Guid? commandId = null)
    {
        var acknowledged = await Send(id, verb, payload, errorCode, cancellationToken, commandId, access: access).ConfigureAwait(false);
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

static class IdempotentCommandId
{
    public static Guid Create<T>(Guid idempotencyKey, T payload)
    {
        if (idempotencyKey == Guid.Empty) throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        var key = idempotencyKey.ToByteArray();
        var body = MessagePackSerializer.Serialize(payload);
        var input = new byte[key.Length + body.Length];
        key.CopyTo(input, 0);
        body.CopyTo(input, key.Length);
        var hash = SHA256.HashData(input);
        return new Guid(hash.AsSpan(0, 16));
    }
}
