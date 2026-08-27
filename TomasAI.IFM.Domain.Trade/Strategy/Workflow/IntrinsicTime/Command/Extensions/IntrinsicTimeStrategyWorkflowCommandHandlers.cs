using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;

/// <summary>Provides result-returning handlers for Intrinsic Time Strategy workflow commands.</summary>
public static class IntrinsicTimeStrategyWorkflowCommandHandlers
{
    /// <summary>Executes a synchronous workflow state transition.</summary>
    public static ServiceResult<GuidResult> Execute<TCommand>(
        this TCommand command,
        IntrinsicTimeStrategyWorkflowCommandState state,
        Action<IntrinsicTimeStrategyWorkflowCommandState, TCommand> transition)
        where TCommand : ICommand
    {
        transition(state, command);
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }

    /// <summary>Executes an asynchronous workflow state transition.</summary>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync<TCommand>(
        this TCommand command,
        IntrinsicTimeStrategyWorkflowCommandState state,
        Func<IntrinsicTimeStrategyWorkflowCommandState, TCommand, ValueTask> transition)
        where TCommand : ICommand
    {
        await transition(state, command).ConfigureAwait(false);
        return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    }
}
