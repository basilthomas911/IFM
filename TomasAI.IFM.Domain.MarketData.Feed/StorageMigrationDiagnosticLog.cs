namespace TomasAI.IFM.Domain.MarketData.Feed;

/// <summary>Compiled structured diagnostics for storage migration and provider boundaries.</summary>
internal static partial class StorageMigrationDiagnosticLog
{
    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8908, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "{Component}.{Method} Futures EOD queued trade exit: {ContractId}; SourceId={SourceId}; TickDataId={TickDataId}; queuePending={Pending}; processedCount={ProcessedCount}; QueueWaitMilliseconds={QueueWaitMilliseconds}; elapsed {ElapsedMilliseconds:F3} ms.")]
    internal static partial void Write8(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, string Component, string Method, string ContractId, global::System.Guid SourceId, global::TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.TickDataId TickDataId, long Pending, long ProcessedCount, double QueueWaitMilliseconds, double ElapsedMilliseconds);
}
