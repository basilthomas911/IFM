using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

/// <summary>
/// Routes eligible ITI triggers into the workflow Command actor and bridges committed workflow lifecycle events to
/// pipeline commands.
/// </summary>
/// <remarks>
/// This actor owns no durable state, sends no replies, and performs no durable replay. Processing notifications are
/// observational; only Completed and Failed pipeline events are translated into workflow commands.
/// </remarks>
public sealed class IntrinsicTimeStrategyWorkflowRealtimeActor(
    IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> actorContext)
    : BaseEventActor<IntrinsicTimeStrategyWorkflowRealtimeActor>(actorContext, RequireContext(actorContext).Logger)
{
    static readonly ActorTypeId[] ExternalRoutes =
    [
        Route(FuturesItiSignalGeneratedEvent.Actor, FuturesItiSignalGeneratedEvent.Verb),
        Route(RegimeDiscoveryPipelineProcessingEvent.Actor, RegimeDiscoveryPipelineProcessingEvent.Verb),
        Route(RegimeDiscoveryPipelineCompletedEvent.Actor, RegimeDiscoveryPipelineCompletedEvent.Verb),
        Route(RegimeDiscoveryPipelineFailedEvent.Actor, RegimeDiscoveryPipelineFailedEvent.Verb),
        Route(MarketConditionPipelineProcessingEvent.Actor, MarketConditionPipelineProcessingEvent.Verb),
        Route(MarketConditionPipelineCompletedEvent.Actor, MarketConditionPipelineCompletedEvent.Verb),
        Route(MarketConditionPipelineFailedEvent.Actor, MarketConditionPipelineFailedEvent.Verb),
        Route(TradeSelectionPipelineProcessingEvent.Actor, TradeSelectionPipelineProcessingEvent.Verb),
        Route(TradeSelectionPipelineCompletedEvent.Actor, TradeSelectionPipelineCompletedEvent.Verb),
        Route(TradeSelectionPipelineFailedEvent.Actor, TradeSelectionPipelineFailedEvent.Verb),
        Route(OrderCompositionPipelineProcessingEvent.Actor, OrderCompositionPipelineProcessingEvent.Verb),
        Route(OrderCompositionPipelineCompletedEvent.Actor, OrderCompositionPipelineCompletedEvent.Verb),
        Route(OrderCompositionPipelineFailedEvent.Actor, OrderCompositionPipelineFailedEvent.Verb),
        Route(RiskManagementPipelineProcessingEvent.Actor, RiskManagementPipelineProcessingEvent.Verb),
        Route(RiskManagementPipelineCompletedEvent.Actor, RiskManagementPipelineCompletedEvent.Verb),
        Route(RiskManagementPipelineFailedEvent.Actor, RiskManagementPipelineFailedEvent.Verb)
    ];

    /// <summary>Gets the workflow Realtime actor name.</summary>
    public const string ActorName = "IntrinsicTimeStrategyWorkflowRealtime";

    IIntrinsicTimeStrategyWorkflowRealtimeContext ActorContext { get; } = RequireContext(actorContext);

    /// <inheritdoc />
    protected override ValueTask OnStartup(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
    {
        if (!ActorContext.Options.Enabled)
        {
            ActorContext.Logger.LogInformation(
                "Intrinsic Time Strategy workflow live routing is disabled; no realtime routes were registered");
            return ValueTask.CompletedTask;
        }
        foreach (var route in ExternalRoutes)
            context.AddRealtimeRouter(route, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnShutdown(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
    {
        if (!ActorContext.Options.Enabled)
            return ValueTask.CompletedTask;
        foreach (var route in ExternalRoutes)
            context.RemoveRealtimeRouter(route, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Realtime, Name: ActorName })
            return default!;

        return message.Subject.Verb switch
        {
            FuturesItiSignalGeneratedEvent.Verb => message.AsEvent<FuturesItiSignalGeneratedEvent>()!,
            IntrinsicTimeStrategyWorkflowStartedEvent.Verb => message.AsEvent<IntrinsicTimeStrategyWorkflowStartedEvent>()!,
            IntrinsicTimeStrategyWorkflowContinuedEvent.Verb => message.AsEvent<IntrinsicTimeStrategyWorkflowContinuedEvent>()!,
            IntrinsicTimeStrategyWorkflowStoppedEvent.Verb => message.AsEvent<IntrinsicTimeStrategyWorkflowStoppedEvent>()!,
            RegimeDiscoveryPipelineProcessingEvent.Verb => message.AsEvent<RegimeDiscoveryPipelineProcessingEvent>()!,
            RegimeDiscoveryPipelineFailedEvent.Verb => message.AsEvent<RegimeDiscoveryPipelineFailedEvent>()!,
            RegimeDiscoveryPipelineCompletedEvent.Verb => ParseCompleted(message),
            _ => throw new InvalidOperationException(
                $"Unable to resolve {ActorName} realtime event from message: {message.Subject}")
        };
    }

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case FuturesItiSignalGeneratedEvent trigger:
                await StartWorkflowAsync(context, trigger).ConfigureAwait(false);
                break;
            case IntrinsicTimeStrategyWorkflowStartedEvent started:
                await StartPipelineAsync(context, DispatchInput.From(started)).ConfigureAwait(false);
                break;
            case IntrinsicTimeStrategyWorkflowContinuedEvent continued:
                await StartPipelineAsync(context, DispatchInput.From(continued)).ConfigureAwait(false);
                break;
            case RegimeDiscoveryPipelineCompletedEvent completed:
                await CompletePipelineAsync(context, completed).ConfigureAwait(false);
                break;
            case RegimeDiscoveryPipelineFailedEvent failed:
                await FailPipelineAsync(context, failed).ConfigureAwait(false);
                break;
            case RegimeDiscoveryPipelineProcessingEvent processing:
                ActorContext.Logger.LogDebug(
                    "Workflow {WorkflowId} pipeline {Stage} is processing at revision {Revision}",
                    processing.WorkflowId,
                    processing.PipelineStage,
                    processing.InputWorkflowRevision);
                break;
            case IntrinsicTimeStrategyWorkflowCompletedEvent:
            case IntrinsicTimeStrategyWorkflowStoppedEvent:
                break;
            default:
                throw new InvalidOperationException($"Unsupported workflow realtime event: {domainEvent.GetType().Name}");
        }
    }

    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        ActorThreadId threadId,
        IEvent domainEvent,
        Exception exception)
    {
        ActorContext.Logger.LogError(
            exception,
            "One-way workflow realtime handling failed for {EventName} on {ThreadId}",
            domainEvent?.EventName ?? "Unknown",
            threadId);
        return ValueTask.CompletedTask;
    }

    async ValueTask StartWorkflowAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        FuturesItiSignalGeneratedEvent trigger)
    {
        var workflowId = StrategyWorkflowId.New(ActorContext.TimeProvider);
        var entityId = IntrinsicTimeStrategyWorkflowEntityId.Create(trigger.EntityId);
        var triggerId = trigger.Id == Guid.Empty ? trigger.CommandId : trigger.Id;
        var command = new StartIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = triggerId == Guid.Empty ? Guid.CreateVersion7(ActorContext.TimeProvider.GetUtcNow()) : triggerId,
            Subject = CommandSubject(StartIntrinsicTimeStrategyWorkflowCommand.Verb, entityId),
            EntityId = entityId,
            ProposedWorkflowId = workflowId,
            TriggerEventId = triggerId,
            TriggerEvent = trigger,
            CorrelationId = trigger.CommandId == Guid.Empty ? triggerId : trigger.CommandId,
            CausationId = triggerId,
            RequestedAtUtc = trigger.CreatedOn == default ? ActorContext.TimeProvider.GetUtcNow().UtcDateTime : trigger.CreatedOn,
            WorkflowDefinitionVersion = 1
        };
        await context.SendAsync<StartIntrinsicTimeStrategyWorkflowCommand, IntrinsicTimeStrategyWorkflowEntityId>(
            command,
            entityId).ConfigureAwait(false);
    }

    static async ValueTask StartPipelineAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        DispatchInput input)
    {
        switch (input.Stage)
        {
            case StrategyWorkflowStage.RegimeDiscovery:
                await context.SendAsync<StartRegimeDiscoveryPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateStart<StartRegimeDiscoveryPipelineCommand>(input, StartRegimeDiscoveryPipelineCommand.Actor,
                        StartRegimeDiscoveryPipelineCommand.Verb, StartRegimeDiscoveryPipelineCommand.ErrorId), input.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.MarketCondition:
                await context.SendAsync<StartMarketConditionPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateStart<StartMarketConditionPipelineCommand>(input, StartMarketConditionPipelineCommand.Actor,
                        StartMarketConditionPipelineCommand.Verb, StartMarketConditionPipelineCommand.ErrorId), input.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.TradeSelection:
                await context.SendAsync<StartTradeSelectionPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateStart<StartTradeSelectionPipelineCommand>(input, StartTradeSelectionPipelineCommand.Actor,
                        StartTradeSelectionPipelineCommand.Verb, StartTradeSelectionPipelineCommand.ErrorId), input.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.OrderComposition:
                await context.SendAsync<StartOrderCompositionPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateStart<StartOrderCompositionPipelineCommand>(input, StartOrderCompositionPipelineCommand.Actor,
                        StartOrderCompositionPipelineCommand.Verb, StartOrderCompositionPipelineCommand.ErrorId), input.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.RiskManagement:
                await context.SendAsync<StartRiskManagementPipelineCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateStart<StartRiskManagementPipelineCommand>(input, StartRiskManagementPipelineCommand.Actor,
                        StartRiskManagementPipelineCommand.Verb, StartRiskManagementPipelineCommand.ErrorId), input.EntityId).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(input.Stage), input.Stage, "A dispatch requires a concrete pipeline stage.");
        }
    }

    static async ValueTask CompletePipelineAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        RegimeDiscoveryPipelineCompletedEvent completed)
    {
        switch (completed.PipelineStage)
        {
            case StrategyWorkflowStage.RegimeDiscovery:
                await context.SendAsync<CompleteRegimeDiscoveryCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateCompletion<CompleteRegimeDiscoveryCommand>(completed, CompleteRegimeDiscoveryCommand.Actor, CompleteRegimeDiscoveryCommand.Verb), completed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.MarketCondition:
                await context.SendAsync<CompleteMarketConditionCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateCompletion<CompleteMarketConditionCommand>(completed, CompleteMarketConditionCommand.Actor, CompleteMarketConditionCommand.Verb), completed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.TradeSelection:
                await context.SendAsync<CompleteTradeSelectionCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateCompletion<CompleteTradeSelectionCommand>(completed, CompleteTradeSelectionCommand.Actor, CompleteTradeSelectionCommand.Verb), completed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.OrderComposition:
                await context.SendAsync<CompleteOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateCompletion<CompleteOrderCompositionCommand>(completed, CompleteOrderCompositionCommand.Actor, CompleteOrderCompositionCommand.Verb), completed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.RiskManagement:
                await context.SendAsync<CompleteRiskManagementCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateCompletion<CompleteRiskManagementCommand>(completed, CompleteRiskManagementCommand.Actor, CompleteRiskManagementCommand.Verb), completed.EntityId).ConfigureAwait(false);
                break;
        }
    }

    static async ValueTask FailPipelineAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        RegimeDiscoveryPipelineFailedEvent failed)
    {
        switch (failed.PipelineStage)
        {
            case StrategyWorkflowStage.RegimeDiscovery:
                await context.SendAsync<FailRegimeDiscoveryCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateFailure<FailRegimeDiscoveryCommand>(failed, FailRegimeDiscoveryCommand.Actor, FailRegimeDiscoveryCommand.Verb), failed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.MarketCondition:
                await context.SendAsync<FailMarketConditionCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateFailure<FailMarketConditionCommand>(failed, FailMarketConditionCommand.Actor, FailMarketConditionCommand.Verb), failed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.TradeSelection:
                await context.SendAsync<FailTradeSelectionCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateFailure<FailTradeSelectionCommand>(failed, FailTradeSelectionCommand.Actor, FailTradeSelectionCommand.Verb), failed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.OrderComposition:
                await context.SendAsync<FailOrderCompositionCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateFailure<FailOrderCompositionCommand>(failed, FailOrderCompositionCommand.Actor, FailOrderCompositionCommand.Verb), failed.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.RiskManagement:
                await context.SendAsync<FailRiskManagementCommand, IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateFailure<FailRiskManagementCommand>(failed, FailRiskManagementCommand.Actor, FailRiskManagementCommand.Verb), failed.EntityId).ConfigureAwait(false);
                break;
        }
    }

    static TCommand CreateStart<TCommand>(DispatchInput input, string actor, string verb, int errorCode)
        where TCommand : class, ICommand<IntrinsicTimeStrategyWorkflowEntityId>, new()
    {
        var command = new TCommand();
        Set(command, nameof(ICommand.CommandId), input.CommandId);
        Set(command, nameof(ICommand.Subject), new ActorSubject(ActorType.Command, actor, verb, input.EntityId.Format()));
        Set(command, "PostEvents", true);
        Set(command, "EntityId", input.EntityId);
        Set(command, nameof(ICommand.ErrorCode), errorCode);
        Set(command, nameof(ICommand.RouteTo), input.BoundedContext);
        Set(command, "WorkflowId", input.WorkflowId);
        Set(command, "InputWorkflowRevision", input.Revision);
        Set(command, "WorkflowState", input.WorkflowState);
        Set(command, "TriggerEvent", input.TriggerEvent);
        Set(command, "CorrelationId", input.CorrelationId);
        Set(command, "CausationId", input.CausationId);
        Set(command, "RequestedAtUtc", input.RequestedAtUtc);
        Set(command, "ExpectedCompletionAtUtc", input.ExpectedCompletionAtUtc);
        return command;
    }

    static TCommand CreateCompletion<TCommand>(
        RegimeDiscoveryPipelineCompletedEvent completed,
        string actor,
        string verb)
        where TCommand : class, ICommand<IntrinsicTimeStrategyWorkflowEntityId>, new()
    {
        var command = new TCommand();
        SetCommonResultCommand(command, actor, verb, completed.EntityId, completed.WorkflowId,
            completed.InputWorkflowRevision, completed.Id, completed.CorrelationId, completed.CausationId);
        Set(command, "Result", completed.Result);
        Set(command, "CompletedAtUtc", completed.CompletedAtUtc);
        return command;
    }

    static TCommand CreateFailure<TCommand>(
        RegimeDiscoveryPipelineFailedEvent failed,
        string actor,
        string verb)
        where TCommand : class, ICommand<IntrinsicTimeStrategyWorkflowEntityId>, new()
    {
        var command = new TCommand();
        SetCommonResultCommand(command, actor, verb, failed.EntityId, failed.WorkflowId,
            failed.InputWorkflowRevision, failed.Id, failed.CorrelationId, failed.CausationId);
        Set(command, "Failure", new StrategyPipelineFailure
        {
            ErrorCode = failed.ErrorCode,
            ErrorMessage = failed.ErrorMessage,
            ErrorType = failed.ErrorType.ToString(),
            ErrorData = failed.ErrorData,
            FailedAtUtc = failed.ErrorDate
        });
        Set(command, "FailedAtUtc", failed.ErrorDate);
        return command;
    }

    static void SetCommonResultCommand<TCommand>(
        TCommand command,
        string actor,
        string verb,
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        StrategyWorkflowId workflowId,
        long revision,
        Guid sourceEventId,
        Guid correlationId,
        Guid causationId)
        where TCommand : class, ICommand<IntrinsicTimeStrategyWorkflowEntityId>
    {
        Set(command, nameof(ICommand.CommandId), sourceEventId);
        Set(command, nameof(ICommand.Subject), new ActorSubject(ActorType.Command, actor, verb, entityId.Format()));
        Set(command, "PostEvents", true);
        Set(command, "EntityId", entityId);
        Set(command, "WorkflowId", workflowId);
        Set(command, "InputWorkflowRevision", revision);
        Set(command, "SourceEventId", sourceEventId);
        Set(command, "CorrelationId", correlationId);
        Set(command, "CausationId", causationId);
    }

    static void Set(object target, string property, object? value)
        => EventInitHelper.SetProperty(target, property, value);

    static IEvent ParseCompleted(IActorMessage message)
    {
        try
        {
            var workflowCompleted = message.AsEvent<IntrinsicTimeStrategyWorkflowCompletedEvent>();
            if (workflowCompleted is not null && workflowCompleted.WorkflowId.Value != Guid.Empty)
                return workflowCompleted;
        }
        catch
        {
            // Pipeline completion contracts have a larger payload at the same realtime verb.
        }
        return message.AsEvent<RegimeDiscoveryPipelineCompletedEvent>()!;
    }

    static ActorTypeId Route(string actor, string verb) => new(ActorType.Realtime, actor, verb);

    static IIntrinsicTimeStrategyWorkflowRealtimeContext RequireContext(
        IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
        => context as IIntrinsicTimeStrategyWorkflowRealtimeContext
            ?? throw new ArgumentException(
                $"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowRealtimeContext)}.",
                nameof(context));

    static ActorSubject CommandSubject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId)
        => new(ActorType.Command, StartIntrinsicTimeStrategyWorkflowCommand.Actor, verb, entityId.Format());

    readonly record struct DispatchInput(
        IntrinsicTimeStrategyWorkflowEntityId EntityId,
        StrategyWorkflowId WorkflowId,
        long Revision,
        StrategyWorkflowStage Stage,
        Guid CommandId,
        BoundedContextName BoundedContext,
        IntrinsicTimeStrategyWorkflowState WorkflowState,
        FuturesItiSignalGeneratedEvent TriggerEvent,
        Guid CorrelationId,
        Guid CausationId,
        DateTime RequestedAtUtc,
        DateTime? ExpectedCompletionAtUtc)
    {
        public static DispatchInput From(IntrinsicTimeStrategyWorkflowStartedEvent e)
            => new(e.EntityId, e.WorkflowId, e.WorkflowRevision, e.NextPipelineStage,
                e.NextPipelineCommandId, e.NextPipelineBoundedContext, e.WorkflowState, e.TriggerEvent,
                e.CorrelationId, e.Id, e.RequestedAtUtc, e.ExpectedCompletionAtUtc);

        public static DispatchInput From(IntrinsicTimeStrategyWorkflowContinuedEvent e)
            => new(e.EntityId, e.WorkflowId, e.WorkflowRevision, e.NextPipelineStage,
                e.NextPipelineCommandId, e.NextPipelineBoundedContext, e.WorkflowState, e.TriggerEvent,
                e.CorrelationId, e.Id, e.RequestedAtUtc, e.ExpectedCompletionAtUtc);
    }
}
