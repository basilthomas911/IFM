using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Application.Actor.Client;
internal static partial class QueryDiagnosticLog
{
    [LoggerMessage(EventId=1, Level=LogLevel.Error, Message="Error handling query exception in {ActorName} for thread {ThreadId}")]
    internal static partial void ReplyFailed(ILogger logger, Exception exception, string actorName, ActorThreadId threadId);
}
