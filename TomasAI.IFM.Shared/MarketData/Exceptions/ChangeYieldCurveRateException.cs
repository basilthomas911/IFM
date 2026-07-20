using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Shared.MarketData.Exceptions
{
    /// <summary>
    /// change yield curve rate exception
    /// </summary>
    public class ChangeYieldCurveRateException : ApplicationException, IErrorEventConverter
    {
        public ChangeYieldCurveRateException()
        {
        }

        public ChangeYieldCurveRateException(string errorMessage) : base(errorMessage)
        {
        }

        public ChangeYieldCurveRateException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// convert exception to error event
        /// </summary>
        /// <param name="command"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        public IErrorEvent ToErrorEvent(ICommand command, Exception? ex = null)=> new YieldCurveRateAddedFailEvent
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
