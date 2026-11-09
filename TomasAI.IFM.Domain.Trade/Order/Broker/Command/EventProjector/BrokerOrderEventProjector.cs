using Microsoft.Extensions.Logging;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Model;
using TomasAI.IFM.Domain.Trade.Order.Execution.Command.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Command.EventProjector;
/// <summary>Dispatches only committed BrokerOrder mutation intents and records the local receipt.</summary>
public sealed class BrokerOrderEventProjector : ConventionalEventProjector<BrokerOrderCommandActor>
{
    private readonly TimeProvider businessClock;
    private static readonly TimeSpan DispatchTimeout = TimeSpan.FromSeconds(15);
    private readonly IBrokerOrderCommandContext _context;
    private readonly ImmutableArray<EventProjectionDescriptor> _descriptors;
    public BrokerOrderEventProjector(ICommandActorContext<BrokerOrderCommandActor> actorContext, EventProjectorReliabilityOptions? options = null, TimeProvider? timeProvider = null) : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource, Typed(actorContext).Logger, options)
    {
        _context = Typed(actorContext);
        businessClock = timeProvider ?? TimeProvider.System;
        _descriptors = [DescribeNotification<BrokerOrderChangedEvent, BrokerOrderId>(ProjectAsync).WithReadModelRebuild((source, _) =>
        {
            _context.ReadStore.Set(((BrokerOrderChangedEvent)source).BrokerOrderDefinition);
            return Task.CompletedTask;
        })];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(BrokerOrderChangedEvent)];

    private async Task ProjectAsync(BrokerOrderChangedEvent changed)
    {
        _context.Logger.LogInformation("OrderSubmission BrokerOrderEvent BrokerOrderId={BrokerOrderId} OperationId={OperationId} SourceEventId={SourceEventId} Status={Status} Mutation={Mutation} Observation={Observation}", changed.EntityId.Format(), changed.BrokerOrderDefinition.OperationId, changed.Id, changed.BrokerOrderDefinition.Status, changed.BrokerOrderDefinition.PendingMutation, changed.BrokerOrderDefinition.LastObservation?.Kind);
        _context.ReadStore.Set(changed.BrokerOrderDefinition);
        if (changed.BrokerOrderDefinition.LastObservation is { } observation)
        {
            await ProjectObservationAsync(changed.EntityId, observation).ConfigureAwait(false);
            return;
        }

        if (CreatePlacementRejection(changed.BrokerOrderDefinition) is { } rejection)
        {
            EnsureHandoff(await _context.ActorService.SendAsync<RejectOrderExecutionCommand, OrderExecutionId>(rejection, rejection.EntityId).ConfigureAwait(false));
            return;
        }

        // Receipt and observation events may retain the pending operation for correlation.
        // Only the committed mutation intent may call the broker again on replay.
        if (changed.IsDispatchReceipt || !IsDispatchIntent(changed.BrokerOrderDefinition))
            return;
        BrokerDispatchReceipt receipt;
        using var deadline = new CancellationTokenSource(DispatchTimeout);
        try
        {
            receipt = await DispatchAsync(changed, deadline.Token).AsTask().WaitAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is TimeoutException or IOException or OperationCanceledException)
        {
            receipt = new(BrokerDispatchOutcome.OutcomeUnknown, changed.BrokerOrderDefinition.OperationId,
                changed.EntityId.Format(), "BROKER.DISPATCH.UNCONFIRMED", error.Message, businessClock.GetUtcNow().UtcDateTime);
        }
        if (receipt.OperationId != changed.BrokerOrderDefinition.OperationId ||
            !string.Equals(receipt.BrokerOrderId, changed.EntityId.Format(), StringComparison.Ordinal))
            throw new InvalidOperationException("BROKER_ORDER.RECEIPT_IDENTITY_MISMATCH");
        _context.Logger.LogInformation("OrderSubmission BrokerDispatchReceipt BrokerOrderId={BrokerOrderId} OperationId={OperationId} Outcome={Outcome} Category={Category} Detail={Detail}", changed.EntityId.Format(), receipt.OperationId, receipt.Outcome, receipt.Category, receipt.Detail);
        var command = new RecordBrokerDispatchCommand
        {
            CommandId = DeterministicId("dispatch-receipt", changed.EntityId.Format(), changed.BrokerOrderDefinition.OperationId.ToString("N")),
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command, RecordBrokerDispatchCommand.Verb, changed.EntityId.Format()),
            EntityId = changed.EntityId,
            OperationId = receipt.OperationId,
            Outcome = (BrokerDispatchResult)receipt.Outcome,
            Category = receipt.Category,
            Detail = receipt.Detail,
            RecordedAtUtc = receipt.RecordedAtUtc.Kind == DateTimeKind.Utc ? receipt.RecordedAtUtc : businessClock.GetUtcNow().UtcDateTime
        };
        var result = await _context.ActorService.SendAsync<RecordBrokerDispatchCommand, BrokerOrderId>(command, command.EntityId).ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException($"BROKER_ORDER.RECEIPT_HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
    }

    private ValueTask<BrokerDispatchReceipt> DispatchAsync(BrokerOrderChangedEvent changed, CancellationToken cancellationToken)
    {
        var order = changed.BrokerOrderDefinition;
        return order.PendingMutation switch
        {
            BrokerMutationKind.Place => PlaceAsync(changed, cancellationToken),
            BrokerMutationKind.UpdateLimit => _context.TradeBroker.ModifyLimitAsync(new(order.Order.BrokerAccountAlias,
                changed.EntityId.Format(), order.OperationId, order.RequestedSignedNetDebitLimit ?? order.CurrentSignedNetDebitLimit, order.BrokerRevision), cancellationToken),
            BrokerMutationKind.Cancel => _context.TradeBroker.CancelAsync(new(order.Order.BrokerAccountAlias,
                changed.EntityId.Format(), order.OperationId, order.BrokerRevision), cancellationToken),
            _ => throw new InvalidOperationException("BROKER_ORDER.MUTATION_UNKNOWN")
        };
    }

    private async ValueTask<BrokerDispatchReceipt> PlaceAsync(BrokerOrderChangedEvent changed, CancellationToken cancellationToken)
    {
        if (!BrokerOrderRequestMapper.TryCreate(changed.BrokerOrderDefinition.Order, changed.EntityId.Execution.ExecutionAttemptId, changed.EntityId.ComponentId, changed.BrokerOrderDefinition.OperationId, out var request, out var reason) || request is null)
            throw new InvalidOperationException($"BROKER_ORDER.MAPPING_FAILED;{reason}");
        _context.Logger.LogInformation("OrderSubmission BrokerPlaceStarted BrokerOrderId={BrokerOrderId} OperationId={OperationId} Account={Account} Environment={Environment}", changed.EntityId.Format(), changed.BrokerOrderDefinition.OperationId, changed.BrokerOrderDefinition.Order.BrokerAccountAlias, changed.BrokerOrderDefinition.Order.BrokerEnvironment);
        try { return await _context.TradeBroker.PlaceAsync(request, cancellationToken).ConfigureAwait(false); }
        catch(Exception error) {
            _context.Logger.LogError(error,"OrderSubmission BrokerPlaceUnconfirmed BrokerOrderId={BrokerOrderId} OperationId={OperationId}; reconcile before retry",changed.EntityId.Format(),changed.BrokerOrderDefinition.OperationId);
            throw;
        }
    }

    private async ValueTask ProjectObservationAsync(BrokerOrderId brokerOrderId, BrokerOrderObservationEvidence observation)
    {
        var executionId = brokerOrderId.Execution;
        ServiceResult<Guid>? result = observation.Kind switch
        {
            BrokerOrderObservationKind.Acknowledged => await SendAcknowledgedAsync(executionId).ConfigureAwait(false),
            BrokerOrderObservationKind.Execution => await SendFillAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.Commission => await SendCommissionAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.OrderCompleted => await SendCompletedAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.Cancelled when observation.ExecutionFinal => await SendCancelledAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.Rejected => await SendRejectedAsync(executionId, observation).ConfigureAwait(false),
            _ => null
        };
        EnsureHandoff(result);
    }

    private ValueTask<ServiceResult<Guid>> SendAcknowledgedAsync(OrderExecutionId executionId)
    {
        var command = new SubmitOrderExecutionCommand
        {
            CommandId = DeterministicId("broker-acknowledged", executionId.Format()),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, SubmitOrderExecutionCommand.Verb, executionId.Format()),
            EntityId = executionId
        };
        return _context.ActorService.SendAsync<SubmitOrderExecutionCommand, OrderExecutionId>(command, executionId);
    }

    private ValueTask<ServiceResult<Guid>> SendFillAsync(OrderExecutionId executionId, BrokerOrderObservationEvidence observation)
    {
        var command = new AddOrderExecutionFillCommand
        {
            CommandId = DeterministicId("fill", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, AddOrderExecutionFillCommand.Verb, executionId.Format()),
            EntityId = executionId,
            Fill = new ExecutionFillEvidence
            {
                ExecutionFillId = observation.ObservationId,
                ExecutionAttemptId = executionId.ExecutionAttemptId,
                ComponentId = observation.ComponentId,
                TradeLegId = observation.LegId,
                ContractId = observation.ContractId,
                SignedQuantity = observation.SignedQuantity,
                Price = observation.Price,
                FilledAtUtc = observation.OccurredAtUtc,
                ExternalExecutionId = observation.ExternalExecutionId
            }
        };
        return _context.ActorService.SendAsync<AddOrderExecutionFillCommand, OrderExecutionId>(command, executionId);
    }

    private ValueTask<ServiceResult<Guid>> SendCommissionAsync(OrderExecutionId executionId, BrokerOrderObservationEvidence observation)
    {
        var command = new UpdateOrderExecutionFillCostCommand
        {
            CommandId = DeterministicId("commission", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, UpdateOrderExecutionFillCostCommand.Verb, executionId.Format()),
            EntityId = executionId,
            ExternalExecutionId = observation.ExternalExecutionId,
            Commission = observation.Commission
        };
        return _context.ActorService.SendAsync<UpdateOrderExecutionFillCostCommand, OrderExecutionId>(command, executionId);
    }

    private ValueTask<ServiceResult<Guid>> SendCompletedAsync(OrderExecutionId executionId, BrokerOrderObservationEvidence observation)
    {
        var command = new AcceptOrderExecutionCommand
        {
            CommandId = DeterministicId("complete", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, AcceptOrderExecutionCommand.Verb, executionId.Format()),
            EntityId = executionId,
            EffectiveAtUtc = observation.OccurredAtUtc
        };
        return _context.ActorService.SendAsync<AcceptOrderExecutionCommand, OrderExecutionId>(command, executionId);
    }

    private ValueTask<ServiceResult<Guid>> SendCancelledAsync(OrderExecutionId executionId, BrokerOrderObservationEvidence observation)
    {
        var command = CreateCancellationHandoff(executionId, observation.ObservationId, observation.OccurredAtUtc, recovery: false);
        return _context.ActorService.SendAsync<CancelOrderExecutionCommand, OrderExecutionId>(command, executionId);
    }

    internal static CancelOrderExecutionCommand CreateCancellationHandoff(OrderExecutionId executionId,
        Guid observationId, DateTime occurredAtUtc, bool recovery) => new()
        {
            CommandId = DeterministicId(recovery ? "cancelled-reconciliation" : "cancelled", observationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, CancelOrderExecutionCommand.Verb, executionId.Format()),
            EntityId = executionId,
            EffectiveAtUtc = occurredAtUtc
        };

    private ValueTask<ServiceResult<Guid>> SendRejectedAsync(OrderExecutionId executionId, BrokerOrderObservationEvidence observation)
    {
        var command = Rejection(executionId, DeterministicId("rejected", observation.ObservationId.ToString("N")),
            observation.Category, observation.Detail);
        return _context.ActorService.SendAsync<RejectOrderExecutionCommand, OrderExecutionId>(command, executionId);
    }

    internal static RejectOrderExecutionCommand? CreatePlacementRejection(BrokerOrderDefinition order)
    {
        if (order.Status != BrokerOrderStatus.Rejected || order.PendingMutation != BrokerMutationKind.Unknown ||
            order.LastObservation is not null || order.PriorOperationId != Guid.Empty)
            return null;

        // A rejected component cannot prove zero exposure for its siblings.
        if (order.Order.Components.Length != 1)
            throw new InvalidOperationException($"BROKER_ORDER.COMPONENT_RECONCILIATION_REQUIRED;{order.Id.Format()}");

        return Rejection(order.Id.Execution, DeterministicId("placement-rejected", order.Id.Format(), order.OperationId.ToString("N")),
            order.DispatchCategory, order.DispatchDetail);
    }

    internal static bool IsDispatchIntent(BrokerOrderDefinition order) =>
        order.LastObservation is null && (order.Status, order.PendingMutation) is
            (BrokerOrderStatus.PlacePending, BrokerMutationKind.Place) or
            (BrokerOrderStatus.UpdatePending, BrokerMutationKind.UpdateLimit) or
            (BrokerOrderStatus.CancelPending, BrokerMutationKind.Cancel);

    private static RejectOrderExecutionCommand Rejection(OrderExecutionId executionId, Guid commandId, string category, string detail) => new()
        {
            CommandId = commandId,
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName, RejectOrderExecutionCommand.Verb, executionId.Format()),
            EntityId = executionId,
            RejectionDetail = string.IsNullOrWhiteSpace(category) ? detail
                : string.IsNullOrWhiteSpace(detail) ? category : $"{category}: {detail}"
        };

    private static void EnsureHandoff(ServiceResult<Guid>? result)
    {
        if (result is { Success: false })
            throw new InvalidOperationException($"BROKER_ORDER.EVIDENCE_HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
    }

    private static Guid DeterministicId(params string[] parts) => new(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)))[..16]);
    private static IBrokerOrderCommandContext Typed(ICommandActorContext<BrokerOrderCommandActor> context) => context as IBrokerOrderCommandContext ?? throw new ArgumentException("Typed BrokerOrder command context required.", nameof(context));
}
