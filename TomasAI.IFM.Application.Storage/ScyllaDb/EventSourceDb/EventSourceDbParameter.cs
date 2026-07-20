using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.EventSourceDb;

internal readonly record struct GetEventLogByEventStreamId(long eventStreamId) : IBindValue
{
    public object Bind() => new { eventStreamId };
}
internal readonly record struct GetEventLogLastNRange(long eventStreamId) : IBindValue
{
    public object Bind() => new { eventStreamId };
}
internal readonly record struct GetMaxEventVersion(long eventStreamId, int snapshotEventNameId) : IBindValue
{
    public object Bind() => new { eventStreamId, snapshotEventNameId };
}
internal readonly record struct GetEventLogByMaxEventVersion(long eventStreamId, long maxEventVersion) : IBindValue
{
    public object Bind() => new { eventStreamId, maxEventVersion };
}
internal readonly record struct GetEventStreamId(string eventStream) : IBindValue
{
    public object Bind() => new { eventStream };
}
internal readonly record struct DeleteEventStreamId(string eventStream) : IBindValue
{
    public object Bind() => new { eventStream };
}
internal readonly record struct InsertEventStreamId(long eventStreamId, string eventStream) : IBindValue
{
    public object Bind() => new { eventStreamId, eventStream };
}
internal readonly record struct GetEventNameId(string eventName) : IBindValue
{
    public object Bind() => new { eventName };
}
internal readonly record struct DeleteEventNameId(string eventName, string eventTypeName) : IBindValue
{
    public object Bind() => new { eventName, eventTypeName };
}
internal readonly record struct InsertEventNameId(string eventName, string eventTypeName) : IBindValue
{
    public object Bind() => new { eventName, eventTypeName };
}
internal readonly record struct InsertEventLog(long eventStreamId, int eventNameId, string eventData, Guid commandId, string eventTimestamp) : IBindValue
{
    public object Bind() => new { eventStreamId, eventNameId, eventData, commandId, eventTimestamp };
}
internal readonly record struct DeleteEventLog(long eventVersion) : IBindValue
{
    public object Bind() => new { eventVersion };
}
internal readonly record struct GetCommandLog(Guid commandId) : IBindValue
{
    public object Bind() => new { commandId };
}
internal readonly record struct InsertActorCommandLog(Guid commandId, string streamId, string aggregateName, string commandName, string commandTimestamp, string commandStatus, string commandData) : IBindValue
{
    public object Bind() => new { commandId, streamId, aggregateName, commandName, commandTimestamp, commandStatus, commandData };
}
internal readonly record struct UpdateCommandLog(Guid commandId, string commandStatus, DateTime updateTimestamp) : IBindValue
{
    public object Bind() => new { commandId, commandStatus, updateTimestamp };
}
internal readonly record struct DeleteEventLogByStreamId(long streamId) : IBindValue
{
    public object Bind() => new { streamId };
}
internal readonly record struct DeleteEventStreamById(long eventStreamId) : IBindValue
{
    public object Bind() => new { eventStreamId };
}
