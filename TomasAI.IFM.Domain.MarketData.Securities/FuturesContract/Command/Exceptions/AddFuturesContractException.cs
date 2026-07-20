using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;

/// <summary>
/// Represents an exception that is thrown when an error occurs while adding a futures contract.
/// </summary>
/// <remarks>This exception is typically used to signal failures during the process of adding a futures contract
/// in command-handling scenarios. It also provides functionality to convert the exception into an error event for
/// event-driven error reporting.</remarks>
public class AddFuturesContractException : ApplicationException, IErrorEventConverter
{
    public AddFuturesContractException()
    {
    }

    public AddFuturesContractException(string errorMessage) : base(errorMessage)
    {
    }

    public AddFuturesContractException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }

    public IErrorEvent ToErrorEvent(ICommand command, Exception? ex = null) 
        => new FuturesContractAddedFailEvent
        {
            Subject = command.Subject,
            CommandId = command.CommandId,
            CommandName = command.GetType().Name,
            RouteTo = $"{command.RouteTo}",
            ErrorType = ErrorType.Command,
            ErrorMessage = this.Message,
            ErrorCode = command.ErrorCode,
            ErrorData = $"{this}",
            AggregateId = command.StreamId,
            CommandData = JsonConvert.SerializeObject(command, Formatting.Indented)
        };

    
};
