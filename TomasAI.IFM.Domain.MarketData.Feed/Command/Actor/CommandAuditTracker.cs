using System.Collections.Concurrent;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;

/// <summary>
/// Starts command auditing during parsing and asynchronously joins it before validation.
/// </summary>
internal sealed class CommandAuditTracker(IEventSourceActorDbContext dbEventSource)
{
    readonly IEventSourceActorDbContext _dbEventSource =
        dbEventSource ?? throw new ArgumentNullException(nameof(dbEventSource));
    readonly ConcurrentDictionary<Guid, Task> _pending = new();

    public void Start(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var auditTask = _dbEventSource.InsertCommandLogAsync(
            command,
            DateTime.UtcNow,
            JsonConvert.SerializeObject(command));

        if (!_pending.TryAdd(command.CommandId, auditTask))
            throw new InvalidOperationException($"Command audit {command.CommandId} is already pending.");
    }

    public async ValueTask CompleteAsync(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_pending.TryRemove(command.CommandId, out var auditTask))
        {
            auditTask = _dbEventSource.InsertCommandLogAsync(
                command,
                DateTime.UtcNow,
                JsonConvert.SerializeObject(command));
        }

        await auditTask.ConfigureAwait(false);
    }
}
