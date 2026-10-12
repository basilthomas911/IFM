using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

static class SupervisorHealthActionLog
{
    static readonly Action<ILogger, ActorThreadId, long, Exception?> RestartFailure =
        LoggerMessage.Define<ActorThreadId, long>(LogLevel.Error, new(7301, nameof(RestartFailed)),
            "Automatic actor-thread restart failed for {ActorThreadId} generation {Generation}.");

    internal static void RestartFailed(ILogger logger, ActorThreadId threadId, long generation, Exception exception)
        => RestartFailure(logger, threadId, generation, exception);
}
