using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.Events;

namespace TomasAI.IFM.Domain.Fund.Command.Exceptions;

/// <summary>
/// Represents an exception that occurs when an order cannot be added to a fund.
/// </summary>
/// <remarks>This exception is typically thrown to indicate a failure during the process of adding an order to a
/// fund, such as validation errors or business rule violations. It provides methods to convert the exception into an
/// error event for further handling or logging.</remarks>
public class AddOrderToFundException : ApplicationException, IErrorEventConverter
{
    public AddOrderToFundException(string errorMessage) : base(errorMessage)
    {
    }

    public AddOrderToFundException(string errorMessage, Exception ex) : base(errorMessage, ex)
    {
    }

    public AddOrderToFundException() : base()
    {
    }

    /// <summary>
    /// convert exception to error event
    /// </summary>
    /// <param name="command"></param>
    /// <param name="ex"></param>
    /// <returns></returns>
    public IErrorEvent ToErrorEvent(ICommand command, Exception? ex = default!) => new OrderAddedToFundFailEvent
    {
        CommandId = command.CommandId,
        CommandName = command.GetType().Name,
        RouteTo = $"{command.RouteTo}",
        ErrorType = ErrorType.Command,
        ErrorMessage = this.Message,
        ErrorCode = command.ErrorCode,
        ErrorData = $"{this}",
        AggregateId = command.StreamId,
        CommandData = JsonConvert.SerializeObject(command, Formatting.Indented, new StringEnumConverter())
    };
}
