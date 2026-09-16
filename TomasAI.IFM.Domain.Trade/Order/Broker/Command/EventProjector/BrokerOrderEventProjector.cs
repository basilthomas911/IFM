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
    private readonly IBrokerOrderCommandContext _context;
    private readonly ImmutableArray<EventProjectionDescriptor> _descriptors;

    public BrokerOrderEventProjector(ICommandActorContext<BrokerOrderCommandActor> actorContext,
        EventProjectorReliabilityOptions? options = null)
        : base(Typed(actorContext).DurableReplayQueue, Typed(actorContext).DbEventSource,
            Typed(actorContext).BlackboardService, Typed(actorContext).Logger, options)
    {
        _context = Typed(actorContext);
        _descriptors = [DescribeNotification<BrokerOrderChangedEvent, BrokerOrderId>(ProjectAsync)];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => [typeof(BrokerOrderChangedEvent)];

    private async Task ProjectAsync(BrokerOrderChangedEvent changed)
    {
        _context.ReadStore.Set(changed.State);
        if (changed.State.LastObservation is { } observation)
        {
            await ProjectObservationAsync(changed.EntityId, observation).ConfigureAwait(false);
            return;
        }
        if (changed.State.PendingMutation == BrokerMutationKind.Unknown) return;
        var receipt = changed.State.PendingMutation switch
        {
            BrokerMutationKind.Place => await PlaceAsync(changed).ConfigureAwait(false),
            BrokerMutationKind.UpdateLimit => await _context.TradeBroker.ModifyLimitAsync(new(
                changed.State.Order.BrokerAccountAlias,
                changed.EntityId.Format(),
                changed.State.OperationId,
                changed.State.CurrentSignedNetDebitLimit,
                changed.State.BrokerRevision)).ConfigureAwait(false),
            BrokerMutationKind.Cancel => await _context.TradeBroker.CancelAsync(new(
                changed.State.Order.BrokerAccountAlias,
                changed.EntityId.Format(),
                changed.State.OperationId,
                changed.State.BrokerRevision)).ConfigureAwait(false),
            _ => throw new InvalidOperationException("BROKER_ORDER.MUTATION_UNKNOWN")
        };
        var command = new RecordBrokerDispatchCommand
        {
            CommandId = DeterministicId("dispatch-receipt", changed.EntityId.Format(), changed.State.OperationId.ToString("N")),
            Subject = new(ActorType.Command, BrokerOrderActorNames.Command, RecordBrokerDispatchCommand.Verb, changed.EntityId.Format()),
            EntityId = changed.EntityId,
            OperationId = receipt.OperationId,
            Outcome = (BrokerDispatchResult)receipt.Outcome,
            Category = receipt.Category,
            Detail = receipt.Detail,
            RecordedAtUtc = receipt.RecordedAtUtc.Kind == DateTimeKind.Utc ? receipt.RecordedAtUtc : DateTime.UtcNow
        };
        var result = await _context.ActorService.SendAsync<RecordBrokerDispatchCommand, BrokerOrderId>(command, command.EntityId).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"BROKER_ORDER.RECEIPT_HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
    }

    private async ValueTask<BrokerDispatchReceipt> PlaceAsync(BrokerOrderChangedEvent changed)
    {
        if (!BrokerOrderRequestMapper.TryCreate(changed.State.Order,
                changed.EntityId.Execution.ExecutionAttemptId, changed.EntityId.ComponentId,
                changed.State.OperationId, out var request, out var reason) || request is null)
            throw new InvalidOperationException($"BROKER_ORDER.MAPPING_FAILED;{reason}");
        return await _context.TradeBroker.PlaceAsync(request).ConfigureAwait(false);
    }

    private async ValueTask ProjectObservationAsync(
        BrokerOrderId brokerOrderId,
        BrokerOrderObservationEvidence observation)
    {
        var executionId = brokerOrderId.Execution;
        ServiceResult<Guid>? result = observation.Kind switch
        {
            BrokerOrderObservationKind.Execution => await SendFillAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.Commission => await SendCommissionAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.OrderCompleted => await SendCompletedAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.Cancelled => await SendCancelledAsync(executionId, observation).ConfigureAwait(false),
            BrokerOrderObservationKind.Rejected => await SendRejectedAsync(executionId, observation).ConfigureAwait(false),
            _ => null
        };
        if (result is { Success: false })
            throw new InvalidOperationException($"BROKER_ORDER.EVIDENCE_HANDOFF_FAILED;{result.ErrorCode};{result.ErrorMessage}");
    }

    private ValueTask<ServiceResult<Guid>> SendFillAsync(
        OrderExecutionId executionId,
        BrokerOrderObservationEvidence observation)
    {
        var command = new AddOrderExecutionFillCommand
        {
            CommandId = DeterministicId("fill", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName,
                AddOrderExecutionFillCommand.Verb, executionId.Format()),
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

    private ValueTask<ServiceResult<Guid>> SendCommissionAsync(
        OrderExecutionId executionId,
        BrokerOrderObservationEvidence observation)
    {
        var command = new UpdateOrderExecutionFillCostCommand
        {
            CommandId = DeterministicId("commission", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName,
                UpdateOrderExecutionFillCostCommand.Verb, executionId.Format()),
            EntityId = executionId,
            ExternalExecutionId = observation.ExternalExecutionId,
            Commission = observation.Commission
        };
        return _context.ActorService.SendAsync<UpdateOrderExecutionFillCostCommand, OrderExecutionId>(command, executionId);
    }

    private ValueTask<ServiceResult<Guid>> SendCompletedAsync(
        OrderExecutionId executionId,
        BrokerOrderObservationEvidence observation)
    {
        var command = new AcceptOrderExecutionCommand
        {
            CommandId = DeterministicId("complete", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName,
                AcceptOrderExecutionCommand.Verb, executionId.Format()),
            EntityId = executionId,
            EffectiveAtUtc = observation.OccurredAtUtc
        };
        return _context.ActorService.SendAsync<AcceptOrderExecutionCommand, OrderExecutionId>(command, executionId);
    }

    private ValueTask<ServiceResult<Guid>> SendCancelledAsync(
        OrderExecutionId executionId,
        BrokerOrderObservationEvidence observation)
    {
        var command = new CancelOrderExecutionCommand
        {
            CommandId = DeterministicId("cancelled", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName,
                CancelOrderExecutionCommand.Verb, executionId.Format()),
            EntityId = executionId
        };
        return _context.ActorService.SendAsync<CancelOrderExecutionCommand, OrderExecutionId>(command, executionId);
    }

    private ValueTask<ServiceResult<Guid>> SendRejectedAsync(
        OrderExecutionId executionId,
        BrokerOrderObservationEvidence observation)
    {
        var command = new RejectOrderExecutionCommand
        {
            CommandId = DeterministicId("rejected", observation.ObservationId.ToString("N")),
            Subject = new(ActorType.Command, OrderExecutionCommandActor.ActorName,
                RejectOrderExecutionCommand.Verb, executionId.Format()),
            EntityId = executionId
        };
        return _context.ActorService.SendAsync<RejectOrderExecutionCommand, OrderExecutionId>(command, executionId);
    }

    private static Guid DeterministicId(params string[] parts) => new(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', parts)))[..16]);
    private static IBrokerOrderCommandContext Typed(ICommandActorContext<BrokerOrderCommandActor> context) => context as IBrokerOrderCommandContext ?? throw new ArgumentException("Typed BrokerOrder command context required.");
}
