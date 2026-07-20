using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions
{
    /// <summary>
    /// remove futures contract exception
    /// </summary>
    public class RemoveFuturesContractException : ApplicationException, IErrorEventConverter
    {
        public RemoveFuturesContractException()
        {
        }

        public RemoveFuturesContractException(string errorMessage) : base(errorMessage)
        {
        }

        public RemoveFuturesContractException(string errorMessage, Exception ex) : base(errorMessage, ex)
        {
        }

        /// <summary>
        /// convert exception details to error event
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        public IErrorEvent ToErrorEvent(ICommand command, Exception? ex = null) => new FuturesContractRemovedFailEvent
        {
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
    }
}
