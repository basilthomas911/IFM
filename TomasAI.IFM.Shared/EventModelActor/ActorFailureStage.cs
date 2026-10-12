using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum ActorFailureStage
{
    Unknown = 0,
    Admission = 1,
    Parsing = 2,
    Validation = 3,
    Deduplication = 4,
    StateReplay = 5,
    Execution = 6,
    Persistence = 7,
    Projection = 8,
    Publication = 9,
    ExceptionHandling = 10,
    Cleanup = 11,
    Reply = 12,
    MailboxInfrastructure = 13,
    WorkerLoop = 14,
    Startup = 15,
    Shutdown = 16,
    Drain = 17,
    Restart = 18
}
