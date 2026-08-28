using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Realtime.Actor;

/// <summary>Translates projected public Regime terminal events into guarded Workflow commands.</summary>
/// <remarks>The actor owns no durable state, timer, retry, redelivery, replay, or processing route.</remarks>
public sealed class RegimeDiscoveryPipelineRealtimeActor(
    IRealtimeActorContext<RegimeDiscoveryPipelineRealtimeActor> actorContext)
    : BaseEventActor<RegimeDiscoveryPipelineRealtimeActor>(actorContext, Typed(actorContext).Logger)
{
    static readonly ActorTypeId CompletedRoute = new(
        ActorType.Realtime, RegimeDiscoveryPipelineCompletedEvent.Actor,
        RegimeDiscoveryPipelineCompletedEvent.Verb);
    static readonly ActorTypeId FailedRoute = new(
        ActorType.Realtime, RegimeDiscoveryPipelineFailedEvent.Actor,
        RegimeDiscoveryPipelineFailedEvent.Verb);

    /// <summary>Gets the Regime terminal realtime actor name.</summary>
    public const string ActorName = RegimeDiscoveryPipelineCompletedEvent.Actor;

    IRegimeDiscoveryPipelineRealtimeContext ActorContext { get; } = Typed(actorContext);

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [RegimeDiscoveryPipelineCompletedEvent.Verb] =
                message => message.AsEvent<RegimeDiscoveryPipelineCompletedEvent>()!,
            [RegimeDiscoveryPipelineFailedEvent.Verb] =
                message => message.AsEvent<RegimeDiscoveryPipelineFailedEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<IEvent,
        IEventActorContext<RegimeDiscoveryPipelineRealtimeActor>, ValueTask>> _receiveMap =
        new Dictionary<Type, Func<IEvent,
            IEventActorContext<RegimeDiscoveryPipelineRealtimeActor>, ValueTask>>
        {
            [typeof(RegimeDiscoveryPipelineCompletedEvent)] = async (@event, context) =>
            {
                var command = CreateCompleteCommand((RegimeDiscoveryPipelineCompletedEvent)@event);
                await context.SendAsync<CompleteRegimeDiscoveryCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    command, command.EntityId).ConfigureAwait(false);
            },
            [typeof(RegimeDiscoveryPipelineFailedEvent)] = async (@event, context) =>
            {
                var command = CreateFailCommand((RegimeDiscoveryPipelineFailedEvent)@event);
                await context.SendAsync<FailRegimeDiscoveryCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    command, command.EntityId).ConfigureAwait(false);
            }
        };

    /// <inheritdoc />
    protected override ValueTask OnStartup(IEventActorContext<RegimeDiscoveryPipelineRealtimeActor> context)
    {
        context.AddRealtimeRouter(CompletedRoute, Id);
        context.AddRealtimeRouter(FailedRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnShutdown(IEventActorContext<RegimeDiscoveryPipelineRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(CompletedRoute, Id);
        context.RemoveRealtimeRouter(FailedRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<RegimeDiscoveryPipelineRealtimeActor> context,
        IActorMessage message)
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<RegimeDiscoveryPipelineRealtimeActor> context,
        IEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(domainEvent, _receiveMap);
        await handler(domainEvent, context).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<RegimeDiscoveryPipelineRealtimeActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception)
    {
        ActorContext.Logger.LogError(exception,
            "Regime terminal translation failed for {EventName} on {ThreadId}; no retry or replay is scheduled",
            domainEvent?.EventName ?? "Unknown", threadId);
        return ValueTask.CompletedTask;
    }

    /// <summary>Maps public completion to one deterministic guarded Workflow command.</summary>
    internal static CompleteRegimeDiscoveryCommand CreateCompleteCommand(
        RegimeDiscoveryPipelineCompletedEvent completed)
        => new()
        {
            CommandId = DeterministicCommandId(completed.EntityId, completed.WorkflowId,
                completed.InputWorkflowRevision, completed.Id, CompleteRegimeDiscoveryCommand.Verb),
            Subject = WorkflowSubject(CompleteRegimeDiscoveryCommand.Verb, completed.EntityId),
            EntityId = completed.EntityId,
            WorkflowId = completed.WorkflowId,
            InputWorkflowRevision = completed.InputWorkflowRevision,
            SourceEventId = completed.Id,
            Result = completed.Result,
            CorrelationId = completed.CorrelationId,
            CausationId = completed.Id,
            CompletedAtUtc = completed.CompletedAtUtc
        };

    /// <summary>Maps public failure to one deterministic guarded Workflow command.</summary>
    internal static FailRegimeDiscoveryCommand CreateFailCommand(RegimeDiscoveryPipelineFailedEvent failed)
        => new()
        {
            CommandId = DeterministicCommandId(failed.EntityId, failed.WorkflowId,
                failed.InputWorkflowRevision, failed.Id, FailRegimeDiscoveryCommand.Verb),
            Subject = WorkflowSubject(FailRegimeDiscoveryCommand.Verb, failed.EntityId),
            EntityId = failed.EntityId,
            WorkflowId = failed.WorkflowId,
            InputWorkflowRevision = failed.InputWorkflowRevision,
            SourceEventId = failed.Id,
            Failure = new StrategyPipelineFailure
            {
                ErrorCode = failed.ErrorCode,
                ErrorMessage = failed.ErrorMessage,
                ErrorType = failed.ErrorCode == 23103 ? "Timeout" : failed.ErrorType.ToString(),
                ErrorData = failed.ErrorData,
                FailedAtUtc = failed.ErrorDate
            },
            CorrelationId = failed.CorrelationId,
            CausationId = failed.Id,
            FailedAtUtc = failed.ErrorDate
        };

    internal static Guid DeterministicCommandId(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowId workflowId,
        long revision,
        Guid sourceEventId,
        string verb)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{entityId.Format()}|{workflowId}|{revision}|{sourceEventId:N}|{verb}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    static ActorSubject WorkflowSubject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId)
        => new(ActorType.Command, CompleteRegimeDiscoveryCommand.Actor, verb, entityId.Format());

    static IRegimeDiscoveryPipelineRealtimeContext Typed(
        IRealtimeActorContext<RegimeDiscoveryPipelineRealtimeActor> context)
        => context as IRegimeDiscoveryPipelineRealtimeContext
           ?? throw new ArgumentException(
               $"{nameof(context)} must implement {nameof(IRegimeDiscoveryPipelineRealtimeContext)}.",
               nameof(context));
}
