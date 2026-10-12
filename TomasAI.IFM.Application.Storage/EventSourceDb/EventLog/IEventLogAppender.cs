


namespace TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;

public interface IEventLogAppender : IAsyncDisposable
{
    EventLogWriteMode WriteMode { get; }
    bool UseLz4Compression { get; }
    ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default);
}
