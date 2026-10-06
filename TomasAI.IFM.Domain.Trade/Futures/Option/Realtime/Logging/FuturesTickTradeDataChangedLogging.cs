using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime.Logging;

internal static partial class FuturesTickTradeDataChangedLogging
{
    [LoggerMessage(
        EventId = 32001,
        Level = LogLevel.Information,
        Message = "Ignoring Futures Option tick for ContractId {ContractId}; no route is registered.; Method={Method}")]
    internal static partial void UnknownContract(ILogger logger, string contractId, [System.Runtime.CompilerServices.CallerMemberName] string method = "");

    [LoggerMessage(
        EventId = 32002,
        Level = LogLevel.Information,
        Message = "Ignoring Futures Option tick because ContractId {ContractId} has no open position.; Method={Method}")]
    internal static partial void NoOpenPosition(ILogger logger, string contractId, [System.Runtime.CompilerServices.CallerMemberName] string method = "");

    [LoggerMessage(
        EventId = 32003,
        Level = LogLevel.Error,
        Message = "Failed to route Futures Option tick for ContractId {ContractId} to position {PositionId}, leg {TradeLegId}. Error {ErrorCode}: {ErrorMessage}; Method={Method}")]
    internal static partial void RouteSendFailed(
        ILogger logger,
        string contractId,
        string positionId,
        Guid tradeLegId,
        int errorCode,
        string errorMessage, [System.Runtime.CompilerServices.CallerMemberName] string method = "");
}
