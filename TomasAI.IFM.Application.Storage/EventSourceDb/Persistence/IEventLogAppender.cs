using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;

public interface IEventLogAppender : IAsyncDisposable
{
    EventLogWriteMode WriteMode { get; }
    bool UseLz4Compression { get; }
    ValueTask<EventLogAppendResult> AppendAsync(EventLogAppendRequest request, CancellationToken cancellationToken = default);
}
