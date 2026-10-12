using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Application.Storage;

/// <summary>Structured persistence diagnostics with generated logging delegates.</summary>
internal static partial class StorageDiagnosticLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{MethodName}: dropping trade plan after persistence failure. PositionId={PositionId} PlanRevision={PlanRevision} ValueDate={ValueDate}")]
    internal static partial void PlanDropped(ILogger logger, Exception exception, string methodName, object positionId, long planRevision, DateOnly valueDate);
    [LoggerMessage(Level = LogLevel.Information, Message = "{Component}.{Method} slow reservation; CommandId={CommandId}; Subject={Subject}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    internal static partial void SlowReservation(ILogger logger, string component, string method, Guid commandId, object subject, double elapsedMilliseconds);
    [LoggerMessage(Level = LogLevel.Error, Message = "{Component}.{Method} payload conflict; CommandId={CommandId}; Subject={Subject}; WriteMode={WriteMode}")]
    internal static partial void ReservationConflict(ILogger logger, Exception exception, string component, string method, Guid commandId, object subject, object writeMode);
    [LoggerMessage(Level = LogLevel.Information, Message = "{Component}.{Method} completed; UnderlyingContractId={UnderlyingContractId}; ExpiryDate={ExpiryDate}; ValueDate={ValueDate}; RowCount={RowCount}; ElapsedMilliseconds={ElapsedMilliseconds}")]
    internal static partial void QuoteCompleted(ILogger logger, string component, string method, string underlyingContractId, DateOnly expiryDate, DateOnly valueDate, int rowCount, double elapsedMilliseconds);
    [LoggerMessage(Level = LogLevel.Error, Message = "{Component}.{Method} failed; UnderlyingContractId={UnderlyingContractId}; ExpiryDate={ExpiryDate}; ValueDate={ValueDate}")]
    internal static partial void QuoteFailed(ILogger logger, Exception exception, string component, string method, string underlyingContractId, DateOnly expiryDate, DateOnly valueDate);
}
