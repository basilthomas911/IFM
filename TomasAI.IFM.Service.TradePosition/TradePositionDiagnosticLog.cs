using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Service.TradePosition;
internal static partial class TradePositionDiagnosticLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "TradePositionService started")]
    internal static partial void Started(ILogger logger);
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "TradePosition: {EventType} failed")]
    internal static partial void EventFailed(ILogger logger, Exception exception, string eventType);
    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "{FailureDetail}")]
    internal static partial void Failed(ILogger logger, Exception exception, string failureDetail);
}
