using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Routing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;

/// <summary>Starts workflows from eligible ITI triggers and dispatches only committed Started snapshots.</summary>
/// <remarks>This stateless actor has no replay, resume, redispatch, or pipeline terminal-translation route.</remarks>
public sealed class IntrinsicTimeStrategyWorkflowRealtimeActor(
    IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> actorContext)
    : BaseEventActor<IntrinsicTimeStrategyWorkflowRealtimeActor>(actorContext, RequireContext(actorContext).Logger)
{
    static readonly ActorTypeId TriggerRoute = new(
        ActorType.Realtime,
        FuturesItiSignalGeneratedEvent.RealtimeActor,
        FuturesItiSignalGeneratedEvent.Verb);

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
        context.AddRealtimeRouter(TriggerRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnShutdown(IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
    {
        if (ActorContext.Options.Enabled)
            context.RemoveRealtimeRouter(TriggerRoute, Id);
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
            WorkflowStrategyStateUpdatedEvent.Verb => message.AsEvent<WorkflowStrategyStateUpdatedEvent>()!,
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
            case WorkflowStrategyStateUpdatedEvent snapshot
                when snapshot.State is
                {
                    Status: WorkflowStrategyMachineStatus.Started
                }:
                await DispatchCommittedStateAsync(context, snapshot).ConfigureAwait(false);
                break;
            case WorkflowStrategyStateUpdatedEvent:
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
        ActorContext.Logger.LogError(exception,
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
        var requestedAtUtc = trigger.CreatedOn == default
            ? ActorContext.TimeProvider.GetUtcNow().UtcDateTime
            : trigger.CreatedOn;
        var resolved = await ActorContext.ConfigurationDb
            .ResolveEffectiveRegimeDiscoveryAsync(requestedAtUtc, trigger.EntityId.TimePeriod).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "No published Regime Discovery parameter set is effective for the workflow trigger.");
        if (ActorContext.Options.RequireWarmRegimeDiscoverySignals)
        {
            var readiness = await ActorContext.RegimeDiscoverySnapshotProvider.CaptureAsync(
                RegimeDiscoverySnapshotRequestFactory.Create(
                    MarketSeriesIdentity.ForContract(trigger.EntityId.ContractId),
                    resolved.ParameterSet)).ConfigureAwait(false);
            if (!readiness.IsSuccess)
            {
                ActorContext.Logger.LogWarning(
                    "Regime Discovery live trigger {TriggerId} was not started because {IssueCount} required signal " +
                    "observations did not pass cache warm-up qualification",
                    triggerId,
                    readiness.Issues.Count(issue =>
                        issue.Availability != RegimeDiscoverySignalAvailability.Available));
                return;
            }
        }

        var command = new StartIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = triggerId == Guid.Empty
                ? Guid.CreateVersion7(ActorContext.TimeProvider.GetUtcNow())
                : triggerId,
            Subject = CommandSubject(StartIntrinsicTimeStrategyWorkflowCommand.Verb, entityId),
            EntityId = entityId,
            ProposedWorkflowId = workflowId,
            TriggerEventId = triggerId,
            TriggerEvent = trigger,
            CorrelationId = trigger.CommandId == Guid.Empty ? triggerId : trigger.CommandId,
            CausationId = triggerId,
            RequestedAtUtc = requestedAtUtc,
            WorkflowDefinitionVersion = 1,
            RegimeDiscoveryParameterSet = resolved.ParameterSet,
            RegimeDiscoveryParameterPayloadSha256 = resolved.PayloadSha256
        };
        await context.SendAsync<StartIntrinsicTimeStrategyWorkflowCommand,
            IntrinsicTimeStrategyWorkflowEntityId>(command, entityId).ConfigureAwait(false);
    }

    static async ValueTask DispatchCommittedStateAsync(
        IEventActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context,
        WorkflowStrategyStateUpdatedEvent snapshot)
    {
        var view = snapshot.State;
        var commandId = IntrinsicTimeStrategyWorkflowCommandActor.DeterministicPipelineCommandId(
            view.WorkflowId,
            view.CurrentStage,
            view.WorkflowRevision);
        var route = IntrinsicTimeStrategyPipelineRoutes.Get(view.CurrentStage);
        if (view.CurrentStage == StrategyWorkflowStage.RegimeDiscovery)
        {
            var execute = CreateRegimeExecute(snapshot)
                ?? throw new InvalidOperationException("Only a committed Started/Regime snapshot can dispatch Execute.");
            var executionId = execute.EntityId;
            await context.SendAsync<ExecuteRegimeDiscoveryPipelineCommand, RegimeDiscoveryExecutionEntityId>(
                execute,
                executionId).ConfigureAwait(false);
            return;
        }

        var input = new LaterPipelineInput(view, snapshot.Id, commandId, route.BoundedContext);
        switch (view.CurrentStage)
        {
            case StrategyWorkflowStage.MarketCondition:
                await context.SendAsync<StartMarketConditionPipelineCommand,
                    IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateLaterStart<StartMarketConditionPipelineCommand>(input,
                        StartMarketConditionPipelineCommand.Actor,
                        StartMarketConditionPipelineCommand.Verb,
                        StartMarketConditionPipelineCommand.ErrorId), view.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.TradeSelection:
                await context.SendAsync<StartTradeSelectionPipelineCommand,
                    IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateLaterStart<StartTradeSelectionPipelineCommand>(input,
                        StartTradeSelectionPipelineCommand.Actor,
                        StartTradeSelectionPipelineCommand.Verb,
                        StartTradeSelectionPipelineCommand.ErrorId), view.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.OrderComposition:
                await context.SendAsync<StartOrderCompositionPipelineCommand,
                    IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateLaterStart<StartOrderCompositionPipelineCommand>(input,
                        StartOrderCompositionPipelineCommand.Actor,
                        StartOrderCompositionPipelineCommand.Verb,
                        StartOrderCompositionPipelineCommand.ErrorId), view.EntityId).ConfigureAwait(false);
                break;
            case StrategyWorkflowStage.RiskManagement:
                await context.SendAsync<StartRiskManagementPipelineCommand,
                    IntrinsicTimeStrategyWorkflowEntityId>(
                    CreateLaterStart<StartRiskManagementPipelineCommand>(input,
                        StartRiskManagementPipelineCommand.Actor,
                        StartRiskManagementPipelineCommand.Verb,
                        StartRiskManagementPipelineCommand.ErrorId), view.EntityId).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>Builds the deterministic Regime Execute command only from a committed Started/Regime snapshot.</summary>
    internal static ExecuteRegimeDiscoveryPipelineCommand? CreateRegimeExecute(
        WorkflowStrategyStateUpdatedEvent snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var view = snapshot.State;
        if (view.Status != WorkflowStrategyMachineStatus.Started ||
            view.CurrentStage != StrategyWorkflowStage.RegimeDiscovery)
            return null;

        var executionId = RegimeDiscoveryExecutionEntityId.Create(view.EntityId, view.WorkflowId);
        return new ExecuteRegimeDiscoveryPipelineCommand
        {
            CommandId = IntrinsicTimeStrategyWorkflowCommandActor.DeterministicPipelineCommandId(
                view.WorkflowId,
                view.CurrentStage,
                view.WorkflowRevision),
            Subject = new ActorSubject(ActorType.Command,
                ExecuteRegimeDiscoveryPipelineCommand.Actor,
                ExecuteRegimeDiscoveryPipelineCommand.Verb,
                executionId.Format()),
            EntityId = executionId,
            InputWorkflowRevision = view.WorkflowRevision,
            WorkflowView = view,
            TriggerEvent = view.TriggerEvent,
            CorrelationId = view.CorrelationId,
            CausationId = snapshot.Id,
            RequestedAtUtc = view.UpdatedAtUtc,
            ExpiresAtUtc = view.ExpiresAtUtc,
            ParameterSet = view.RegimeDiscoveryParameterSet,
            ParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256,
            TargetHorizon = view.TriggerEvent.EntityId.TimePeriod
        };
    }

    static TCommand CreateLaterStart<TCommand>(LaterPipelineInput input, string actor, string verb, int errorCode)
        where TCommand : class, ICommand<IntrinsicTimeStrategyWorkflowEntityId>, new()
    {
        var view = input.View;
        var command = new TCommand();
        Set(command, nameof(ICommand.CommandId), input.CommandId);
        Set(command, nameof(ICommand.Subject),
            new ActorSubject(ActorType.Command, actor, verb, view.EntityId.Format()));
        Set(command, "PostEvents", true);
        Set(command, "EntityId", view.EntityId);
        Set(command, nameof(ICommand.ErrorCode), errorCode);
        Set(command, nameof(ICommand.RouteTo), input.BoundedContext);
        Set(command, "WorkflowId", view.WorkflowId);
        Set(command, "InputWorkflowRevision", view.WorkflowRevision);
        Set(command, "WorkflowState", ToLegacyWorkflow(view));
        Set(command, "TriggerEvent", view.TriggerEvent);
        Set(command, "CorrelationId", view.CorrelationId);
        Set(command, "CausationId", input.CausationId);
        Set(command, "RequestedAtUtc", view.UpdatedAtUtc);
        Set(command, "ExpectedCompletionAtUtc", view.ExpiresAtUtc);
        return command;
    }

    static IntrinsicTimeStrategyWorkflowState ToLegacyWorkflow(IntrinsicTimeStrategyWorkflowView view) => new()
    {
        EntityId = view.EntityId,
        WorkflowId = view.WorkflowId,
        TriggerEventId = view.TriggerEventId,
        CorrelationId = view.CorrelationId,
        WorkflowDefinitionVersion = view.WorkflowDefinitionVersion,
        Status = StrategyWorkflowStatus.Running,
        Outcome = StrategyWorkflowOutcome.None,
        CurrentStage = view.CurrentStage,
        WorkflowRevision = view.WorkflowRevision,
        StartedAtUtc = view.StartedAtUtc,
        RegimeDiscovery = view.RegimeDiscovery,
        MarketCondition = view.MarketCondition,
        TradeSelection = view.TradeSelection,
        OrderComposition = view.OrderComposition,
        RiskManagement = view.RiskManagement,
        RegimeDiscoveryParameterSet = view.RegimeDiscoveryParameterSet,
        RegimeDiscoveryParameterPayloadSha256 = view.RegimeDiscoveryParameterPayloadSha256
    };

    static void Set(object target, string property, object? value)
        => EventInitHelper.SetProperty(target, property, value);

    static IIntrinsicTimeStrategyWorkflowRealtimeContext RequireContext(
        IRealtimeActorContext<IntrinsicTimeStrategyWorkflowRealtimeActor> context)
        => context as IIntrinsicTimeStrategyWorkflowRealtimeContext
            ?? throw new ArgumentException(
                $"Context must implement {nameof(IIntrinsicTimeStrategyWorkflowRealtimeContext)}.",
                nameof(context));

    static ActorSubject CommandSubject(string verb, IntrinsicTimeStrategyWorkflowEntityId entityId)
        => new(ActorType.Command, StartIntrinsicTimeStrategyWorkflowCommand.Actor, verb, entityId.Format());

    readonly record struct LaterPipelineInput(
        IntrinsicTimeStrategyWorkflowView View,
        Guid CausationId,
        Guid CommandId,
        BoundedContextName BoundedContext);
}
