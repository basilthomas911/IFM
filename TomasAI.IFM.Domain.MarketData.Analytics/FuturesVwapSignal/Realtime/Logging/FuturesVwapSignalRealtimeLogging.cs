using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Realtime.Logging;

/// <summary>Contains compile-time generated logging declarations for VWAP realtime recovery.</summary>
internal static partial class FuturesVwapSignalRealtimeLogging
{
    [LoggerMessage(EventId = 26410, Level = LogLevel.Error,
        Message = "VWAP replay batch failed ContractId={ContractId} BatchOrdinal={BatchOrdinal} IsFinal={IsFinal} ErrorCode={ErrorCode} Error={Error}")]
    public static partial void ReplayBatchFailed(
        ILogger logger,
        string contractId,
        long batchOrdinal,
        bool isFinal,
        int errorCode,
        string error);

    [LoggerMessage(EventId = 26411, Level = LogLevel.Information,
        Message = "VWAP startup replay completed ContractId={ContractId} ValueDate={ValueDate} LiveStreamEpochId={LiveStreamEpochId} BatchCount={BatchCount}")]
    public static partial void ReplayCompleted(
        ILogger logger,
        string contractId,
        DateOnly valueDate,
        Guid liveStreamEpochId,
        long batchCount);
}
