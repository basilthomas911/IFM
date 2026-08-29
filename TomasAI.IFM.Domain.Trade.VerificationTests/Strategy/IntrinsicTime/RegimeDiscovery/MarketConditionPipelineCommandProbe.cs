using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

/// <summary>Observes the committed revision that is the sole authority for Market Condition Function dispatch.</summary>
public sealed class MarketConditionPipelineCommandProbe(IServiceProvider services)
{
    readonly ConcurrentDictionary<string, ExecuteMarketConditionPipelineCommand> _commands = new();

    public int Count(IntrinsicTimeStrategyWorkflowEntityId entityId)
        => _commands.ContainsKey(entityId.Format()) ? 1 : 0;

    public async Task<ExecuteMarketConditionPipelineCommand> WaitAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_commands.TryGetValue(entityId.Format(), out var existing)) return existing;
            var state = await LoadAsync(entityId);
            if (state.CurrentView is { Status: WorkflowStrategyMachineStatus.Started,
                    CurrentStage: StrategyWorkflowStage.MarketCondition } view)
            {
                var snapshot = new WorkflowStrategyStateUpdatedEvent
                {
                    Id = view.CausationId,
                    EntityId = view.EntityId,
                    WorkflowId = view.WorkflowId,
                    WorkflowRevision = view.WorkflowRevision,
                    State = view
                };
                var executionId = MarketConditionExecutionEntityId.Create(view.EntityId, view.WorkflowId);
                var command = new ExecuteMarketConditionPipelineCommand
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Function, ExecuteMarketConditionPipelineCommand.Actor,
                        ExecuteMarketConditionPipelineCommand.Verb, executionId.Format()),
                    EntityId = executionId,
                    InputWorkflowRevision = view.WorkflowRevision,
                    WorkflowView = view,
                    TriggerEvent = view.TriggerEvent,
                    CorrelationId = view.CorrelationId,
                    CausationId = snapshot.Id,
                    RequestedAtUtc = view.UpdatedAtUtc,
                    ExpiresAtUtc = view.ExpiresAtUtc,
                    ParameterSet = view.MarketConditionParameterSet,
                    ParameterPayloadSha256 = view.MarketConditionParameterPayloadSha256,
                    TargetHorizon = view.TriggerEvent.EntityId.TimePeriod,
                    FundId = view.FundId,
                    InstrumentRoot = view.MarketConditionParameterSet.InstrumentRoot
                };
                return _commands.GetOrAdd(entityId.Format(), command);
            }
            await Task.Delay(25);
        }
        throw new TimeoutException(
            $"Market Condition Function dispatch for {entityId.Format()} was not observed within {timeout}.");
    }

    async ValueTask<IntrinsicTimeStrategyWorkflowCommandState> LoadAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId)
    {
        var replay = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor,
                ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, entityId.Format()),
            EntityId = entityId
        };
        var repository = services.GetRequiredService<IActorSupervisor>().Container.Resolve<
            IEventSourceActorStateRepository<IntrinsicTimeStrategyWorkflowCommandState>>();
        return await repository.LoadStateAsync(replay);
    }
}
