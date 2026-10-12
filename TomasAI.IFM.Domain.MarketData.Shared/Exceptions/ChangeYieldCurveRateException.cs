using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Shared.Exceptions
{
    /// <summary>
    /// change yield curve rate exception
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing ApplicationException hierarchy observed by caller exception handlers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing ApplicationException hierarchy observed by caller exception handlers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing ApplicationException hierarchy observed by caller exception handlers.")]
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
        public IErrorEvent ToErrorEvent(ICommand command, Exception? ex = null) 
        {
            ArgumentNullException.ThrowIfNull(command);
            return new YieldCurveRateAddedFailEvent
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
}
