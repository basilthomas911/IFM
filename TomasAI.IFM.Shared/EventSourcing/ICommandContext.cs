using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

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
