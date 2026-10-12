using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed record SupervisorWorkerSnapshot(
    int WorkerId,
    ActorThreadState State,
    bool IsStarted,
    bool IsRunning,
    bool IsFaulted,
    ActorThreadId? CurrentMailbox,
    string ExceptionType,
    string FailureReason);
