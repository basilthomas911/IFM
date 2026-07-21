using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Shared.MarketData.Exceptions;

/// <summary>
/// add yield curve rate exception
/// </summary>
public class AddYieldCurveRateException : ApplicationException , IErrorEventConverter
{
    public AddYieldCurveRateException()
    {

    }

    public AddYieldCurveRateException(string errorMessage) : base(errorMessage)
    {
    }

    public AddYieldCurveRateException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public IErrorEvent ToErrorEvent(ICommand command, Exception? ex = null) => new YieldCurveRateAddedFailEvent
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


