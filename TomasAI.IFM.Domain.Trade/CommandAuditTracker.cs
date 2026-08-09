using System.Collections.Concurrent;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade;

/// <summary>Tracks command-audit writes across parse and validation without blocking actor parsing.</summary>
internal sealed class CommandAuditTracker(IEventSourceActorDbContext dbEventSource)
{
    readonly IEventSourceActorDbContext _dbEventSource =
        dbEventSource ?? throw new ArgumentNullException(nameof(dbEventSource));
    readonly ConcurrentDictionary<ICommand, Task> _pending =
        new(ReferenceEqualityComparer.Instance);

    public void Start(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var auditTask = _dbEventSource.InsertCommandLogAsync(
            command,
            DateTime.UtcNow,
            JsonConvert.SerializeObject(command));

        if (!_pending.TryAdd(command, auditTask))
            throw new InvalidOperationException($"Command audit {command.CommandId} is already pending.");
    }

    public async ValueTask CompleteAsync(ICommand command)
        => await CompleteAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask CompleteAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!_pending.TryRemove(command, out var auditTask))
        {
            auditTask = _dbEventSource.InsertCommandLogAsync(
                command,
                DateTime.UtcNow,
                JsonConvert.SerializeObject(command));
        }

        await auditTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }
}
