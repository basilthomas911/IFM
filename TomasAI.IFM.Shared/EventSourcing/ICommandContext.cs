using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface ICommandContext<TState> where TState : IBoundedContextState
{
    /// <summary>
    /// always return command service result that contains command execution status 
    /// </summary>
    /// <typeparam name="TEntityId"></typeparam>
    /// <param name="command"></param>
    /// <returns></returns>
    Task<ServiceResult<Guid>> ExecuteAsync<TEntityId>(ICommand<TEntityId> command) where TEntityId : IActorEntityId;
    ValueTask<ServiceResult<Guid>> ExecuteAsync(ICommand command);
}
