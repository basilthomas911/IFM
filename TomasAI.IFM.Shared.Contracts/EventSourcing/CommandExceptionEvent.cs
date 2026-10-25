using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventSourcing;

public record CommandExceptionEvent : ErrorEvent, IExceptionEvent, IErrorEventConverter
{

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0061", Justification = "The concrete converter requires an exception and validates it; the interface also supports converters without an exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1006", Justification = "The concrete converter requires an exception and validates it; the interface also supports converters without an exception.")]
    public IErrorEvent ToErrorEvent(ICommand command, Exception ex)
        
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(ex);
        return new CommandExceptionEvent
        {
            CommandId = command.CommandId,
            CommandName = command.GetType().Name,
            RouteTo = $"{command.RouteTo}",
            ErrorType = ErrorType.Command,
            ErrorMessage = ex.Message,
            ErrorCode = command.ErrorCode,
            ErrorData = $"{ex}",
            AggregateId = command.StreamId,
            CommandData = JsonConvert.SerializeObject(command, Formatting.Indented)
        };
    }
}

