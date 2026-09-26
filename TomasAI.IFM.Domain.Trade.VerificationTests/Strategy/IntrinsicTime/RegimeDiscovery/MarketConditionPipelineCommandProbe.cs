using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.VerificationTests.Strategy.IntrinsicTime.RegimeDiscovery;

/// <summary>Records actual assessment Function requests while preserving the real state repository.</summary>
public sealed class MarketConditionPipelineCommandProbe :
    IEventSourceFunctionStateRepository<MarketConditionAssessmentState, ExecuteMarketConditionAssessmentCommand>
{
    readonly Func<IEventSourceFunctionStateRepository<MarketConditionAssessmentState,
        ExecuteMarketConditionAssessmentCommand>> resolveRepository;
    readonly ConcurrentDictionary<string, ConcurrentQueue<ExecuteMarketConditionAssessmentCommand>> commands = new();

    /// <summary>Wraps the real repository without changing state loading or completion persistence.</summary>
    public MarketConditionPipelineCommandProbe(
        Func<IEventSourceFunctionStateRepository<MarketConditionAssessmentState,
            ExecuteMarketConditionAssessmentCommand>> resolveRepository)
    {
        this.resolveRepository = resolveRepository;
    }

    /// <summary>Returns the actual number of assessment requests observed for a workflow entity.</summary>
    public int Count(IntrinsicTimeStrategyWorkflowEntityId entityId)
        => commands.TryGetValue(entityId.Format(), out var received) ? received.Count : 0;

    /// <summary>Waits for the real initialized request instead of reconstructing it from an earlier workflow revision.</summary>
    public async Task<ExecuteMarketConditionAssessmentCommand> WaitAsync(
        IntrinsicTimeStrategyWorkflowEntityId entityId, TimeSpan timeout)
    {
        using var deadline = new CancellationTokenSource(timeout);
        try
        {
            while (true)
            {
                if (commands.TryGetValue(entityId.Format(), out var received) && received.TryPeek(out var command))
                    return command;
                await Task.Delay(25, deadline.Token);
            }
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Market Condition Function dispatch for {entityId.Format()} was not observed within {timeout}.");
        }
    }

    /// <summary>Loads real persisted state and records the Function request, including its resolved binding.</summary>
    public async ValueTask<MarketConditionAssessmentState> LoadStateAsync(
        ExecuteMarketConditionAssessmentCommand request, CancellationToken cancellationToken = default)
    {
        var state = await resolveRepository().LoadStateAsync(request, cancellationToken);
        commands.GetOrAdd(request.WorkflowEntityId.Format(), static _ => new()).Enqueue(request);
        return state;
    }

    /// <summary>Passes completed-state persistence through to the real repository unchanged.</summary>
    public ValueTask SaveCompletedStateAsync(IFunctionActorContext context,
        MarketConditionAssessmentState state, ExecuteMarketConditionAssessmentCommand request,
        CancellationToken cancellationToken = default)
        => resolveRepository().SaveCompletedStateAsync(context, state, request, cancellationToken);
}
