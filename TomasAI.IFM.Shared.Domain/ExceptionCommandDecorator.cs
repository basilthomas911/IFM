using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Domain;

public class ExceptionCommandDecorator<TState>(
    ICommandContext<TState> commandContext,
    IExceptionDecoratorFactory exceptionDecoratorFactory,
    ILogger logger) : ICommandContext<TState> where  TState : IBoundedContextState
{
     /// <summary>
     /// Executes the specified command asynchronously and returns the result.
     /// </summary>
     /// <remarks>This method logs any exceptions that occur during command execution and attempts to convert
     /// them into a structured error event.</remarks>
     /// <typeparam name="TEntityId">The type of the entity identifier associated with the command.</typeparam>
     /// <param name="command">The command to be executed. Cannot be null.</param>
     /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{Guid}"/>
     /// which includes the unique identifier of the executed command if successful, or an error event if the execution
     /// fails.</returns>
    public async Task<ServiceResult<Guid>> ExecuteAsync<TEntityId>(ICommand<TEntityId> command) where TEntityId : IActorEntityId
    {
        ServiceResult<Guid> serviceResult;
        try
        {
            serviceResult = await commandContext.ExecuteAsync(command);
        }
        catch(Exception ex)
        {
            IErrorEvent errorEvent;
            logger?.LogError(ex, "{CommandName}: execution failed", command.CommandName);
            var exceptionDecorator = exceptionDecoratorFactory.GetDecorator<TState>() as IExceptionCommandDecorator<TState>;
            if (exceptionDecorator is not null)
                errorEvent = await exceptionDecorator.ConvertExceptionToErrorEventAsync(command, ex);
            else
                errorEvent = ex is IErrorEventConverter converter
                    ? converter.ToErrorEvent(command)
                    : new CommandExceptionEvent().ToErrorEvent(command, ex);
            serviceResult = new ServiceFailed<Guid>(errorEvent);
        }
        return serviceResult;
    }

    /// <summary>
    /// Executes the specified command asynchronously and returns the result.
    /// </summary>
    /// <remarks>This method logs any exceptions that occur during command execution and attempts to convert
    /// them into error events using a registered exception decorator. If no decorator is available, it uses a default
    /// error event conversion strategy.</remarks>
    /// <param name="command">The command to be executed. Must not be null.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, containing a <see
    /// cref="ServiceResult{Guid}"/>. The result contains the unique identifier of the executed command if successful,
    /// or an error event if the execution fails.</returns>
    public async ValueTask<ServiceResult<Guid>> ExecuteAsync(ICommand command)
    {
        ServiceResult<Guid> serviceResult;
        try
        {
            serviceResult = await commandContext.ExecuteAsync(command);
        }
        catch (Exception ex)
        {
            IErrorEvent errorEvent;
            logger?.LogError(ex, "{CommandName}: execution failed", command.CommandName);
            var exceptionDecorator = exceptionDecoratorFactory.GetDecorator<TState>() as IExceptionCommandDecorator<TState>;
            if (exceptionDecorator is not null)
                errorEvent = await exceptionDecorator.ConvertExceptionToErrorEventAsync(command, ex).ConfigureAwait(false);
            else
                errorEvent = ex is IErrorEventConverter converter
                    ? converter.ToErrorEvent(command)
                    : new CommandExceptionEvent().ToErrorEvent(command, ex);
            serviceResult = new ServiceFailed<Guid>(errorEvent);
        }
        return serviceResult;
    }
}
