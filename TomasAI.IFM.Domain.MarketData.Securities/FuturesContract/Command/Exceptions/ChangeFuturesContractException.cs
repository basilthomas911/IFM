using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Shared.MarketData.Exceptions
{
    /// <summary>
    /// change futures contract exception
    /// </summary>
    public class ChangeFuturesContractException : ApplicationException, IErrorEventConverter
    {
        public ChangeFuturesContractException(string errorMessage) : base(errorMessage)
        {
        }

        public ChangeFuturesContractException(string errorMessage, Exception ex) : base(errorMessage, ex)
        {
        }

        /// <summary>
        /// convert exception details to error event
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        public IErrorEvent ToErrorEvent(ICommand command, Exception? ex = null) => new FuturesContractChangedFailEvent
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
