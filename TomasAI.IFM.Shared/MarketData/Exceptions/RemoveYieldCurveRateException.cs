using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Shared.MarketData.Exceptions
{
    public class RemoveYieldCurveRateException : ApplicationException, IErrorEventConverter
    {
        public RemoveYieldCurveRateException(string errorMessage) : base(errorMessage)
        {
        }

        public RemoveYieldCurveRateException() : base()
        {
        }

        public RemoveYieldCurveRateException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// convert exception to error event
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        public IErrorEvent ToErrorEvent(ICommand command, Exception ex = null) => new YieldCurveRateRemovedFailEvent
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
