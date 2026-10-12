using System.Diagnostics;
using TomasAI.IFM.Shared.EventModelActor;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.EventProjector;

internal interface IEventProjectorTransientQueue
{
    ValueTask StartAsync(
        Func<IEvent, CancellationToken, ValueTask> handler,
        CancellationToken cancellationToken = default);

    ValueTask EnqueueAsync(IEvent domainEvent, CancellationToken cancellationToken = default);

    /// <summary>Attempts immediate admission of a snapshot projection, rejecting a full or stopped queue.</summary>
    bool TryEnqueueSnapshot(IEvent domainEvent);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
