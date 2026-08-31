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
    public Task<ServiceResult<Guid>> CreatePortfolioAsync(PortfolioReadModel portfolio, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(portfolio.PortfolioId), "CreatePortfolio", new CreatePortfolioPayload(portfolio, idempotencyKey), PortfolioErrorCodes.ValidationFailed, cancellationToken, IdempotentCommandId.Create(idempotencyKey, portfolio));
    public Task<ServiceResult<Guid>> AddPortfolioVersionAsync(PortfolioReadModel portfolio, long expectedVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(portfolio.PortfolioId), "AddPortfolioVersion", new AddPortfolioVersionPayload(portfolio, expectedVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    public Task<ServiceResult<Guid>> ChangePortfolioStateAsync(PortfolioId portfolioId, long expectedVersion, PortfolioOperatingState state, string reason, CancellationToken cancellationToken = default) =>
        Send(portfolioId, "ChangePortfolioOperatingState", new ChangePortfolioStatePayload(expectedVersion, state, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<Guid>> AddFundAsync(PortfolioFundId fundId, long expectedPortfolioVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(fundId.PortfolioId), "AddFundToPortfolio", new AddFundPayload(fundId, expectedPortfolioVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    public Task<ServiceResult<Guid>> DelegateAllocationAsync(FundAllocationReadModel allocation, long expectedPortfolioVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(allocation.PortfolioId), "DelegateFundAllocation", new DelegateAllocationPayload(allocation, expectedPortfolioVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);
    public Task<ServiceResult<Guid>> DelegateRiskEnvelopeAsync(FundRiskEnvelopeReadModel envelope, long expectedPortfolioVersion, CancellationToken cancellationToken = default) =>
        Send(new PortfolioId(envelope.PortfolioId), "DelegateFundRiskEnvelope", new DelegateRiskEnvelopePayload(envelope, expectedPortfolioVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);
    public Task<ServiceResult<Guid>> RetirePortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        Send(portfolioId, "RetirePortfolio", new RetirePortfolioPayload(expectedVersion, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<Guid>> DeleteDraftPortfolioAsync(PortfolioId portfolioId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        Send(portfolioId, "DeleteDraftPortfolio", new DeleteDraftPortfolioPayload(expectedVersion, reason), PortfolioErrorCodes.DraftDeletionNotAllowed, cancellationToken);

    async Task<ServiceResult<Guid>> Send<TPayload>(PortfolioId id, string verb, TPayload payload, int errorCode, CancellationToken cancellationToken, Guid? commandId = null)
    {
        var subject = new ActorSubject(ActorType.Command, PortfolioCommandSubjects.PortfolioActor, verb, id.Format());
        var command = new PortfolioCommand<TPayload, PortfolioId>
        {
            CommandId = commandId ?? Guid.NewGuid(), Subject = subject, EntityId = id, ErrorCode = errorCode, Payload = payload,
            CorrelationId = PortfolioRequestCorrelation.CurrentOrNew(), RequestedOnUtc = DateTime.UtcNow,
        };
        try { return await RequestCommandAsync(command, id, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(errorCode, ex.Message); }
    }
}

public sealed class PortfolioFundCommandApi(IActorProducer actorProducer, IPortfolioQueryApi? queries = null) : NatsClientApi(actorProducer), IPortfolioFundCommandApi
{
    public Task<ServiceResult<Guid>> CreateFundMandateAsync(FundMandateReadModel mandate, Guid idempotencyKey, CancellationToken cancellationToken = default) =>
        Send(new(mandate.PortfolioId, mandate.FundId), "CreateFundMandate", new CreateFundMandatePayload(mandate, idempotencyKey), PortfolioErrorCodes.ValidationFailed, cancellationToken, IdempotentCommandId.Create(idempotencyKey, mandate));
    public Task<ServiceResult<Guid>> AddFundMandateVersionAsync(FundMandateReadModel mandate, long expectedVersion, CancellationToken cancellationToken = default) =>
        Send(new(mandate.PortfolioId, mandate.FundId), "AddFundMandateVersion", new AddFundMandateVersionPayload(mandate, expectedVersion), PortfolioErrorCodes.VersionConflict, cancellationToken);
    public Task<ServiceResult<Guid>> ChangeFundStateAsync(PortfolioFundId fundId, long expectedVersion, FundOperatingState state, string reason, CancellationToken cancellationToken = default) =>
        Send(fundId, "ChangeFundOperatingState", new ChangeFundStatePayload(expectedVersion, state, reason), PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<Guid>> AssignTradeTemplateAsync(FundTradeTemplateAssignmentReadModel assignment, long expectedVersion, CancellationToken cancellationToken = default) =>
        Send(new(assignment.PortfolioId, assignment.FundId), "AssignTradeTemplate", new AssignTradeTemplatePayload(assignment, expectedVersion), PortfolioErrorCodes.ValidationFailed, cancellationToken);

    public async Task<ServiceResult<FundCompositionReservationResult>> CreateManualOrderAsync(CreateManualFundOrderRequest request, CancellationToken cancellationToken = default)
    {
        var alreadyProjected = queries is not null && await FindManualOrderAsync(request, cancellationToken).ConfigureAwait(false) is not null;
        var acknowledged = await Send(new(request.PortfolioId, request.FundId), "CreateManualFundOrder",
            new CreateManualFundOrderPayload(request), PortfolioErrorCodes.ValidationFailed, cancellationToken,
            IdempotentCommandId.Create(request.IdempotencyKey, request)).ConfigureAwait(false);
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

    public async Task<ServiceResult<FundCompositionReservationResult>> ReserveCompositionAsync(ReserveFundOrderCompositionRequest request, PortfolioFundStrategySnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var wasAlreadyProjected = queries is not null && await FindReservationAsync(request, cancellationToken).ConfigureAwait(false) is not null;
        var acknowledged = await Send(new(request.PortfolioId, request.FundId), "ReserveFundOrderComposition", new ReserveCompositionPayload(request, snapshot), PortfolioErrorCodes.IdempotencyConflict, cancellationToken).ConfigureAwait(false);
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
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), "MarkFundOrderComposing", new MarkComposingPayload(orderId, expectedVersion, invocationId), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<FundOrderProjectionReadModel>> RecordComposedAsync(PortfolioFundOrderId orderId, long expectedVersion, OrderCompositionResultReference result, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), "RecordFundOrderComposed", new RecordComposedPayload(orderId, expectedVersion, result), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.ResultMismatch, cancellationToken);
    public Task<ServiceResult<FundOrderProjectionReadModel>> RecordRiskOutcomeAsync(PortfolioFundOrderId orderId, long expectedVersion, RiskManagementResultReference result, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), "RecordFundOrderRiskOutcome", new RecordRiskOutcomePayload(orderId, expectedVersion, result), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.ResultMismatch, cancellationToken);
    public Task<ServiceResult<FundOrderProjectionReadModel>> CancelCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), "CancelFundOrderComposition", new StopCompositionPayload(orderId, expectedVersion, reason), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, cancellationToken);
    public Task<ServiceResult<FundOrderProjectionReadModel>> ExpireCompositionAsync(PortfolioFundOrderId orderId, long expectedVersion, string reason, CancellationToken cancellationToken = default) =>
        SendAndReadOrder(new(orderId.PortfolioId, orderId.FundId), "ExpireFundOrderComposition", new StopCompositionPayload(orderId, expectedVersion, reason), orderId.OrderId, expectedVersion + 1, PortfolioErrorCodes.InvalidStateTransition, cancellationToken);

    async Task<ServiceResult<Guid>> Send<TPayload>(PortfolioFundId id, string verb, TPayload payload, int errorCode, CancellationToken cancellationToken, Guid? commandId = null)
    {
        var subject = new ActorSubject(ActorType.Command, PortfolioCommandSubjects.FundActor, verb, id.Format());
        var command = new PortfolioCommand<TPayload, PortfolioFundId>
        {
            CommandId = commandId ?? Guid.NewGuid(), Subject = subject, EntityId = id, ErrorCode = errorCode, Payload = payload,
            CorrelationId = PortfolioRequestCorrelation.CurrentOrNew(), RequestedOnUtc = DateTime.UtcNow,
        };
        try { return await RequestCommandAsync(command, id, cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new ServiceFailed<Guid>(errorCode, ex.Message); }
    }

    async Task<ServiceResult<FundOrderProjectionReadModel>> SendAndReadOrder<TPayload>(PortfolioFundId id, string verb, TPayload payload, int orderId, long minimumVersion, int errorCode, CancellationToken cancellationToken)
    {
        var acknowledged = await Send(id, verb, payload, errorCode, cancellationToken).ConfigureAwait(false);
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
