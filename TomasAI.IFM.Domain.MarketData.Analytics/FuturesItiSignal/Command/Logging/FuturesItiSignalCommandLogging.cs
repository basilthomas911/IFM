using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Logging;

/// <summary>Contains compile-time generated logging declarations for Futures ITI commands.</summary>
internal static partial class FuturesItiSignalCommandLogging
{
    [LoggerMessage(EventId = 23810, Level = LogLevel.Trace,
        Message = "Futures ITI command evaluating CommandId={CommandId} EntityId={EntityId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod}; Method={Method}")]
    public static partial void Evaluating(ILogger logger, Guid commandId, string entityId, string contractId,
        DateOnly valueDate, TimeFrameType timePeriod, [System.Runtime.CompilerServices.CallerMemberName] string method = "");

    [LoggerMessage(EventId = 23811, Level = LogLevel.Debug,
        Message = "Futures ITI command completed without material change CommandId={CommandId} EntityId={EntityId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod} Outcome=CommandAcceptedNoChange; Method={Method}")]
    public static partial void NoChange(ILogger logger, Guid commandId, string entityId, string contractId,
        DateOnly valueDate, TimeFrameType timePeriod, [System.Runtime.CompilerServices.CallerMemberName] string method = "");

    [LoggerMessage(EventId = 23812, Level = LogLevel.Debug,
        Message = "Futures ITI signal changed CommandId={CommandId} EntityId={EntityId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod} Outcome=SignalChanged; Method={Method}")]
    public static partial void SignalChanged(ILogger logger, Guid commandId, string entityId, string contractId,
        DateOnly valueDate, TimeFrameType timePeriod, [System.Runtime.CompilerServices.CallerMemberName] string method = "");

    [LoggerMessage(EventId = 23813, Level = LogLevel.Information,
        Message = "Futures ITI event committed CommandId={CommandId} EntityId={EntityId} ContractId={ContractId} ValueDate={ValueDate} TimePeriod={TimePeriod} Outcome=EventCommitted; Method={Method}")]
    public static partial void EventCommitted(ILogger logger, Guid commandId, string entityId, string contractId,
        DateOnly valueDate, TimeFrameType timePeriod, [System.Runtime.CompilerServices.CallerMemberName] string method = "");

    [LoggerMessage(EventId = 23814, Level = LogLevel.Error,
        Message = "Futures ITI command failed CommandId={CommandId} EntityId={EntityId} Handler={Handler} Outcome=Failed ExceptionType={ExceptionType}; Method={Method}")]
    public static partial void CommandFailed(ILogger logger, Exception exception, Guid commandId, string entityId,
        string handler, string exceptionType, [System.Runtime.CompilerServices.CallerMemberName] string method = "");
}
