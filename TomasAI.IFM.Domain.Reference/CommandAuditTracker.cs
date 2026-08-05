using System.Collections.Concurrent;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference;

/// <summary>
/// Starts command auditing during parsing and asynchronously observes it before validation.
/// </summary>
internal sealed class CommandAuditTracker(IEventSourceActorDbContext dbEventSource)
{
    readonly IEventSourceActorDbContext _dbEventSource =
        dbEventSource ?? throw new ArgumentNullException(nameof(dbEventSource));
    readonly ConcurrentDictionary<ICommand, Task> _pending =
        new(ReferenceEqualityComparer.Instance);

    public void Start(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var auditTask = CreateAuditTask(command);

        // Surface an already-completed failure without blocking the actor on incomplete storage I/O.
        if (auditTask.IsCompleted)
            auditTask.GetAwaiter().GetResult();

        if (!_pending.TryAdd(command, auditTask))
            throw new InvalidOperationException($"Command audit {command.CommandId} is already pending.");
    }

    public async ValueTask CompleteAsync(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_pending.TryRemove(command, out var auditTask))
            auditTask = CreateAuditTask(command);

        await auditTask.ConfigureAwait(false);
    }

    Task CreateAuditTask(ICommand command)
        => _dbEventSource.InsertCommandLogAsync(
            command,
            DateTime.UtcNow,
            JsonConvert.SerializeObject(command));
}
