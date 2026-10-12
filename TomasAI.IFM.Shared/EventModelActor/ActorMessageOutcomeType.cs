using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public enum ActorMessageOutcomeType
{
    Unknown = 0,
    Succeeded = 1,
    HandledFailure = 2,
    EscapedFailure = 3,
    CancelledBeforeCommit = 4,
    CompletedAfterCancellation = 5
}
