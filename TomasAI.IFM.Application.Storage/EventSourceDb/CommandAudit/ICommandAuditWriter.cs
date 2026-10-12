using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

internal interface ICommandAuditWriter : IAsyncDisposable
{
    ValueTask<CommandAuditWriteResult> ReserveAsync(
        CommandAuditEnvelope envelope,
        CancellationToken cancellationToken = default);
}
