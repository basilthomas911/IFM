using TomasAI.IFM.Shared.EventModelActor.Contracts;

using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventSourcing;

public abstract class BaseCommandContext<TState>(IEventRepository<TState> eventRepository)
    : ICommandContext<TState> where TState : IBoundedContextState<TState>
{
    /// <summary>
    /// Executes the specified command asynchronously within its bounded context.
    /// </summary>
    /// <remarks>This method loads the bounded context associated with the command, executes the command to
    /// generate any state change events, and persists these events. The exact persistence action is determined by the
    /// state repository.</remarks>
    /// <typeparam name="TEntityId">The type of the instance identifier for the command.</typeparam>
    /// <param name="command">The command to be executed, which contains the necessary data and logic for execution.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/>
    /// with the command's unique identifier if the execution is successful.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6966", Justification = "Execute is the synchronous domain-handler contract; ExecuteAsync dispatches a different optional handler method and is not interchangeable.")]
    public async Task<ServiceResult<Guid>> ExecuteAsync<TEntityId>(ICommand<TEntityId> command) where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(command);
        // load bounded context...
        var boundCtx = await eventRepository.LoadBoundedContextAsync(command).ConfigureAwait(false);

        // execute command against bounded context to generate any state change events...
        boundCtx.Execute(command);

        // get bounded context state...
        var boundCtxState = boundCtx.State;

        // save any state change events...(exact persistence action determined by state repository)...
        await eventRepository.SaveBoundedContextAsync(boundCtxState, command).ConfigureAwait(false);

        // successfully executed command...
        return new ServiceOk<Guid>(command.CommandId);
    }

    /// <summary>
    /// Executes the specified command asynchronously within a bounded context and persists any resulting state changes.
    /// </summary>
    /// <remarks>This method loads the bounded context associated with the command, executes the command to
    /// produce any state changes, and persists those changes. The persistence mechanism is determined by the state
    /// repository.</remarks>
    /// <param name="command">The command to be executed, which must not be null and should contain a valid command identifier.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, containing a <see
    /// cref="ServiceResult{Guid}"/> with the command's unique identifier upon successful execution.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6966", Justification = "Execute is the synchronous domain-handler contract; ExecuteAsync dispatches a different optional handler method and is not interchangeable.")]
    public async ValueTask<ServiceResult<Guid>> ExecuteAsync(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var boundCtx = await eventRepository.LoadBoundedContextAsync(command).ConfigureAwait(false);

        // execute command against bounded context to generate any state change events...
        boundCtx.Execute(command);

        // get bounded context state...
        var boundCtxState = boundCtx.State;

        // save any state change events...(exact persistence action determined by state repository)...
        await eventRepository.SaveBoundedContextAsync(boundCtxState, command).ConfigureAwait(false);

        // successfully executed command...
        return new ServiceOk<Guid>(command.CommandId);
    }
}
