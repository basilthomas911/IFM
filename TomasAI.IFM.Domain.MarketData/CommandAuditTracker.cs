using System.Collections.Concurrent;
using Newtonsoft.Json;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData;

internal sealed class CommandAuditTracker(IEventSourceActorDbContext dbEventSource)
{
    readonly IEventSourceActorDbContext _dbEventSource = dbEventSource;
    readonly ConcurrentDictionary<ICommand, Task> _pending = new(ReferenceEqualityComparer.Instance);

    public void Start(ICommand command)
    {
        var auditTask = CreateAuditTask(command);
        if (!_pending.TryAdd(command, auditTask))
            throw new InvalidOperationException($"Command audit {command.CommandId} is already pending.");
    }

    public async ValueTask CompleteAsync(ICommand command)
        => await CompleteAsync(command, CancellationToken.None).ConfigureAwait(false);

    public async ValueTask CompleteAsync(ICommand command, CancellationToken cancellationToken)
    {
        if (!_pending.TryRemove(command, out var auditTask))
            auditTask = CreateAuditTask(command);
        await auditTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    Task CreateAuditTask(ICommand command)
        => _dbEventSource.InsertCommandLogAsync(command, DateTime.UtcNow, JsonConvert.SerializeObject(command));
}
