using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Domain;

/// <summary>
/// Defines a contract for converting exceptions that occur during command execution into error events within a bounded
/// context.
/// </summary>
/// <remarks>Implementations of this interface allow exception handling logic to be decoupled from command
/// processing, enabling consistent error event generation across different bounded contexts. This is typically used in
/// systems that require reliable error reporting and recovery mechanisms.</remarks>
/// <typeparam name="TState">The type of the bounded context state associated with the command execution. Must implement <see
/// cref="IBoundedContextState"/>.</typeparam>
public interface IExceptionCommandDecorator<TState>where TState : IBoundedContextState
{
    Task<IErrorEvent> ConvertExceptionToErrorEventAsync(ICommand command, Exception ex);
}
