using System.Collections.Concurrent;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

public sealed record SupervisorFailureRecord(
    Guid FailureId,
    DateTime FailedUtc,
    ActorMailboxId ActorId,
    ActorThreadId ThreadId,
    string Verb,
    ActorFailureStage Stage,
    string ExceptionType,
    string Error,
    Guid? PrimaryFailureId,
    ActorFailureSeverity Severity = ActorFailureSeverity.Error,
    ActorMessageOutcomeType Outcome = ActorMessageOutcomeType.EscapedFailure,
    ActorDeliveryOutcomeType DeliveryOutcome = ActorDeliveryOutcomeType.NotRequired,
    int HResult = 0,
    string ExceptionDetail = "",
    string TraceId = "",
    string SpanId = "");
