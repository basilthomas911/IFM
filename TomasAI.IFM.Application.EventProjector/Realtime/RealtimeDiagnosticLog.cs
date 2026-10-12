using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Application.EventProjector;
internal static partial class RealtimeDiagnosticLog
{
    [LoggerMessage(EventId=1, Level=LogLevel.Information, Message="{Component}.{Method} Realtime projection stage: {ProjectorName}; {EventName}; {EntityId}; SourceId={SourceId}; stage={Stage}; elapsed {ElapsedMilliseconds:F3} ms.")]
    internal static partial void SlowStage(ILogger logger, string component, string method, string projectorName, string eventName, string entityId, object sourceId, string stage, double elapsedMilliseconds);
}
