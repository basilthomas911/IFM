using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Service.TradePosition.HostedService;
internal static partial class LifecycleLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Trade-position event consumer startup was cancelled by API shutdown.")]
    internal static partial void Log1(ILogger logger, Exception exception);
    [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Trade-position event consumer failed to start; the API host will remain running.")]
    internal static partial void Log2(ILogger logger, Exception exception);
    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Trade-position event consumer reached the API shutdown deadline.")]
    internal static partial void Log3(ILogger logger, Exception exception);
    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Trade-position event consumer failed while stopping; API shutdown will continue.")]
    internal static partial void Log4(ILogger logger, Exception exception);
}
