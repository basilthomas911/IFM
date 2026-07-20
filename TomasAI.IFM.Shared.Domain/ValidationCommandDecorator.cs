using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Domain;

public class ValidationCommandDecorator<TState>(ICommandContext<TState> commandContext, IValidationDecoratorFactory commandDecoratorFactory)
    : ICommandContext<TState> where TState : IBoundedContextState
{
    /// <summary>
    /// validate command fields via factory command decorator
    /// </summary>
    /// <typeparam name="TEntityId"></typeparam>
    /// <param name="command"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ExecuteAsync<TEntityId>(ICommand<TEntityId> command) where TEntityId : IActorEntityId
    {
        var commandDecorator = commandDecoratorFactory.GetDecorator<TState>();
        commandDecorator?.Validate(command);
        return await commandContext.ExecuteAsync(command);
    }

    /// <summary>
    /// Executes the specified command asynchronously and returns the result.
    /// </summary>
    /// <param name="command">The command to be executed. Must not be <see langword="null"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation,  containing a <see
    /// cref="ServiceResult{T}"/> with a <see cref="Guid"/> that  represents the result of the command execution.</returns>
    public async ValueTask<ServiceResult<Guid>> ExecuteAsync(ICommand command)
    {
        var commandDecorator = commandDecoratorFactory.GetDecorator<TState>();
        commandDecorator?.Validate(command);
        return await commandContext.ExecuteAsync(command);
    }
}
