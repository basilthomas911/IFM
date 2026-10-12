using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Persistence;

/// <summary>Logs dropped plan writes and optional publication failures without scheduling retries.</summary>
internal static partial class TradePlanPersistenceLog
{
    [LoggerMessage(EventId = 24050, Level = LogLevel.Error,
        Message = "Atomic trade plan write failed and snapshot dropped CommandId={CommandId} StreamId={StreamId} SourceEventId={SourceEventId} PlanRevision={PlanRevision} ValueDate={ValueDate}")]
    internal static partial void Dropped(ILogger logger, Exception exception, Guid commandId, string streamId,
        Guid? sourceEventId, long? planRevision, DateOnly? valueDate);

    [LoggerMessage(EventId = 24051, Level = LogLevel.Error,
        Message = "Committed trade plan notification failed CommandId={CommandId} StreamId={StreamId} SourceEventId={SourceEventId}")]
    internal static partial void PublicationFailed(ILogger logger, Exception exception, Guid commandId, string streamId, Guid? sourceEventId);
}
