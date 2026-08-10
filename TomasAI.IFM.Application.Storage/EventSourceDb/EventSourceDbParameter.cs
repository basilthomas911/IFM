using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

internal readonly record struct GetEventLogByEventStreamId(long eventStreamId) : IBindValue
{
    public object Bind() => Values(Bigint(eventStreamId));
}
internal readonly record struct GetEventLogLastNRange(long eventStreamId) : IBindValue
{
    public object Bind() => Values(Bigint(eventStreamId));
}
internal readonly record struct GetEventLogLastNRangeByEventName(
    long eventStreamId,
    int eventNameId,
    int lastNRange) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventStreamId),
        Integer(eventNameId),
        Integer(lastNRange));
}
internal readonly record struct GetEventLogFromSnapshotLastNRange(
    long eventStreamId,
    int snapshotEventNameId,
    int rangeEventNameId,
    int lastNRange) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventStreamId),
        Integer(snapshotEventNameId),
        Integer(rangeEventNameId),
        Integer(lastNRange));
}
internal readonly record struct GetMaxEventVersion(long eventStreamId, int snapshotEventNameId) : IBindValue
{
    public object Bind() => Values(Bigint(eventStreamId), Integer(snapshotEventNameId));
}
internal readonly record struct GetEventLogByMaxEventVersion(long eventStreamId, long maxEventVersion) : IBindValue
{
    public object Bind() => Values(Bigint(eventStreamId), Bigint(maxEventVersion));
}
internal readonly record struct GetEventStreamId(string eventStream) : IBindValue
{
    public object Bind() => Values(Text(eventStream));
}
internal readonly record struct DeleteEventStreamId(string eventStream) : IBindValue
{
    public object Bind() => Values(Text(eventStream));
}
internal readonly record struct InsertEventStreamId(string eventStream) : IBindValue
{
    public object Bind() => Values(Text(eventStream));
}
internal readonly record struct GetEventNameId(string eventName, string eventTypeName) : IBindValue
{
    public object Bind() => Values(Text(eventName), Text(eventTypeName));
}
internal readonly record struct DeleteEventNameId(string eventName, string eventTypeName) : IBindValue
{
    public object Bind() => Values(Text(eventName), Text(eventTypeName));
}
internal readonly record struct InsertEventNameId(string eventName, string eventTypeName) : IBindValue
{
    public object Bind() => Values(Text(eventName), Text(eventTypeName));
}
internal readonly record struct InsertEventLog(long eventStreamId, int eventNameId, string eventData, Guid commandId, string eventTimestamp) : IBindValue
{
    public object Bind() => Values(Bigint(eventStreamId), Integer(eventNameId), Text(eventData), Uuid(commandId), Text(eventTimestamp));
}
internal readonly record struct DeleteEventLog(long eventVersion) : IBindValue
{
    public object Bind() => Values(Bigint(eventVersion));
}
internal readonly record struct GetCommandLog(Guid commandId) : IBindValue
{
    public object Bind() => Values(Uuid(commandId));
}
internal readonly record struct InsertActorCommandLog(Guid commandId, string streamId, string aggregateName, string commandName, string commandTimestamp, string commandStatus, string commandData) : IBindValue
{
    public object Bind() => Values(Uuid(commandId), Text(streamId), Text(aggregateName), Text(commandName), Text(commandTimestamp), Text(commandStatus), Text(commandData));
}
internal readonly record struct UpdateCommandLog(Guid commandId, string commandStatus, DateTime updateTimestamp) : IBindValue
{
    public object Bind() => Values(Uuid(commandId), Text(commandStatus), Timestamp(updateTimestamp));
}
internal readonly record struct DeleteEventLogByStreamId(long streamId) : IBindValue
{
    public object Bind() => Values(Bigint(streamId));
}
internal readonly record struct DeleteEventStreamById(long eventStreamId) : IBindValue
{
    public object Bind() => Values(Bigint(eventStreamId));
}
internal readonly record struct UpsertEventProjectorState(
    long eventId,
    string actorName,
    string projectorName,
    bool isReplay,
    int attemptNumber,
    string outcome,
    string stage,
    string errorMessage,
    string createdTimestamp,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(actorName),
        Text(projectorName),
        Boolean(isReplay),
        Integer(attemptNumber),
        Text(outcome),
        Text(stage),
        Text(errorMessage),
        Text(createdTimestamp),
        Text(updatedTimestamp));
}
internal readonly record struct GetEventProjectorState(long eventId, string projectorName) : IBindValue
{
    public object Bind() => Values(Bigint(eventId), Text(projectorName));
}
internal readonly record struct GetUncompletedEventProjectorEvents(string projectorName, string eventNames) : IBindValue
{
    public object Bind() => Values(Text(projectorName), Text(eventNames));
}
internal readonly record struct TryCreateEventProjectorExecutionState(
    long eventId,
    string actorName,
    string projectorName,
    bool isReplay,
    int attemptNumber,
    string outcome,
    string stage,
    string errorMessage,
    string createdTimestamp,
    string updatedTimestamp,
    DateTime updatedAtUtc) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(actorName),
        Text(projectorName),
        Boolean(isReplay),
        Integer(attemptNumber),
        Text(outcome),
        Text(stage),
        Text(errorMessage),
        Text(createdTimestamp),
        Text(updatedTimestamp),
        TimestampTz(updatedAtUtc));
}
internal readonly record struct TryClaimEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    DateTime leaseExpiresAtUtc,
    DateTime nowUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        TimestampTz(leaseExpiresAtUtc),
        TimestampTz(nowUtc),
        Text(updatedTimestamp));
}
internal readonly record struct TryRenewEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    long expectedRevision,
    DateTime nowUtc,
    DateTime leaseExpiresAtUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        Bigint(expectedRevision),
        TimestampTz(nowUtc),
        TimestampTz(leaseExpiresAtUtc),
        Text(updatedTimestamp));
}
internal readonly record struct TryReleaseEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    long expectedRevision,
    string expectedStage,
    DateTime nowUtc,
    int retryCount,
    DateTime nextAttemptAtUtc,
    DateTime lastErrorAtUtc,
    string errorMessage,
    DateTime updatedAtUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        Bigint(expectedRevision),
        Text(expectedStage),
        TimestampTz(nowUtc),
        Integer(retryCount),
        TimestampTz(nextAttemptAtUtc),
        TimestampTz(lastErrorAtUtc),
        Text(errorMessage),
        TimestampTz(updatedAtUtc),
        Text(updatedTimestamp));
}
internal readonly record struct TryTransitionEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    long expectedRevision,
    string expectedStage,
    DateTime nowUtc,
    string nextStage,
    string outcome,
    string lastCompletedStage,
    int retryCount,
    DateTime? nextAttemptAtUtc,
    DateTime? lastErrorAtUtc,
    string errorMessage,
    string blockedReason,
    DateTime updatedAtUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        Bigint(expectedRevision),
        Text(expectedStage),
        TimestampTz(nowUtc),
        Text(nextStage),
        Text(outcome),
        Text(lastCompletedStage),
        Integer(retryCount),
        TimestampTz(nextAttemptAtUtc),
        TimestampTz(lastErrorAtUtc),
        Text(errorMessage),
        Text(blockedReason),
        TimestampTz(updatedAtUtc),
        Text(updatedTimestamp));
}
internal readonly record struct TryTerminalizeEventProjectorExecution(
    long eventId,
    string projectorName,
    Guid executionToken,
    long expectedRevision,
    string expectedStage,
    DateTime nowUtc,
    string outcome,
    string lastCompletedStage,
    int retryCount,
    DateTime? lastErrorAtUtc,
    string errorMessage,
    string blockedReason,
    DateTime updatedAtUtc,
    string updatedTimestamp) : IBindValue
{
    public object Bind() => Values(
        Bigint(eventId),
        Text(projectorName),
        Uuid(executionToken),
        Bigint(expectedRevision),
        Text(expectedStage),
        TimestampTz(nowUtc),
        Text(outcome),
        Text(lastCompletedStage),
        Integer(retryCount),
        TimestampTz(lastErrorAtUtc),
        Text(errorMessage),
        Text(blockedReason),
        TimestampTz(updatedAtUtc),
        Text(updatedTimestamp));
}
internal readonly record struct GetEventProjectorRecoveryPage(
    string projectorName,
    string eventNames,
    long afterEventId,
    DateTime nowUtc,
    int batchSize) : IBindValue
{
    public object Bind() => Values(
        Text(projectorName),
        Text(eventNames),
        Bigint(afterEventId),
        TimestampTz(nowUtc),
        Integer(batchSize));
}
